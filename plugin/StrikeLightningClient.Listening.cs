using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BTCPayServer.Lightning;
using BTCPayServer.Plugins.Strike.Persistence;
using ExchangeSharp;
using Microsoft.Extensions.Logging;
using NBXplorer;
using Strike.Client.Models;
using Strike.Client.ReceiveRequests;

namespace BTCPayServer.Plugins.Strike;

public partial class StrikeLightningClient
{
	public Task<ILightningInvoiceListener> Listen(CancellationToken cancellation = new())
	{
		return Task.FromResult<ILightningInvoiceListener>(new Listener(this));
	}

	private class Listener : ILightningInvoiceListener
	{
		private readonly StrikeLightningClient _client;
		private readonly List<LightningInvoice> _completedToBeReported = new();

		public Listener(StrikeLightningClient client)
		{
			_client = client;
		}

		public void Dispose()
		{
		}

		public async Task<LightningInvoice> WaitInvoice(CancellationToken cancellation)
		{
			try
			{
				return await ReturnCompletedOrEmpty(cancellation);
			}
			catch (Exception e)
			{
				_client._logger.LogWarning(e, "Failed while listening for invoice status, error: {errorMessage}", e.Message);
				_completedToBeReported.Clear();
				await Task.Delay(TimeSpan.FromSeconds(5), cancellation);
			}

			return EmptyResponse();
		}

		private async Task<LightningInvoice> ReturnCompletedOrEmpty(CancellationToken cancellation)
		{
			if (_completedToBeReported.Count == 0)
			{
				var completed = await GetCompleted(cancellation);
				if (completed.Length == 0)
				{
					// nothing is paid or expired yet, let's wait a bit and restart cycle
					await Task.Delay(TimeSpan.FromSeconds(2), cancellation);
					return EmptyResponse();
				}

				// store completed invoices and report them one by one
				_completedToBeReported.AddRange(completed);
			}

			foreach (var invoice in _completedToBeReported.ToArray())
			{
				await using var storage = _client._db.ResolveStorage();
				var request = await storage.FindReceiveRequest(invoice.Id);
				if (request == null)
				{
					_completedToBeReported.Remove(invoice);
					continue;
				}

				request.Observed = true;
				await storage.Store(request);

				_completedToBeReported.Remove(invoice);
				return invoice;
			}

			return EmptyResponse();
		}

		// BTCPayServer expects a non-null invoice to be returned
		private static LightningInvoice EmptyResponse() => new()
		{
			Id = string.Empty
		};

		private async Task<LightningInvoice[]> GetCompleted(CancellationToken cancellation)
		{
			await using var storage = _client._db.ResolveStorage();
			var unobserved = await storage.GetUnobserved(cancellation);
			if (unobserved.Length == 0)
				return Array.Empty<LightningInvoice>();

			var receives = await QueryStrikeApi(unobserved);
			var converted = ConvertToBtcPayFormat(receives, unobserved).ToArray();
			var completed = converted
				.Where(x => x != null && x.Status != LightningInvoiceStatus.Unpaid)
				.ToArray();

			await storage.Store();
			return completed!;
		}

		private static IEnumerable<LightningInvoice?> ConvertToBtcPayFormat(Receive[] receives, StrikeReceiveRequest[] unobserved)
		{
			foreach (var request in unobserved)
			{
				var receive = receives.FirstOrDefault(x => x.ReceiveRequestId.ToString() == request.ReceiveRequestId);
				if (receive != null && receive.State == ReceiveState.Completed)
					yield return ConvertAndUpdateReceive(receive, request);
				if (request.IsExpired)
					yield return ConvertReceiveRequest(request);
			}
		}

		private static LightningInvoice ConvertAndUpdateReceive(Receive receive, StrikeReceiveRequest request)
		{
			var status = TranslateStatus(receive, request);
			var amount = receive.AmountReceived.Currency == Currency.Btc ?
				new LightMoney(receive.AmountReceived.Amount, LightMoneyUnit.BTC) :
				new LightMoney(request.RealBtcAmount, LightMoneyUnit.BTC);

			UpdateRequest(request, receive);

			return new LightningInvoice
			{
				Id = request.ReceiveRequestId,
				BOLT11 = receive.Lightning?.Invoice,
				PaymentHash = receive.Lightning?.PaymentHash,
				Preimage = receive.Lightning?.Preimage,
				PaidAt = receive.Completed,
				ExpiresAt = request.ExpiresAt,
				Amount = amount,
				AmountReceived = amount,
				Status = status
			};
		}

		private static void UpdateRequest(StrikeReceiveRequest request, Receive receive)
		{
			if (receive.State != ReceiveState.Completed)
				return;

			request.Paid = true;
			request.PaidAt = receive.Completed;
			request.ConversionRate = receive.ConversionRate?.Amount;
			request.PaymentPreimage = receive.Lightning?.Preimage;
			request.PaymentCounterpartyId = receive.P2P?.PayerAccountId.ToString();

			if (receive.AmountCredited != null)
			{
				request.TargetAmount = receive.AmountCredited.Amount;
				request.TargetCurrency = receive.AmountCredited.Currency.ToStringUpperInvariant();
			}
		}

		private async Task<Receive[]> QueryStrikeApi(StrikeReceiveRequest[] unobserved)
		{
			var bulks = unobserved.Batch(100);
			var receives = new List<Receive>();

			foreach (var bulk in bulks)
			{
				var requestIds = bulk.Select(x => Guid.Parse(x.ReceiveRequestId)).ToArray();
				var collection = await _client._client.ReceiveRequests.GetReceives(receiveRequestId: requestIds);
				receives.AddRange(collection.Items);
			}

			return receives.ToArray();
		}

		private static LightningInvoiceStatus TranslateStatus(Receive receive, StrikeReceiveRequest request)
		{
			if (receive.State == ReceiveState.Completed)
				return LightningInvoiceStatus.Paid;

			if (request.IsExpired)
				return LightningInvoiceStatus.Expired;

			return receive.State switch
			{
				ReceiveState.Pending => LightningInvoiceStatus.Unpaid,
				ReceiveState.Undefined => LightningInvoiceStatus.Unpaid,
				_ => LightningInvoiceStatus.Expired
			};
		}
	}
}
