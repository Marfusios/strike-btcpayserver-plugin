using System;
using BTCPayServer.Data;
using BTCPayServer.Payments;
using BTCPayServer.Payments.Lightning;
using BTCPayServer.Services.Invoices;
using Strike.Client;

namespace BTCPayServer.Plugins.Strike;
public class StrikeClientFinder
{
	private readonly BTCPayNetworkProvider _btcPayNetworkProvider;
	private readonly StrikeLightningConnectionStringHandler _strikeHandler;
	private readonly PaymentMethodHandlerDictionary _paymentMethods;

	public StrikeClientFinder(BTCPayNetworkProvider btcPayNetworkProvider, StrikeLightningConnectionStringHandler strikeHandler, PaymentMethodHandlerDictionary paymentMethods)
	{
		_btcPayNetworkProvider = btcPayNetworkProvider;
		_strikeHandler = strikeHandler;
		_paymentMethods = paymentMethods;
	}


	public StrikeClient? TryGetClient(StoreData? store, out string? error)
	{
		try
		{
			return GetClient(store, out error);
		}
		catch (Exception e)
		{
			error = $"Error while resolving Strike client: {e.Message}";
			return null;
		}
	}

	private StrikeClient? GetClient(StoreData? store, out string? error)
	{
		error = null;

		if (store == null)
		{
			error = "Store not found";
			return null;
		}

		var btcCode = "BTC";
		var network = _btcPayNetworkProvider.GetNetwork<BTCPayNetwork>(btcCode);
		var pmi = PaymentTypes.LN.GetPaymentMethodId(btcCode);
		var existing = store.GetPaymentMethodConfig<LightningPaymentMethodConfig>(pmi, _paymentMethods);

		if (existing == null)
		{
			error = "Lightning payment method is not configured";
			return null;
		}

		var lnConnectionString = existing.GetExternalLightningUrl();
		if (string.IsNullOrWhiteSpace(lnConnectionString))
		{
			error = "Connection string for lightning payment method is empty";
			return null;
		}

		if (_strikeHandler.Create(lnConnectionString, network.NBitcoinNetwork, out error) is StrikeLightningClient client)
			return client.Client;

		error ??= "Currently selected lightning payment method is not Strike";
		return null;
	}
}
