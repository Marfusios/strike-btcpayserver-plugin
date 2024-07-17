using System;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using BTCPayServer.Lightning;
using BTCPayServer.Plugins.Strike.Persistence;
using ExchangeSharp;
using Microsoft.EntityFrameworkCore;
using Strike.Client;
using Strike.Client.Errors;
using Strike.Client.Models;
using Strike.Client.PaymentQuotes.Lightning;
using Strike.Client.Payments;

namespace BTCPayServer.Plugins.Strike;

public partial class StrikeLightningClient
{
	public async Task<LightningPayment?> GetPayment(string paymentHash, CancellationToken cancellation = new())
	{
		await using var db = _dbContextFactory.CreateContext();
		var found = await db.Payments
			.FirstOrDefaultAsync(x => x.TenantId == _tenantId && x.PaymentHash == paymentHash, cancellationToken: cancellation);
		if (found == null)
			return null;

		var payment = await _client.Payments.FindPayment(Guid.Parse(found.PaymentId));
		if (payment.StatusCode == HttpStatusCode.NotFound)
			return null;

		var status = TranslateLightningPayStatus(payment.State);
		var realLnAmount = new LightMoney(found.RequestedBtcAmount, LightMoneyUnit.BTC);
		var realLnFee = ConvertAmount(payment.LightningNetworkFee);

		if (found.Status != status)
		{
			found.Status = status;
			found.CompletedAt = payment.Completed;
			found.RealBtcFeeAmount = realLnFee.ToUnit(LightMoneyUnit.BTC);
			await db.SaveChangesAsync(cancellation);
		}

		return new LightningPayment
		{
			PaymentHash = found.PaymentHash,
			BOLT11 = found.LightningInvoice,
			Amount = realLnAmount,
			AmountSent = status == LightningPaymentStatus.Complete ? realLnAmount : LightMoney.Zero,
			Fee = realLnFee,
			CreatedAt = found.CompletedAt ?? found.CreatedAt,
			Status = status
		};
	}

	public Task<LightningPayment[]> ListPayments(CancellationToken cancellation = new())
	{
		return ListPayments(null, cancellation);
	}

	public async Task<LightningPayment[]> ListPayments(ListPaymentsParams? request, CancellationToken cancellation = new())
	{
		await using var db = _dbContextFactory.CreateContext();
		var payments = await GetPayments(db, request?.IncludePending == false, (int?)request?.OffsetIndex ?? 0);
		return payments
			.Select(x => new LightningPayment
			{
				PaymentHash = x.PaymentHash,
				BOLT11 = x.LightningInvoice,
				Amount = new LightMoney(x.RequestedBtcAmount, LightMoneyUnit.BTC),
				AmountSent = x.Status == LightningPaymentStatus.Complete ? new LightMoney(x.RequestedBtcAmount, LightMoneyUnit.BTC) : LightMoney.Zero,
				Fee = new LightMoney(x.RealBtcFeeAmount, LightMoneyUnit.BTC),
				CreatedAt = x.CompletedAt ?? x.CreatedAt,
				Status = x.Status
			})
			.ToArray();
	}
	
	

	public async Task<StrikePayment[]> GetPayments(StrikeDbContext db, bool onlyCompleted, int offset = 0)
	{
		return await db.Payments
			.Where(x => x.TenantId == _tenantId)
			.Where(x => onlyCompleted && x.CompletedAt != null)
			.OrderByDescending(x => x.CreatedAt)
			.Skip(offset)
			.ToArrayAsync();
	}

	public Task<PayResponse> Pay(PayInvoiceParams payParams, CancellationToken cancellation = new())
	{
		throw new NotImplementedException();
	}

