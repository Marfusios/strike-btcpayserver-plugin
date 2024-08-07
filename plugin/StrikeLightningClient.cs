﻿using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BTCPayServer.Lightning;
using BTCPayServer.Plugins.Strike.Persistence;
using ExchangeSharp;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Strike.Client;
using Strike.Client.CurrencyExchanges;
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

	public async Task<bool> ConvertAmount(Currency from, Currency to, decimal amountFrom, Guid idempotencyKey)
	{
		var req = new CurrencyExchangeQuoteReq
		{
			Sell = from,
			Buy = to,
			Amount = new MoneyWithFee
			{
				Currency = from,
				Amount = amountFrom,
				FeePolicy = FeePolicy.Exclusive
			},
			IdempotencyKey = idempotencyKey
		};
		var quote = await _client.CurrencyExchanges.CreateQuote(req);
		if (quote.Error?.Data.Code == "DUPLICATE_CURRENCY_EXCHANGE_QUOTE")
			return true;
		ThrowOnError(quote);
		var response = await _client.CurrencyExchanges.ExecuteQuote(quote.Id);
		return response.IsSuccessStatusCode;
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
