using System;
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
	private readonly EventAggregator _eventAggregator;

	public StrikeLightningConnectionStringHandler(IServiceProvider serviceProvider, ILoggerFactory loggerFactory, EventAggregator eventAggregator)
	{
		_serviceProvider = serviceProvider;
		_loggerFactory = loggerFactory;
		_eventAggregator = eventAggregator;
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
		
		if (!kv.TryGetValue("api-key", out var apiKey))
		{
			error = "The key 'api-key' is not found";
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

		var convertToCurrency = Currency.Undefined;
		if (kv.TryGetValue("convert-to", out var convertToCurrencyStr))
		{
			if (!Enum.TryParse(convertToCurrencyStr, true, out convertToCurrency))
			{
				error = "The key 'convert-to' is invalid, set either 'BTC', 'USD', 'EUR'";
				return null;
			}
		}
		
		error = null;
		
		// if we already have a client for this tenant, return it
		var tenantId = ComputeHash(connectionString.Trim().ToLowerInvariant());
		var holder = _serviceProvider.GetRequiredService<StrikeLightningClientFactory>();
		var existingClient = holder.GetClient(tenantId);
		if (existingClient != null)
		{
			return existingClient;
		}

		
		// initialize client
		var client = _serviceProvider.GetRequiredService<StrikeClient>();
		client.ApiKey = apiKey;
		client.Environment = environment;
		client.ThrowOnError = false;

		if (serverUrl != null)
			client.ServerUrl = serverUrl;

		// initialize lightning client that listens on top of StrikeClient
		var logger = _loggerFactory.CreateLogger<StrikeLightningClient>();
		var dbContextFactory = _serviceProvider.GetRequiredService<StrikeDbContextFactory>();
		
		holder.AddOrUpdateClient(tenantId, new StrikeLightningClient(client, dbContextFactory, network, logger, convertToCurrency, tenantId, _eventAggregator));
		return holder.GetClient(tenantId);
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
