﻿using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using BTCPayServer.Lightning;
using BTCPayServer.Plugins.Strike.Persistence;
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
	
	// TODO: There has to be better way to fetch the reference to StrikeClient for StrikePluginHostedService
	private StrikeLightningClient _latest;
	public StrikeLightningClient Latest => _latest;

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

		var convertToCurrency = Currency.Undefined;
		if (kv.TryGetValue("convertto", out var convertToCurrencyStr))
		{
			if (!Enum.TryParse(convertToCurrencyStr, true, out convertToCurrency))
			{
				error = "The key 'convertTo' is invalid, set either 'BTC', 'USD', 'EUR'";
				return null;
			}
		}

		error = null;

		// TODO: use StoreId instead (but how to get it?)
		var tenantId = ComputeHash(apiKey);

		var db = _serviceProvider.GetRequiredService<StrikeStorageFactory>();
		db.TenantId = tenantId;

		var client = _serviceProvider.GetRequiredService<StrikeClient>();
		client.ApiKey = apiKey;
		client.Environment = environment;
		client.ThrowOnError = false;

		if (serverUrl != null)
			client.ServerUrl = serverUrl;

		var logger = _loggerFactory.CreateLogger<StrikeLightningClient>();

		_latest = new StrikeLightningClient(client, db,
			network, logger, convertToCurrency);
		return _latest;
	}

	private static string ComputeHash(string value)
	{
		var sb = new StringBuilder();
		using (var hash = SHA256.Create())
		{
			var enc = Encoding.UTF8;
			var result = hash.ComputeHash(enc.GetBytes(value));

			foreach (var b in result)
				sb.Append(b.ToString("x2"));
		}

		return sb.ToString();
	}
}
