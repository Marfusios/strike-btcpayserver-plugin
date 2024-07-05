using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using BTCPayServer.Lightning;
using BTCPayServer.Plugins.Strike.Persistence;
using ExchangeSharp;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Newtonsoft.Json;
using Strike.Client.Invoices;
using Strike.Client.Models;
using Money = Strike.Client.Models.Money;

namespace BTCPayServer.Plugins.Strike;

public partial class StrikeLightningClient
{
	public async Task<LightningInvoice?> GetInvoice(string invoiceId, CancellationToken cancellation = new())
	{
		var invoice = await _client.Invoices.FindInvoice(Guid.Parse(invoiceId));
		if (invoice.StatusCode == HttpStatusCode.NotFound)
			return null;
		ThrowOnError(invoice);
		return await ConvertInvoice(invoice);
	}

	public async Task<LightningInvoice?> GetInvoice(uint256 paymentHash, CancellationToken cancellation = new())
	{
		var found = await _db.ResolveStorage().FindQuoteByPaymentHash(paymentHash.ToString());
		if (found == null)
		{
			return null;
		}
		return await GetInvoice(found.InvoiceId, cancellation);
	}

	public async Task<LightningInvoice[]> ListInvoices(CancellationToken cancellation = new())
	{
		var invoices = await _client.Invoices.GetInvoices();
		ThrowOnError(invoices);
		return await ConvertInvoices(invoices);
	}

	public async Task<LightningInvoice[]> ListInvoices(ListInvoicesParams? request, CancellationToken cancellation = new())
	{
		var invoices = await _client.Invoices.GetInvoices(100, (int)(request?.OffsetIndex ?? 0));
		ThrowOnError(invoices);

		return (await ConvertInvoices(invoices))
			.Where(x => request?.PendingOnly == true && x.Status == LightningInvoiceStatus.Unpaid)
			.ToArray()!;
	}

	public async Task<LightningInvoice> CreateInvoice(LightMoney amount, string? description, TimeSpan expiry,
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

	public Task CancelInvoice(string invoiceId, CancellationToken cancellation = new())
	{
		throw new NotImplementedException();
	}

	public Task<BitcoinAddress> GetDepositAddress(CancellationToken cancellation = new())
	{
		throw new NotImplementedException();
	}

	private async Task<LightningInvoice> CreateInvoice(LightMoney amount, string? description, string? descriptionHash)
	{
		var invoiceAmount = await CalculateInvoiceAmount(amount);
		var invoice = await _client.Invoices.IssueInvoice(new InvoiceReq
		{
			Amount = invoiceAmount,
			Description = ParseDescription(description)
		});
		ThrowOnError(invoice);

		var quote = await _client.Invoices.IssueQuote(invoice.InvoiceId, new InvoiceQuoteReq
		{
			DescriptionHash = descriptionHash
		});
		ThrowOnError(quote);

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

	private string? ParseDescription(string? description)
	{
		if (description == null)
			return null;

		try
		{
			var parsedDict = JsonConvert.DeserializeObject<string[][]>(description);
			if (parsedDict == null)
				return description;

			var info = FindValue(parsedDict, "plain") ?? FindValue(parsedDict, "description");
			var identifier = FindValue(parsedDict, "identifier");

			var hasInfo = !string.IsNullOrWhiteSpace(info);
			var hasIdentifier = !string.IsNullOrWhiteSpace(identifier);

			return hasInfo switch
			{
				false when !hasIdentifier => description,
				false when hasIdentifier => $"Payment to {identifier}",
				true when !hasIdentifier => info,
				_ => $"{info} ({identifier})"
			};
		}
		catch
		{
			// ignore
		}

		return description;
	}

	private static string? FindValue(string[][] dict, string searchWord) =>
		dict.FirstOrDefault(x => x.Length > 1 && x[0].Contains(searchWord, StringComparison.OrdinalIgnoreCase))?[1];

	private async Task<Money> CalculateInvoiceAmount(LightMoney amount)
	{
		var btcAmount = amount.ToUnit(LightMoneyUnit.BTC);
		if (_targetOperatingCurrency == Currency.Btc)
			return new Money { Amount = btcAmount, Currency = Currency.Btc };

		var rates = await _client.Rates.GetRatesTicker();
		var foundRate = rates.FirstOrDefault(x =>
			x.TargetCurrency == _targetOperatingCurrency
			&& x.SourceCurrency == Currency.Btc);
		if (foundRate is not { Amount: > 0 })
			throw new InvalidOperationException(
				$"Cannot calculate invoice amount, rate for BTC/{_targetOperatingCurrency.ToStringUpperInvariant()} is unavailable");
		return new Money { Amount = btcAmount * foundRate.Amount, Currency = _targetOperatingCurrency };
	}

	private async Task<LightningInvoice?> ConvertInvoice(Invoice invoice)
	{
		try
		{
			var storage = _db.ResolveStorage();
			var invoiceId = invoice.InvoiceId.ToString();
			var quote = await storage.FindQuoteByInvoiceId(invoiceId);
			if (quote == null)
				return null;

			var converted = ConvertInvoice(invoice, quote);
			if (converted == null)
				return null;

			var status = TranslateStatus(invoice.State, quote.ExpiresAt);
			if (status == LightningInvoiceStatus.Paid && !quote.Paid)
			{
				quote.Paid = true;
				await storage.Store(quote);
			}

			return converted;
		}
		catch (Exception e)
		{
			_logger.LogWarning(e, "Failed to convert invoice {invoiceId}, error: {errorMessage}", invoice?.InvoiceId, e.Message);
			return null;
		}
	}

	private LightningInvoice? ConvertInvoice(Invoice invoice, StrikeQuote quote)
	{
		try
		{
			var invoiceId = invoice.InvoiceId.ToString();
			var parsedInvoice = BOLT11PaymentRequest.Parse(quote.LightningInvoice, _network);
			var status = TranslateStatus(invoice.State, quote.ExpiresAt);

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
}
