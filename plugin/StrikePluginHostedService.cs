using System;
using System.Threading;
using System.Threading.Tasks;
using BTCPayServer.Events;
using BTCPayServer.HostedServices;
using BTCPayServer.Payments;
using BTCPayServer.Payments.Lightning;
using BTCPayServer.Plugins.Strike.Persistence;
using Microsoft.Extensions.Logging;
using Strike.Client.Models;

namespace BTCPayServer.Plugins.Strike;

public class StrikePluginHostedService : EventHostedServiceBase, IDisposable
{
	private readonly ILogger<StrikePluginHostedService> _logger;
	private readonly StrikeStorageFactory _storageFactory;
	private readonly StrikeLightningClientLookup _clientLookup;

	private readonly CancellationTokenSource _cts = new();

	public StrikePluginHostedService(EventAggregator eventAggregator, ILogger<StrikePluginHostedService> logger,
		StrikeStorageFactory storageFactory, StrikeLightningClientLookup clientLookup) : base(eventAggregator, logger)
	{
		_logger = logger;
		_storageFactory = storageFactory;
		_clientLookup = clientLookup;
	}

	public void Dispose()
	{
		if (_cts.IsCancellationRequested)
			return;
		_cts.Cancel();
	}

	protected override void SubscribeToEvents()
	{
		Subscribe<InvoiceEvent>();
		base.SubscribeToEvents();
	}

	protected override async Task ProcessEvent(object evt, CancellationToken cancellationToken)
	{
		// convert paid invoice to the requested currency
		if (evt is InvoiceEvent { Name: InvoiceEvent.Completed or InvoiceEvent.MarkedCompleted or InvoiceEvent.Confirmed } invoiceEvent)
		{
			var paymentMethod = new PaymentMethodId("BTC", PaymentTypes.LightningLike);
			var pm = invoiceEvent.Invoice.GetPaymentMethod(paymentMethod);
			if (pm?.GetPaymentMethodDetails() is LightningLikePaymentMethodDetails lightning)
			{
				await using var storage = _storageFactory.ResolveStorage();
				var quote = await storage.FindQuoteByInvoiceId(lightning.InvoiceId);
				if (quote != null)
				{
					await ConvertQuote(quote, storage);
				}
			}
		}

		await base.ProcessEvent(evt, cancellationToken);
	}

	private async Task ConvertQuote(StrikeQuote quote, StrikeStorage storage)
	{
		if (!quote.Paid || quote.ConvertToCurrency == null || quote.Converted)
			return;

		var client = _clientLookup.GetClient(quote.TenantId);
		if (client == null)
		{
			_logger.LogWarning("Client not found for tenant {tenantId}. Cannot convert quote for invoice {invoiceId} to {currency}",
				quote.TenantId, quote.InvoiceId, quote.ConvertToCurrency);
			return;
		}

		var to = Enum.Parse<Currency>(quote.ConvertToCurrency, true);
		var from = Enum.Parse<Currency>(quote.TargetCurrency, true);
		var amount = quote.TargetAmount;
		var idempotency = Guid.Parse(quote.InvoiceId);

		var converted = await client.ConvertAmount(from, to, amount, idempotency);
		quote.Converted = converted;
		await storage.Store(quote);
	}
}
