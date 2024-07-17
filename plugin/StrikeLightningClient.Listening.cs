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
using Microsoft.EntityFrameworkCore;
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
		return Task.FromResult<ILightningInvoiceListener>(listener);
	}

	public async Task<bool> ExecCurrencyConversion(CurrencyExchangeQuoteReq req, CancellationToken cancellation = new())
	{
		var resp = await _client.CurrencyExchanges.CreateQuote(req);
		var exec = await _client.CurrencyExchanges.ExecuteQuote(resp.Id);

		return exec.IsSuccessStatusCode;
	}

	private class Listener : ILightningInvoiceListener
	{
		private readonly StrikeLightningClient _client;
		private readonly IEventAggregatorSubscription _subscription;
		private readonly Channel<StrikePaidInvoice> _channel = Channel.CreateUnbounded<StrikePaidInvoice>();

		public Listener(StrikeLightningClient client)
		{
			_client = client;
			_subscription = _client._eventAggregator.Subscribe<StrikePaidInvoice>(o =>
			{
				if (o.TenantId != client._tenantId)
					return;
				_channel.Writer.TryWrite(o);
			});
		}

		public void Dispose()
		{
			_subscription.Dispose();
			_channel.Writer.TryComplete();
		}

		public async Task<LightningInvoice> WaitInvoice(CancellationToken cancellation)
		{
			while (true)
			{
				await using var db = _client.DbContextFactory.CreateContext();
				var q = await db.Quotes.FirstOrDefaultAsync(a => a.TenantId == _client._tenantId && a.Paid && !a.Observed,
					cancellationToken: cancellation);
				if (q != null)
				{
					var lightningInvoice = new LightningInvoice
					{
						Status = LightningInvoiceStatus.Paid,
						Amount = new LightMoney(q.RequestedBtcAmount, LightMoneyUnit.BTC),
						AmountReceived = new LightMoney(q.RequestedBtcAmount, LightMoneyUnit.BTC),
						PaidAt = DateTimeOffset.Now,
						PaymentHash = q.PaymentHash,
						BOLT11 = q.LightningInvoice,
						Id = q.InvoiceId,
						ExpiresAt = q.ExpiresAt
					};

					q.Observed = true;
					await db.SaveChangesAsync(cancellation);

					return lightningInvoice;
				}

				// if invoice is not returned, wait for a second before checking again
				await _channel.Reader.ReadAsync(cancellation);
			}
		}
	}
}
