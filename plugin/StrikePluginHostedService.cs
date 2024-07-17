using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BTCPayServer.Events;
using BTCPayServer.HostedServices;
using BTCPayServer.Payments;
using BTCPayServer.Payments.Lightning;
using BTCPayServer.Plugins.Strike.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NBXplorer;
using Strike.Client.CurrencyExchanges;
using Strike.Client.Invoices;
using Strike.Client.Models;

namespace BTCPayServer.Plugins.Strike;

public record StrikePaidInvoice(string TenantId, Invoice Invoice);

public class StrikePluginHostedService : EventHostedServiceBase, IDisposable
{
	private readonly ILogger<StrikePluginHostedService> _logger;
	private readonly StrikeDbContextFactory _dbContextFactory;
	private readonly StrikeLightningClientFactory _hodler;

	public StrikePluginHostedService(EventAggregator eventAggregator, ILogger<StrikePluginHostedService> logger,
		StrikeDbContextFactory dbContextFactory, StrikeLightningClientFactory hodler) : base(eventAggregator, logger)
	{
		_logger = logger;
		_dbContextFactory = dbContextFactory;
		_hodler = hodler;
	}

	private readonly CancellationTokenSource _cts = new();
	private Task? _strikeApiListenLoop;

	public void Dispose()
	{
		if (_cts.IsCancellationRequested) return;
		_cts.Cancel();
	}

	protected override void SubscribeToEvents()
	{
		_strikeApiListenLoop = StartStrikeApiLoop();
		Subscribe<InvoiceEvent>();
		base.SubscribeToEvents();
	}

	private async Task? StartStrikeApiLoop()
	{
		var cancellation = _cts.Token;
		while (!cancellation.IsCancellationRequested)
		{
			try
			{
				await cleanupOldExpiredQuotes();
				
				await using var db = _dbContextFactory.CreateContext();
				
				// update quotes that are waiting for payment
				var waitingQuotes = await db.Quotes.Where(a => !a.Observed && !a.Paid).ToArrayAsync(cancellation);

				var distinctTenants = waitingQuotes.Select(b => b.TenantId).Distinct().ToArray();
				foreach (var tenantId in distinctTenants)
				{
					var tenantQuotes = waitingQuotes.Where(a => a.TenantId == tenantId).ToArray();
					var client = _hodler.GetClient(tenantId);
					
					// clients may still not be initialized during BTCPayServer startup, so we can wait for them to be init 
					if (client == null)
						continue;

					await updateInvoicesForTenant(client, tenantQuotes);
				}

				await Task.Delay(1000, cancellation);
			}
			catch when (cancellation.IsCancellationRequested)
			{
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "StrikePluinHostedService error: "+ ex);
			}
		}
	}

	// on old instances, we may have quotes that are expired but not observed... leaving this for extreme cases
	private async Task cleanupOldExpiredQuotes()
	{
		await using var db = _dbContextFactory.CreateContext();
		
		var expiredQuotes = db.Quotes.Where(a => 
			!a.Observed && !a.Paid && a.ExpiresAt < DateTimeOffset.UtcNow);
		foreach (var expired in expiredQuotes)
			expired.Observed = true;
                
		await db.SaveChangesAsync(_cts.Token);
	}

	private async Task updateInvoicesForTenant(StrikeLightningClient client, StrikeQuote[] quotes)
	{
		var invoices = await queryStrikeApi(client, quotes);
		
		await using var db = _dbContextFactory.CreateContext();
		foreach (var invoice in invoices.Where(a => a.State == InvoiceState.Paid))
		{
			var quote = await db.Quotes.FindAsync(invoice.InvoiceId.ToString());
			if (quote == null)
				continue;

			quote.Paid = true;

			// saving changes so that invoice listener can immediately pick up on this
			await db.SaveChangesAsync();
			EventAggregator.Publish(new StrikePaidInvoice(quote.TenantId, invoice));
			// PROCESS CURRENCY CONVERSION
			if (quote.PaidConvertTo != null)
			{
				var convertTo = (Currency)Enum.Parse(typeof(Currency), quote.PaidConvertTo, true);
				var target = (Currency)Enum.Parse(typeof(Currency), quote.TargetCurrency, true);
				var req = new CurrencyExchangeQuoteReq
				{
					Sell = target,
					Buy = convertTo,
					Amount = new CurrencyExchangeAmount
					{
						Currency = target, Amount = quote.TargetAmount, FeePolicy = FeePolicy.Exclusive
					}
				};

				var success = await client.ExecCurrencyConversion(req, _cts.Token);
				if (success)
				{
					quote.PaidConvertTo = null;
					await db.SaveChangesAsync(_cts.Token);
				}
			}
		}
		
		// handling invoices that are explicity cancelled...
		foreach (var invoice in invoices.Where(a => 
			         a.State is InvoiceState.Canceled or InvoiceState.Reversed or InvoiceState.Undefined))
		{
				var quote = await db.Quotes.FindAsync(invoice.InvoiceId.ToString());
				if (quote == null)
					continue;

				quote.Observed = true;
		}
		await db.SaveChangesAsync(_cts.Token);
	}		

	private async Task<List<Invoice>> queryStrikeApi(StrikeLightningClient client, StrikeQuote[] quotes)
	{
		var bulks = quotes.Batch(100);
		var invoices = new List<Invoice>();

		foreach (var bulk in bulks)
		{ 
			var invoiceIds = string.Join(',', bulk.Select(a=>a.InvoiceId));
			var filter = $"invoiceId in ({invoiceIds})";
			var collection = await client.Client.Invoices.GetInvoices(filter);
			
			invoices.AddRange(collection.Items);
		}

		return invoices;
	}
	

	protected override async Task ProcessEvent(object evt, CancellationToken cancellationToken)
	{
		// handle expired invoices to stop listening on Strike Api
		if (evt is InvoiceEvent { Name: InvoiceEvent.Expired } invoiceEvent)
		{
			var paymentMethod = new PaymentMethodId("BTC", PaymentTypes.LightningLike);
			var pm = invoiceEvent.Invoice.GetPaymentMethod(paymentMethod);
			if (pm?.GetPaymentMethodDetails() is LightningLikePaymentMethodDetails lightning)
			{
				await using var db = _dbContextFactory.CreateContext();
				var quote = await db.Quotes.FindAsync(new[] { lightning.InvoiceId }, cancellationToken: cancellationToken);
				if (quote != null)
				{
					quote.Observed = true;
					await db.SaveChangesAsync(cancellationToken);
				}
			}
		}
		
		await base.ProcessEvent(evt, cancellationToken);
	}
}
