using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BTCPayServer.Lightning;
using BTCPayServer.Plugins.Strike.Persistence;
using Microsoft.Extensions.Logging;
using NBXplorer;
using Strike.Client.Invoices;

namespace BTCPayServer.Plugins.Strike;

public partial class StrikeLightningClient
{
	public Task<ILightningInvoiceListener> Listen(CancellationToken cancellation = new())
	{
		return Task.FromResult<ILightningInvoiceListener>(new Listener(this));
	}

	private class Listener : ILightningInvoiceListener
	{
		private readonly StrikeLightningClient _client;
		private readonly List<LightningInvoice> _completedToBeReported = new();

		public Listener(StrikeLightningClient client)
		{
			_client = client;
		}

		public void Dispose()
		{
		}

		public async Task<LightningInvoice> WaitInvoice(CancellationToken cancellation)
		{
			try
			{
				return await ReturnCompletedOrEmpty(cancellation);
			}
			catch (Exception e)
			{
				_client._logger.LogWarning(e, "Failed while listening for invoice status, error: {errorMessage}", e.Message);
				_completedToBeReported.Clear();
				await Task.Delay(TimeSpan.FromSeconds(5), cancellation);
			}

			return EmptyResponse();
		}

		private async Task<LightningInvoice> ReturnCompletedOrEmpty(CancellationToken cancellation)
		{
			if (_completedToBeReported.Count == 0)
			{
				var completed = await GetCompleted(cancellation);
				if (completed.Length == 0)
				{
					// nothing is paid or expired yet, let's wait a bit and restart cycle
					await Task.Delay(TimeSpan.FromSeconds(2), cancellation);
					return EmptyResponse();
				}

				// store completed invoices and report them one by one
				_completedToBeReported.AddRange(completed);
			}

			foreach (var invoice in _completedToBeReported.ToArray())
			{
				await using var storage = _client._db.ResolveStorage();
				var quote = await storage.FindQuoteByInvoiceId(invoice.Id);
				if (quote == null)
				{
					_completedToBeReported.Remove(invoice);
					continue;
				}

				quote.Paid = invoice.Status == LightningInvoiceStatus.Paid;
				quote.Observed = true;
				await storage.Store(quote);

				_completedToBeReported.Remove(invoice);
				return invoice;
			}

			return EmptyResponse();
		}

		// BTCPayServer expects a non-null invoice to be returned
		private static LightningInvoice EmptyResponse() => new()
		{
			Id = string.Empty
		};

		private async Task<LightningInvoice[]> GetCompleted(CancellationToken cancellation)
		{
			await using var storage = _client._db.ResolveStorage();
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
				var filter = $"{nameof(Invoice.InvoiceId)} in ({string.Join(',', ids)})";
				var collection = await _client._client.Invoices.GetInvoices(filter);
				invoices.AddRange(collection.Items);
			}

			return invoices.ToArray();
		}
	}
}
