#nullable enable
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BTCPayServer.Lightning;
using ExchangeSharp;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Strike.Client;
using Strike.Client.Models;

namespace BTCPayServer.Plugins.Strike;

public class StrikeLightningClient : ILightningClient
{
	private readonly ILogger _logger;
	private readonly StrikeClient _client;
	private readonly Currency _accountFiatCurrency;
	private readonly Currency _targetReceiveCurrency;

	public StrikeLightningClient(StrikeClient client, Currency accountFiatCurrency, Currency targetReceiveCurrency, ILogger logger)
	{
		_logger = logger;
		_targetReceiveCurrency = targetReceiveCurrency;
		_accountFiatCurrency = accountFiatCurrency;
		_client = client;
	}

	public override string ToString()
	{
		return $"type=strike;environment={_client.Environment.ToStringLowerInvariant()};currency={_targetReceiveCurrency.ToStringUpperInvariant()};api-key={_client.ApiKey}";
	}

	public Task<LightningInvoice> GetInvoice(string invoiceId, CancellationToken cancellation = new CancellationToken())
	{
		throw new NotImplementedException();
	}

	public Task<LightningInvoice> GetInvoice(uint256 paymentHash, CancellationToken cancellation = new CancellationToken())
	{
		throw new NotImplementedException();
	}

	public Task<LightningInvoice[]> ListInvoices(CancellationToken cancellation = new CancellationToken())
	{
		throw new NotImplementedException();
	}

	public Task<LightningInvoice[]> ListInvoices(ListInvoicesParams request, CancellationToken cancellation = new CancellationToken())
	{
		throw new NotImplementedException();
	}

	public Task<LightningPayment> GetPayment(string paymentHash, CancellationToken cancellation = new CancellationToken())
	{
		throw new NotImplementedException();
	}

	public Task<LightningPayment[]> ListPayments(CancellationToken cancellation = new CancellationToken())
	{
		throw new NotImplementedException();
	}

	public Task<LightningPayment[]> ListPayments(ListPaymentsParams request, CancellationToken cancellation = new CancellationToken())
	{
		throw new NotImplementedException();
	}

	public Task<LightningInvoice> CreateInvoice(LightMoney amount, string description, TimeSpan expiry,
		CancellationToken cancellation = new CancellationToken())
	{
		throw new NotImplementedException();
	}

	public Task<LightningInvoice> CreateInvoice(CreateInvoiceParams createInvoiceRequest, CancellationToken cancellation = new CancellationToken())
	{
		throw new NotImplementedException();
	}

	public Task<ILightningInvoiceListener> Listen(CancellationToken cancellation = new CancellationToken())
	{
		throw new NotImplementedException();
	}

	public Task<LightningNodeInformation> GetInfo(CancellationToken cancellation = new CancellationToken())
	{
		throw new NotSupportedException();
	}

	public async Task<LightningNodeBalance> GetBalance(CancellationToken cancellation = new CancellationToken())
	{
		var rates = await _client.Rates.GetRatesTicker();
		var balances = await _client.Balances.GetBalances();
		var totalAvailableBtcBalance = 0m;

		foreach (var balance in balances)
		{
			if (balance.Currency == Currency.Btc)
			{
				totalAvailableBtcBalance += balance.Available;
				continue;
			}

			var foundRate = rates.FirstOrDefault(x => x.TargetCurrency == balance.Currency && x.SourceCurrency == Currency.Btc);
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

	public Task<PayResponse> Pay(PayInvoiceParams payParams, CancellationToken cancellation = new CancellationToken())
	{
		throw new NotImplementedException();
	}

	public Task<PayResponse> Pay(string bolt11, PayInvoiceParams payParams, CancellationToken cancellation = new CancellationToken())
	{
		throw new NotImplementedException();
	}

	public Task<PayResponse> Pay(string bolt11, CancellationToken cancellation = new CancellationToken())
	{
		throw new NotImplementedException();
	}

	public Task<OpenChannelResponse> OpenChannel(OpenChannelRequest openChannelRequest, CancellationToken cancellation = new CancellationToken())
	{
		throw new NotImplementedException();
	}

	public Task<BitcoinAddress> GetDepositAddress(CancellationToken cancellation = new CancellationToken())
	{
		throw new NotImplementedException();
	}

	public Task<ConnectionResult> ConnectTo(NodeInfo nodeInfo, CancellationToken cancellation = new CancellationToken())
	{
		throw new NotImplementedException();
	}

	public Task CancelInvoice(string invoiceId, CancellationToken cancellation = new CancellationToken())
	{
		throw new NotImplementedException();
	}

	public Task<LightningChannel[]> ListChannels(CancellationToken cancellation = new CancellationToken())
	{
		throw new NotImplementedException();
	}
}