	public async Task<PayResponse> Pay(string bolt11, PayInvoiceParams? payParams, CancellationToken cancellation = new())
	{
		var parsedInvoice = BOLT11PaymentRequest.Parse(bolt11, _network);
		var sendingAmount = parsedInvoice.MinimumAmount;
		var hasAmount = sendingAmount > 0;
		var requestedAmount = hasAmount ? sendingAmount : payParams?.Amount;

		var paymentQuote = await _client.PaymentQuotes.CreateLnQuote(new LnPaymentQuoteReq
		{
			LnInvoice = bolt11,
			SourceCurrency = Currency.Btc,
			Amount = !hasAmount ? new MoneyWithFee
			{
				Currency = Currency.Btc,
				Amount = requestedAmount?.ToUnit(LightMoneyUnit.BTC) ?? throw new InvalidOperationException("Cannot send payment, please specify amount for amount-less LN invoices")
			} : null
		});

		if (!paymentQuote.IsSuccessStatusCode)
			return GetErrorPayResponse(paymentQuote);

		var payment = await _client.PaymentQuotes.ExecuteQuote(paymentQuote.PaymentQuoteId);
		if (!payment.IsSuccessStatusCode)
			return GetErrorPayResponse(payment);

		var realLnFee = ConvertAmount(payment.LightningNetworkFee);
		var realStatus = TranslateLightningPayStatus(payment.State);

		var entity = new StrikePayment
		{
			PaymentId = payment.PaymentId.ToString(),
			LightningInvoice = bolt11,
			CreatedAt = DateTimeOffset.UtcNow,
			CompletedAt = payment.Completed,
			PaymentHash = parsedInvoice.PaymentHash?.ToString() ?? string.Empty,
			RequestedBtcAmount = requestedAmount?.ToUnit(LightMoneyUnit.BTC) ?? 0,
			TargetAmount = payment.Amount.Amount,
			TargetCurrency = payment.Amount.Currency.ToStringUpperInvariant(),
			RealBtcFeeAmount = realLnFee.ToUnit(LightMoneyUnit.BTC),
			FeeAmount = payment.LightningNetworkFee?.Amount,
			FeeCurrency = payment.LightningNetworkFee?.Currency.ToStringUpperInvariant(),
			ConversionRate = payment.ConversionRate?.Amount,
			Status = realStatus
		};
		
		await using var db = _dbContextFactory.CreateContext();
		db.Payments.Add(entity);
		await db.SaveChangesAsync(cancellation);

		return new PayResponse
		{
			Result = TranslatePayStatus(payment.State),
			Details = new PayDetails
			{
				Status = realStatus,
				PaymentHash = parsedInvoice.PaymentHash,
				FeeAmount = realLnFee,
				TotalAmount = requestedAmount + realLnFee
			}
		};
	}

	public Task<PayResponse> Pay(string bolt11, CancellationToken cancellation = new())
	{
		return Pay(bolt11, null, cancellation);
	}

	private LightMoney ConvertAmount(Money? amount)
	{
		if (amount == null)
			return LightMoney.Zero;

		if (amount.Currency == Currency.Btc)
			return new LightMoney(amount.Amount, LightMoneyUnit.BTC);

		// todo: convert to fiat
		return LightMoney.Zero;
	}

	private PayResponse GetErrorPayResponse(ResponseBase response)
	{
		return new PayResponse
		{
			Result = PayResult.Error,
			ErrorDetail = FormatError(response.StatusCode, response.Error)
		};
	}

	private string FormatError(HttpStatusCode status, StrikeError? error)
	{
		return $"HTTP: {(int)status}, code: {error?.Data?.Code} {error?.Data?.Message}";
	}

	private PayResult TranslatePayStatus(PaymentState state) =>
		state switch
		{
			PaymentState.Completed => PayResult.Ok,
			PaymentState.Pending => PayResult.Unknown,
			PaymentState.Failed => PayResult.Error,
			_ => PayResult.Unknown
		};

	private LightningPaymentStatus TranslateLightningPayStatus(PaymentState state) =>
		state switch
		{
			PaymentState.Completed => LightningPaymentStatus.Complete,
			PaymentState.Pending => LightningPaymentStatus.Pending,
			PaymentState.Failed => LightningPaymentStatus.Failed,
			_ => LightningPaymentStatus.Unknown
		};
}
