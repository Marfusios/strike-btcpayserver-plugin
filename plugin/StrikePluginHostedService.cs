using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BTCPayServer.Events;
using BTCPayServer.HostedServices;
using BTCPayServer.Plugins.Strike.Persistence;
using Microsoft.Extensions.Logging;
using Strike.Client.CurrencyExchanges;
using Strike.Client.Models;

namespace BTCPayServer.Plugins.Strike;

public class StrikePluginHostedService : EventHostedServiceBase
{
	private readonly StrikeStorageFactory _db;
	private readonly StrikeLightningConnectionStringHandler _handler;

	public StrikePluginHostedService(EventAggregator eventAggregator, ILogger<StrikePluginHostedService> logger,
		StrikeStorageFactory db, StrikeLightningConnectionStringHandler handler) : base(eventAggregator, logger)
	{
		_db = db;
		_handler = handler;
	}

	protected override void SubscribeToEvents()
	{
		Subscribe<InvoiceEvent>();
		base.SubscribeToEvents();
	}
	
	protected override async Task ProcessEvent(object evt, CancellationToken cancellationToken)
	{
		// on every paid invoice we'll convert those paid quotes to currency of choice if needed
		if (evt is InvoiceEvent invoiceEvent && new[]
		    {
			    InvoiceEvent.PaidInFull
		    }.Contains(invoiceEvent.Name))
		{
			var storage = _db.ResolveStorage();
			var quotes = await storage.GetPaidQuotesToConvert(cancellationToken);
			if (quotes.Length == 0)
			{
				return;
			}

			var strikeClient = _handler.Latest;
			foreach (var q in quotes)
			{
				var convertTo = (Currency)Enum.Parse(typeof(Currency), q.PaidConvertTo, true);
				var target = (Currency)Enum.Parse(typeof(Currency), q.TargetCurrency, true);
				var req = new CurrencyExchangeQuoteReq
				{
					Sell = target,
					Buy = convertTo,
					Amount = new CurrencyExchangeAmount
					{
						Currency = target, Amount = q.TargetAmount, FeePolicy = FeePolicy.Exclusive
					}
				};
				
				var success = await strikeClient.ExecCurrencyConversion(req, cancellationToken);
				if (success)
				{
					q.PaidConvertTo = null;
					await storage.Store(q);
				}
			}
		}

		await base.ProcessEvent(evt, cancellationToken);
	}
}
