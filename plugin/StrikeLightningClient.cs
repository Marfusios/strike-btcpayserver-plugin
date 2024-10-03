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
	private readonly ILogger _logger;
	private readonly StrikeClient _client;
	private readonly Network _network;
	private readonly StrikeStorageFactory _db;

	public StrikeLightningClient(StrikeClient client, StrikeStorageFactory db, Currency targetOperatingCurrency,
		Network network, ILogger logger)
	{
		_logger = logger;
		_db = db;
		_network = network;
		TargetCurrency = targetOperatingCurrency;
		_client = client;
	}

	public Currency TargetCurrency { get; }

	public StrikeClient Client => _client;

	public override string ToString()
	{
		var currency = TargetCurrency.ToStringUpperInvariant();
		return _client.Environment == StrikeEnvironment.Custom ?
			$"type=strike;currency={currency};server={_client.ServerUrl};api-key={_client.ApiKey}" :
			$"type=strike;currency={currency};api-key={_client.ApiKey}";
	}

	public Task<LightningNodeInformation> GetInfo(CancellationToken cancellation = new())
	{
		throw new NotSupportedException();
	}

	public async Task<LightningNodeBalance> GetBalance(CancellationToken cancellation = new())
	{
		var balances = await _client.Balances.GetBalances();
		if (!balances.IsSuccessStatusCode)
			return BalanceResult(0);

		var balance = balances.FirstOrDefault(x => x.Currency == TargetCurrency)?.Available ?? 0;
		if (TargetCurrency == Currency.Btc)
			return BalanceResult(balance);


		var rates = await _client.Rates.GetRatesTicker();
		if (!rates.IsSuccessStatusCode)
			return BalanceResult(0);

		var foundRate = rates
			.FirstOrDefault(x => x.TargetCurrency == TargetCurrency && x.SourceCurrency == Currency.Btc);
		if (foundRate is not { Amount: > 0 })
			return BalanceResult(0);

		return BalanceResult(Math.Round(balance / foundRate.Amount, 8));

		static LightningNodeBalance BalanceResult(decimal balance1) =>
			new(
				new OnchainBalance(),
				new OffchainBalance { Local = new LightMoney(balance1, LightMoneyUnit.BTC) }
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
		if (response.IsSuccessStatusCode)
			return;
		var error = response.Error?.Data;
		throw new StrikeApiException(
			$"API error, status: {response.StatusCode}, error: {error?.Code} {error?.Message}");

	}
}
