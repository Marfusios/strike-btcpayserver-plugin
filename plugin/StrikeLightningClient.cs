using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BTCPayServer.Lightning;
using BTCPayServer.Plugins.Strike.Persistence;
using ExchangeSharp;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Strike.Client;
using Strike.Client.Invoices;
using Strike.Client.Models;
using Money = Strike.Client.Models.Money;

namespace BTCPayServer.Plugins.Strike;

public class StrikeLightningClient : ILightningClient
{
	private readonly ILogger _logger;
	private readonly StrikeClient _client;
	private readonly Currency _accountFiatCurrency;
	private readonly Currency _targetReceiveCurrency;
	private readonly Network _network;
	private readonly StrikeStorageFactory _db;

	public StrikeLightningClient(StrikeClient client, StrikeStorageFactory db, Currency accountFiatCurrency, Currency targetReceiveCurrency,
		Network network, ILogger logger)
	{
		_logger = logger;
		_db = db;
		_network = network;
		_targetReceiveCurrency = targetReceiveCurrency;
		_accountFiatCurrency = accountFiatCurrency;
		_client = client;
	}

	public override string ToString()
	{
		var currency = _targetReceiveCurrency.ToStringUpperInvariant();
		return _client.Environment == StrikeEnvironment.Custom ?
			$"type=strike;currency={currency};server={_client.ServerUrl};api-key={_client.ApiKey}" :
			$"type=strike;currency={currency};api-key={_client.ApiKey}";
	}

	public async Task<LightningInvoice?> GetInvoice(string invoiceId, CancellationToken cancellation = new())
	{
		var invoice = await _client.Invoices.FindInvoice(Guid.Parse(invoiceId));
		return await ConvertInvoice(invoice);
	}

	public async Task<LightningInvoice?> GetInvoice(uint256 paymentHash, CancellationToken cancellation = new())
	{
		var found = await _db.ResolveStorage().FindByPaymentHash(paymentHash.ToString());
		if (found == null)
		{
			return null;
		}
		return await GetInvoice(found.InvoiceId, cancellation);
	}

	public async Task<LightningInvoice[]> ListInvoices(CancellationToken cancellation = new())
	{
		var invoices = await _client.Invoices.GetInvoices();
		return await ConvertInvoices(invoices);
	}

	public async Task<LightningInvoice[]> ListInvoices(ListInvoicesParams? request, CancellationToken cancellation = new())
	{
		var invoices = await _client.Invoices.GetInvoices(100, (int)(request?.OffsetIndex ?? 0));

		return (await ConvertInvoices(invoices))
			.Where(x => request?.PendingOnly == true && x.Status == LightningInvoiceStatus.Unpaid)
			.ToArray()!;
	}

	public Task<LightningPayment> GetPayment(string paymentHash, CancellationToken cancellation = new())
	{
		throw new NotImplementedException();
	}

	public Task<LightningPayment[]> ListPayments(CancellationToken cancellation = new())
	{
		throw new NotImplementedException();
	}

	public Task<LightningPayment[]> ListPayments(ListPaymentsParams request, CancellationToken cancellation = new())
	{
		throw new NotImplementedException();
	}

	public async Task<LightningInvoice> CreateInvoice(LightMoney amount, string description, TimeSpan expiry,
		CancellationToken cancellation = new())
	{
		return await CreateInvoice(amount, description, null);
	}

	public async Task<LightningInvoice> CreateInvoice(CreateInvoiceParams createInvoiceRequest, CancellationToken cancellation = new())
	{
		return await CreateInvoice(
			createInvoiceRequest.Amount,
			createInvoiceRequest.Description,
			createInvoiceRequest.DescriptionHash?.ToString()
			);
	}

	public Task<ILightningInvoiceListener> Listen(CancellationToken cancellation = new())
	{
		return Task.FromResult<ILightningInvoiceListener>(new Listener(this));
	}

	public Task<LightningNodeInformation> GetInfo(CancellationToken cancellation = new())
	{
		throw new NotSupportedException();
	}

	public async Task<LightningNodeBalance> GetBalance(CancellationToken cancellation = new())
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

	public Task<PayResponse> Pay(PayInvoiceParams payParams, CancellationToken cancellation = new())
	{
		throw new NotImplementedException();
	}

	public Task<PayResponse> Pay(string bolt11, PayInvoiceParams payParams, CancellationToken cancellation = new())
	{
		throw new NotImplementedException();
	}

	public Task<PayResponse> Pay(string bolt11, CancellationToken cancellation = new())
	{
		throw new NotImplementedException();
	}

	public Task<OpenChannelResponse> OpenChannel(OpenChannelRequest openChannelRequest, CancellationToken cancellation = new())
	{
		throw new NotImplementedException();
	}

	public Task<BitcoinAddress> GetDepositAddress(CancellationToken cancellation = new())
	{
		throw new NotImplementedException();
	}

	public Task<ConnectionResult> ConnectTo(NodeInfo nodeInfo, CancellationToken cancellation = new())
	{
		throw new NotImplementedException();
	}

	public Task CancelInvoice(string invoiceId, CancellationToken cancellation = new())
	{
		throw new NotImplementedException();
	}

	public Task<LightningChannel[]> ListChannels(CancellationToken cancellation = new())
	{
		throw new NotImplementedException();
	}

