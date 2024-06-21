#nullable enable
using System;
using System.Linq;
using BTCPayServer.Lightning;
using ExchangeSharp;
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

        if (!kv.TryGetValue("environment", out var environmentStr))
        {
            environmentStr = network.Name switch
            {
                nameof(Network.Main) => StrikeEnvironment.Live.ToStringLowerInvariant(),
                _ => StrikeEnvironment.Development.ToStringLowerInvariant()
            };
        }

        if (!Enum.TryParse<StrikeEnvironment>(environmentStr, true, out var environment))
        {
            error = "The key 'environment' is not in correct format, try 'live' or 'development'";
            return null;
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
        client.ApiKey = apiKey;
        client.Environment = environment;

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
            error = "Invalid server or api key";
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


        return new StrikeLightningClient(client, accountFiatCurrency, targetReceivingCurrency, logger);
    }
}
