using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BTCPayServer.Lightning;
using BTCPayServer.Plugins.Strike.Persistence;
using ExchangeSharp;
using NBitcoin;
using Newtonsoft.Json;
using Strike.Client.Models;
using Strike.Client.ReceiveRequests.Requests;
using Money = Strike.Client.Models.Money;

namespace BTCPayServer.Plugins.Strike;

public partial class StrikeLightningClient
{
	public async Task<LightningInvoice?> GetInvoice(string invoiceId, CancellationToken cancellation = new())
	{
		await using var storage = _db.ResolveStorage();
		var found = await storage.FindReceiveRequest(invoiceId);
		return ConvertReceiveRequest(found);
	}

	public async Task<LightningInvoice?> GetInvoice(uint256 paymentHash, CancellationToken cancellation = new())
	{
		await using var storage = _db.ResolveStorage();
		var found = await storage.FindReceiveRequestByPaymentHash(paymentHash.ToString());
		return ConvertReceiveRequest(found);
	}

	public async Task<LightningInvoice[]> ListInvoices(CancellationToken cancellation = new())
	{
		await using var storage = _db.ResolveStorage();
		var requests = await storage.GetReceiveRequests(false);
		return ConvertReceiveRequests(requests);
	}

	public async Task<LightningInvoice[]> ListInvoices(ListInvoicesParams? request, CancellationToken cancellation = new())
	{
		await using var storage = _db.ResolveStorage();
		var requests = await storage.GetReceiveRequests(request?.PendingOnly == true);
		return ConvertReceiveRequests(requests);
	}

	public async Task<LightningInvoice> CreateInvoice(LightMoney amount, string? description, TimeSpan expiry,
		CancellationToken cancellation = new())
	{
		return await CreateInvoice(amount, description, null, expiry);
	}

	public async Task<LightningInvoice> CreateInvoice(CreateInvoiceParams createInvoiceRequest, CancellationToken cancellation = new())
	{
		return await CreateInvoice(
			createInvoiceRequest.Amount,
			createInvoiceRequest.Description,
			createInvoiceRequest.DescriptionHash?.ToString(),
			createInvoiceRequest.Expiry
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

	private async Task<LightningInvoice> CreateInvoice(LightMoney amount, string? description, string? descriptionHash, TimeSpan expiry)
	{
		var btcAmount = amount.ToUnit(LightMoneyUnit.BTC);
		var requestAmount = new Money { Amount = btcAmount, Currency = Currency.Btc };
		var request = await _client.ReceiveRequests.Create(new ReceiveRequestReq
		{
			TargetCurrency = TargetCurrency,
			Bolt11 = new Bolt11ReceiveRequestReq
			{
				Amount = requestAmount,
				Description = ParseDescription(description),
				DescriptionHash = descriptionHash,
				ExpiryInSeconds = (ulong?)expiry.TotalSeconds
			}
		});
		ThrowOnError(request);

		var bolt11 = request.Bolt11 ?? throw new InvalidOperationException("Received null BOLT11 from Strike API");
		var receiveRequestId = request.ReceiveRequestId.ToString();

		var entity = new StrikeReceiveRequest
		{
			ReceiveRequestId = receiveRequestId,
			LightningInvoice = bolt11.Invoice,
			Description = bolt11.Description,
			CreatedAt = request.Created,
			ExpiresAt = bolt11.Expires,
			PaymentHash = bolt11.PaymentHash,
			RequestedBtcAmount = requestAmount.Amount,
			RealBtcAmount = bolt11.BtcAmount,
			TargetCurrency = request.TargetCurrency?.ToStringUpperInvariant() ?? TargetCurrency.ToStringUpperInvariant(),
		};
		await using var storage = _db.ResolveStorage();
		await storage.Store(entity);

		return new LightningInvoice
		{
			Id = receiveRequestId,
			BOLT11 = bolt11.Invoice,
			PaymentHash = bolt11.PaymentHash,
			ExpiresAt = bolt11.Expires,
			Amount = new LightMoney(bolt11.BtcAmount, LightMoneyUnit.BTC),
			Status = LightningInvoiceStatus.Unpaid
		};
	}

	private static string? ParseDescription(string? description)
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

	private static LightningInvoice[] ConvertReceiveRequests(StrikeReceiveRequest[] requests)
	{
		return requests
			.Select(ConvertReceiveRequest)
			.ToArray()!;
	}

	private static LightningInvoice? ConvertReceiveRequest(StrikeReceiveRequest? request)
	{
		if (request == null)
			return null;

		var status = TranslateStatus(request);
		var amount = new LightMoney(request.RealBtcAmount, LightMoneyUnit.BTC);

		return new LightningInvoice
		{
			Id = request.ReceiveRequestId,
			BOLT11 = request.LightningInvoice,
			PaymentHash = request.PaymentHash,
			ExpiresAt = request.ExpiresAt,
			PaidAt = request.PaidAt,
			Amount = amount,
			AmountReceived = request.Paid ? amount : null,
			Status = status
		};
	}

	private static LightningInvoiceStatus TranslateStatus(StrikeReceiveRequest request)
	{
		if (request.Paid)
			return LightningInvoiceStatus.Paid;

		return request.IsExpired ?
			LightningInvoiceStatus.Expired :
			LightningInvoiceStatus.Unpaid;
	}
}