	private async Task<LightningInvoice> CreateInvoice(LightMoney amount, string description, string? descriptionHash)
	{
		var invoiceAmount = await CalculateInvoiceAmount(amount);
		var invoice = await _client.Invoices.IssueInvoice(new InvoiceReq
		{
			Amount = invoiceAmount,
			Description = description
		});

		var quote = await _client.Invoices.IssueQuote(invoice.InvoiceId, new InvoiceQuoteReq
		{
			DescriptionHash = descriptionHash
		});

		var invoiceId = invoice.InvoiceId.ToString();
		var parsedInvoice = BOLT11PaymentRequest.Parse(quote.LnInvoice, _network);

		var entity = new StrikeQuote
		{
			InvoiceId = invoiceId,
			LightningInvoice = quote.LnInvoice,
			Description = invoice.Description,
			CreatedAt = DateTimeOffset.UtcNow,
			ExpiresAt = parsedInvoice.ExpiryDate,
			PaymentHash = parsedInvoice.PaymentHash?.ToString() ?? string.Empty,
			RequestedBtcAmount = amount.ToUnit(LightMoneyUnit.BTC),
			RealBtcAmount = parsedInvoice.MinimumAmount.ToUnit(LightMoneyUnit.BTC),
			TargetAmount = invoice.Amount.Amount,
			TargetCurrency = invoice.Amount.Currency.ToStringUpperInvariant(),
			ConversionRate = quote.ConversionRate.Amount
		};
		await _db.ResolveStorage().Store(entity);

		return new LightningInvoice
		{
			Id = invoiceId,
			BOLT11 = quote.LnInvoice,
			PaymentHash = parsedInvoice.PaymentHash?.ToString(),
			ExpiresAt = parsedInvoice.ExpiryDate,
			Amount = parsedInvoice.MinimumAmount,
			Status = TranslateStatus(invoice.State, parsedInvoice.ExpiryDate)
		};
	}

	private async Task<Money> CalculateInvoiceAmount(LightMoney amount)
	{
		var btcAmount = amount.ToUnit(LightMoneyUnit.BTC);
		if (_targetReceiveCurrency == Currency.Btc)
			return new Money { Amount = btcAmount, Currency = Currency.Btc };

		var rates = await _client.Rates.GetRatesTicker();
		var foundRate = rates.FirstOrDefault(x =>
			x.TargetCurrency == _targetReceiveCurrency
			&& x.SourceCurrency == Currency.Btc);
		if (foundRate is not { Amount: > 0 })
			throw new InvalidOperationException(
				$"Cannot calculate invoice amount, rate for BTC/{_targetReceiveCurrency.ToStringUpperInvariant()} is unavailable");
		return new Money { Amount = btcAmount * foundRate.Amount, Currency = _targetReceiveCurrency };
	}

	private async Task<LightningInvoice?> ConvertInvoice(Invoice invoice)
	{
		try
		{
			var storage = _db.ResolveStorage();
			var invoiceId = invoice.InvoiceId.ToString();
			var quote = await storage.FindByInvoiceId(invoiceId);
			if (quote == null)
				return null;

			var parsedInvoice = BOLT11PaymentRequest.Parse(quote.LightningInvoice, _network);
			var status = TranslateStatus(invoice.State, quote.ExpiresAt);

			if (status == LightningInvoiceStatus.Paid && !quote.Paid)
			{
				quote.Paid = true;
				await storage.Store(quote);
			}

			return new LightningInvoice
			{
				Id = invoiceId,
				BOLT11 = quote.LightningInvoice,
				PaymentHash = parsedInvoice.PaymentHash?.ToString(),
				ExpiresAt = parsedInvoice.ExpiryDate,
				Amount = parsedInvoice.MinimumAmount,
				AmountReceived = status == LightningInvoiceStatus.Paid ? parsedInvoice.MinimumAmount : null,
				PaidAt = status == LightningInvoiceStatus.Paid ? DateTimeOffset.UtcNow : null,
				Status = status
			};
		}
		catch (Exception e)
		{
			_logger.LogWarning(e, "Failed to convert invoice {invoiceId}, error: {errorMessage}", invoice?.InvoiceId, e.Message);
			return null;
		}
	}

	private async Task<LightningInvoice[]> ConvertInvoices(InvoicesCollection invoices)
	{
		var result = new List<LightningInvoice>();
		foreach (var invoice in invoices.Items)
		{
			var converted = await ConvertInvoice(invoice);
			if (converted == null)
				continue;
			result.Add(converted);
		}
		return result.ToArray();
	}

	private LightningInvoiceStatus TranslateStatus(InvoiceState state, DateTimeOffset expiration)
	{
		if (state == InvoiceState.Paid)
			return LightningInvoiceStatus.Paid;

		var now = DateTimeOffset.UtcNow;
		if (now > expiration)
			return LightningInvoiceStatus.Expired;

		return state switch
		{
			InvoiceState.Unpaid => LightningInvoiceStatus.Unpaid,
			InvoiceState.Pending => LightningInvoiceStatus.Unpaid,
			_ => LightningInvoiceStatus.Expired
		};
	}

	private class Listener : ILightningInvoiceListener
	{
		private readonly StrikeLightningClient _client;
		public Listener(StrikeLightningClient client)
		{
			_client = client;
		}

		public void Dispose()
		{
		}

		public async Task<LightningInvoice> WaitInvoice(CancellationToken cancellation)
		{
			var storage = _client._db.ResolveStorage();
			var invoices = await storage.GetUnobserved(cancellation);

			foreach (var invoice in invoices)
			{
				var found = await _client.GetInvoice(invoice.InvoiceId, cancellation);
				if (found == null)
					continue;

				if (found.Status == LightningInvoiceStatus.Unpaid)
					continue;


				invoice.Paid = found.Status == LightningInvoiceStatus.Paid;
				invoice.Observed = true;
				await storage.Store(invoice);

				return found;
			}

			await Task.Delay(TimeSpan.FromSeconds(2), cancellation);

			// BTCPayServer expects a non-null invoice to be returned
			return new LightningInvoice
			{
				Id = string.Empty
			};
		}
	}
}
