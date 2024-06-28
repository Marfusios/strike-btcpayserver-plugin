using System;
using System.Threading;
using System.Threading.Tasks;
using BTCPayServer.Lightning;

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
