#nullable enable
using System;
using System.Linq;
using BTCPayServer.Lightning;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Strike.Client;
using Strike.Client.Models;
using Network = NBitcoin.Network;

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
        
        if (kv.TryGetValue("server", out var serverStr))
        {
	        if (!Uri.TryCreate(serverStr, UriKind.Absolute, out serverUrl)
	            || serverUrl.Scheme != "http" && serverUrl.Scheme != "https")
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

        var client = _serviceProvider.GetRequiredService<StrikeClient>();
        client.ThrowOnError = true;
        client.ApiKey = apiKey;
        client.Environment = environment;
        
        if(serverUrl != null)
			client.ServerUrl = serverUrl;

        var logger = _loggerFactory.CreateLogger<StrikeLightningClient>();
        Currency accountFiatCurrency;

        try
        {
            var balances = client.Balances.GetBalances().GetAwaiter().GetResult();
            if (!balances.IsSuccessStatusCode)
            {
                var errorFromServer = balances.Error?.Data;
                error = $"The connection failed, check api key. Error: {errorFromServer?.Code} {errorFromServer?.Message}";
                return null;
            }

            accountFiatCurrency = balances.FirstOrDefault(x => x.Currency != Currency.Btc)?.Currency ?? Currency.Usd;
        }
        catch (Exception e)
        {
            error = $"Invalid server or api key. Error: {e.Message}";
            return null;
        }

        Currency targetReceivingCurrency;
        if ("fiat".Equals(currencyStr, StringComparison.OrdinalIgnoreCase))
        {
            targetReceivingCurrency = accountFiatCurrency;
        }
        else if (!Enum.TryParse(currencyStr, true, out targetReceivingCurrency))
        {
            error = "The key 'currency' is invalid, set either 'BTC', 'FIAT' or 'USD'/'EUR'";
            return null;
        }


        return new StrikeLightningClient(client, accountFiatCurrency, targetReceivingCurrency, network, logger);
    }
}
