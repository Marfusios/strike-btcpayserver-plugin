using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BTCPayServer.Lightning;
using BTCPayServer.Plugins.Strike.Persistence;
using ExchangeSharp;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Strike.Client;
using Strike.Client.Errors;
using Strike.Client.Models;

namespace BTCPayServer.Plugins.Strike;

public partial class StrikeLightningClient : ILightningClient
{
	private readonly StrikeClient _client;
	public StrikeClient Client => _client;
	private readonly StrikeDbContextFactory _dbContextFactory;
	public StrikeDbContextFactory DbContextFactory => _dbContextFactory;
	private readonly Network _network;
	private readonly ILogger _logger;
	private readonly Currency _convertToCurrency;
	private readonly string _tenantId;
	private readonly EventAggregator _eventAggregator;

	public StrikeLightningClient(StrikeClient client, StrikeDbContextFactory dbContextFactory, 
		Network network, ILogger logger, Currency convertToCurrency, string tenantId, EventAggregator eventAggregator)
	{
		_client = client;
		_dbContextFactory = dbContextFactory;
		_network = network;
		_logger = logger;
		_convertToCurrency = convertToCurrency;
		_tenantId = tenantId;
		_eventAggregator = eventAggregator;
	}

	public override string ToString()
	{
		var convertToCurrency = _convertToCurrency.ToStringUpperInvariant();
		return _client.Environment == StrikeEnvironment.Custom ?
			$"type=strike;convert-to={convertToCurrency};server={_client.ServerUrl};api-key={_client.ApiKey}" :
			$"type=strike;convert-to={convertToCurrency};api-key={_client.ApiKey}";
	}

	public Task<LightningNodeInformation> GetInfo(CancellationToken cancellation = new())
	{
		throw new NotSupportedException();
	}

	public async Task<LightningNodeBalance> GetBalance(CancellationToken cancellation = new())
	{
		var rates = await _client.Rates.GetRatesTicker();
		ThrowOnError(rates);

		var balances = await _client.Balances.GetBalances();
		ThrowOnError(balances);

		var totalAvailableBtcBalance = 0m;

		foreach (var balance in balances)
		{
			if (balance.Currency == Currency.Btc)
			{
				totalAvailableBtcBalance += balance.Available;
				continue;
			}

			var foundRate = rates
				.FirstOrDefault(x => x.TargetCurrency == balance.Currency && x.SourceCurrency == Currency.Btc);
			if (foundRate is not { Amount: > 0 })
				continue;

			totalAvailableBtcBalance += Math.Round(balance.Available / foundRate.Amount, 8);
		}

		return new LightningNodeBalance(
			new OnchainBalance(),
			new OffchainBalance
			{
				Local = new LightMoney(totalAvailableBtcBalance, LightMoneyUnit.BTC)
			}
			);
	}

	public Task<OpenChannelResponse> OpenChannel(OpenChannelRequest openChannelRequest, CancellationToken cancellation = new())
	{
		throw new NotImplementedException();
	}

	public Task<ConnectionResult> ConnectTo(NodeInfo nodeInfo, CancellationToken cancellation = new())
	{
		throw new NotImplementedException();
	}

	public Task<LightningChannel[]> ListChannels(CancellationToken cancellation = new())
	{
		throw new NotImplementedException();
	}

	private void ThrowOnError(ResponseBase response)
	{
		if (!response.IsSuccessStatusCode)
		{
			var error = response.Error?.Data;
			throw new StrikeApiException(
			$"API error, status: {response.StatusCode}, error: {error?.Code} {error?.Message}");
		}

	}
}
