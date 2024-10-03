using System;
using System.Linq;
using BTCPayServer.Lightning;
using BTCPayServer.Plugins.Strike.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Strike.Client;
using Strike.Client.Models;
using Network = NBitcoin.Network;
using Lookup = BTCPayServer.Plugins.Strike.StrikeLightningClientLookup;

namespace BTCPayServer.Plugins.Strike;

public class StrikeLightningConnectionStringHandler : ILightningConnectionStringHandler
{
	private readonly IServiceProvider _serviceProvider;
	private readonly ILoggerFactory _loggerFactory;


	public StrikeLightningConnectionStringHandler(IServiceProvider serviceProvider, ILoggerFactory loggerFactory)
	{
		_serviceProvider = serviceProvider;
		_loggerFactory = loggerFactory;
	}

	public ILightningClient? Create(string connectionString, Network network, out string? error)
	{
		try
		{
			return CreateInternal(connectionString, network, out error);
		}
		catch (Exception e)
		{
			error = $"Error while initializing Strike plugin: {e.Message}";
			return null;
		}
	}

	private ILightningClient? CreateInternal(string connectionString, Network network, out string? error)
	{
		var kv = LightningConnectionStringHelper.ExtractValues(connectionString, out var type);
		if (type != "strike")
		{
			error = null;
			return null;
		}

		var environment = network.Name switch
		{
			nameof(Network.Main) => StrikeEnvironment.Live,
			_ => StrikeEnvironment.Development
		};
		Uri? serverUrl = null;

		// Allow server URL override, used for local development
		if (kv.TryGetValue("server", out var serverStr))
		{
			if (!Uri.TryCreate(serverStr, UriKind.Absolute, out serverUrl)
				|| (serverUrl.Scheme != "http" && serverUrl.Scheme != "https"))
			{
				error = "The key 'server' should be an URI starting by http:// or https://";
				return null;
			}
		}

		if (!kv.TryGetValue("api-key", out var apiKey))
		{
			error = "The key 'api-key' is not found";
			return null;
		}

		if (!kv.TryGetValue("currency", out var currencyStr))
		{
			error = "The key 'currency' setting is not found";
			return null;
		}

		error = null;

		var tenantId = Lookup.ComputeTenantId(apiKey, currencyStr);

		var clientLookup = _serviceProvider.GetRequiredService<StrikeLightningClientLookup>();
		var existingClient = clientLookup.GetClient(tenantId);
		if (existingClient != null)
		{
			return existingClient;
		}

		var client = _serviceProvider.GetRequiredService<StrikeClient>();
		client.ApiKey = apiKey;
		client.Environment = environment;
		client.ThrowOnError = false;

		if (serverUrl != null)
			client.ServerUrl = serverUrl;

		var logger = _loggerFactory.CreateLogger<StrikeLightningClient>();

		var accountFiatCurrency = GetAccountFiatCurrency(client, ref error);
		if (accountFiatCurrency == null)
			return null;

		Currency targetOperatingCurrency;
		if ("fiat".Equals(currencyStr, StringComparison.OrdinalIgnoreCase))
		{
			targetOperatingCurrency = accountFiatCurrency.Value;
		}
		else if (!Enum.TryParse(currencyStr, true, out targetOperatingCurrency))
		{
			error = "The key 'currency' is invalid, set either 'BTC', 'FIAT' or 'USD'/'EUR'";
			return null;
		}

		// recompute tenantId with the real currency
		tenantId = Lookup.ComputeTenantId(apiKey, targetOperatingCurrency.ToString());

		var db = _serviceProvider.GetRequiredService<StrikeStorageFactory>();
		db.TenantId = tenantId;

		var lightningClient = new StrikeLightningClient(client, db, targetOperatingCurrency, network, logger);
		clientLookup.AddOrUpdateClient(tenantId, lightningClient);
		return lightningClient;
	}

	private static Currency? GetAccountFiatCurrency(StrikeClient client, ref string? error)
	{
		try
		{
			var balances = client.Balances.GetBalances().GetAwaiter().GetResult();
			if (!balances.IsSuccessStatusCode)
			{
				var errorFromServer = balances.Error?.Data;
				error = $"The connection failed, check api key. Error: {errorFromServer?.Code} {errorFromServer?.Message}";
				return null;
			}

			var accountFiatCurrency = balances.FirstOrDefault(x => x.Currency != Currency.Btc)?.Currency ?? Currency.Usd;
			return accountFiatCurrency;
		}
		catch (Exception e)
		{
			error = $"Invalid server or api key. Error: {e.Message}";
			return null;
		}
	}
}
