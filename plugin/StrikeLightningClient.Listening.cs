using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using BTCPayServer.Lightning;
using BTCPayServer.Plugins.Strike.Persistence;
using ExchangeSharp;
using Microsoft.Extensions.Logging;
using NBXplorer;
using Strike.Client.CurrencyExchanges;
using Strike.Client.Invoices;
using Strike.Client.Models;

namespace BTCPayServer.Plugins.Strike;

public partial class StrikeLightningClient
{
	public Task<ILightningInvoiceListener> Listen(CancellationToken cancellation = new())
	{
		var listener = new Listener(this);
		listener.StartListening();
		return Task.FromResult<ILightningInvoiceListener>(listener);
	}

	public async Task<bool> ExecCurrencyConversion(CurrencyExchangeQuoteReq req, CancellationToken cancellation = new())
	{
		var resp = await _client.CurrencyExchanges.PostCurrencyExchangeQuote(req);
		var exec = await _client.CurrencyExchanges.PatchExecuteQuote(resp.Id);

		return exec.IsSuccessStatusCode;
	}

	private class Listener : ILightningInvoiceListener
	{
		private readonly StrikeLightningClient _client;
		private readonly Channel<LightningInvoice> _invoices = Channel.CreateUnbounded<LightningInvoice>();
		private readonly CancellationTokenSource _cts = new();
		private Task? _listenLoop;

		public Listener(StrikeLightningClient client)
		{
			_client = client;
		}

		public void Dispose()
		{
			if (_cts.IsCancellationRequested)
				return;
			_cts.Cancel();
			_invoices.Writer.TryComplete();
		}

		internal void StartListening()
		{
			if (_listenLoop != null)
				return;
			_listenLoop = ListenLoop();
		}

		private async Task? ListenLoop()
		{
			var cancellation = _cts.Token;
			try
			{
				while (!cancellation.IsCancellationRequested)
				{
					var completed = await GetCompleted(cancellation);
					if (completed.Length == 0)
					{
						// nothing is paid or expired yet, let's wait a bit and restart cycle
						await Task.Delay(TimeSpan.FromSeconds(1), cancellation);
						continue;
					}

					foreach (var invoice in completed)
					{
						var storage = _client._db.ResolveStorage();
						var quote = await storage.FindQuoteByInvoiceId(invoice.Id);
						if (quote == null)
							continue;

						quote.Paid = invoice.Status == LightningInvoiceStatus.Paid;
						quote.Observed = true;

						// TODO: Discuss if there is a better way to handle signaling; maybe even whole fetching of paid quotes
						// now that we have StrikePluginHostedService with events

						// if convertTo is different currency, label this payment to execute conversion
						if (_client._convertToCurrency != Currency.Undefined &&
							_client._convertToCurrency.ToStringUpperInvariant() != quote.TargetCurrency)
						{
							quote.PaidConvertTo = _client._convertToCurrency.ToStringUpperInvariant();
						}

						if (_invoices.Writer.TryWrite(invoice))
							await storage.Store(quote);
					}
				}
			}
			catch when (cancellation.IsCancellationRequested)
			{

			}
			catch (Exception ex)
			{
				_invoices.Writer.TryComplete(ex);
			}
			finally
			{
				Dispose();
			}
		}

		public async Task<LightningInvoice> WaitInvoice(CancellationToken cancellation)
		{
			try
			{
				return await _invoices.Reader.ReadAsync(cancellation);
			}
			catch (ChannelClosedException ex) when (ex.InnerException is null)
			{
				throw new OperationCanceledException();
			}
			catch (ChannelClosedException ex) when (ex.InnerException is not null)
			{
				_client._logger.LogWarning(ex.InnerException, "Failed while listening for invoice status, error: {errorMessage}", ex.InnerException.Message);
				ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
				throw;
			}
		}

		private async Task<LightningInvoice[]> GetCompleted(CancellationToken cancellation)
		{
			var storage = _client._db.ResolveStorage();
			var unobserved = await storage.GetUnobserved(cancellation);
			if (unobserved.Length == 0)
				return Array.Empty<LightningInvoice>();

			var invoices = await QueryStrikeApi(unobserved);
			var converted = ConvertToBtcPayFormat(invoices, unobserved).ToArray();
			var completed = converted
				.Where(x => x.Status != LightningInvoiceStatus.Unpaid)
				.ToArray();
			return completed;
		}

		private IEnumerable<LightningInvoice> ConvertToBtcPayFormat(Invoice[] invoices, StrikeQuote[] unobserved)
		{
			foreach (var invoice in invoices)
			{
				var quote = unobserved.FirstOrDefault(x => x.InvoiceId == invoice.InvoiceId.ToString());
				if (quote == null)
					continue;
				var converted = _client.ConvertInvoice(invoice, quote);
				if (converted == null)
					continue;

				yield return converted;
			}
		}

		private async Task<Invoice[]> QueryStrikeApi(StrikeQuote[] unobserved)
		{
			var bulks = unobserved.Batch(100);
			var invoices = new List<Invoice>();

			foreach (var bulk in bulks)
			{
				var ids = bulk.Select(x => Guid.Parse(x.InvoiceId)).ToArray();
				var collection = await _client._client.Invoices.GetInvoices(builder => builder
					.Filter((e, f, o) => o.In(e.InvoiceId, ids))
					.OrderByDescending(x => x.Created));
				invoices.AddRange(collection.Items);
			}

			return invoices.ToArray();
		}
	}
}
