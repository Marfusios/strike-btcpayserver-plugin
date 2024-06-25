using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BTCPayServer.Plugins.Strike.Persistence;
public class StrikeStorage
{
	private readonly ILogger _logger;
	private readonly StrikeDbContext _db;

	public StrikeStorage(StrikeDbContext db, ILogger logger)
	{
		_db = db;
		_logger = logger;
	}

	public string TenantId { get; set; } = string.Empty;

	public async Task<StrikeQuote[]> GetUnobserved(CancellationToken cancellation)
	{
		ValidateTenantId();

		return await _db.Quotes
			.Where(x => x.TenantId == TenantId && !x.Observed)
			.ToArrayAsync(cancellation);
	}

	public async Task<StrikeQuote?> FindByInvoiceId(string invoiceId)
	{
		return await _db.Quotes
			.FirstOrDefaultAsync(x => x.TenantId == TenantId && x.InvoiceId == invoiceId);
	}

	public async Task<StrikeQuote?> FindByPaymentHash(string paymentHash)
	{
		return await _db.Quotes
			.FirstOrDefaultAsync(x => x.TenantId == TenantId && x.PaymentHash == paymentHash);
	}

	public async Task Store(IHasTenantId entity)
	{
		try
		{
			ValidateTenantId();

			if (_db.Entry(entity).State == EntityState.Detached)
			{
				entity.TenantId = TenantId;
				_db.Add(entity);
			}
			else
			{
				ValidateTenantId(entity.TenantId);
			}

			await _db.SaveChangesAsync();
		}
		catch (Exception e)
		{
			_logger.LogError(e, "Failed to store entity into the DB, error: {error} / {inner}", e.Message, e.InnerException?.Message);
			throw;
		}
	}

	private void ValidateTenantId()
	{
		if (string.IsNullOrWhiteSpace(TenantId))
			throw new InvalidOperationException("StoreId is not set, cannot perform any DB operation");
	}

	private void ValidateTenantId(string targetTenantId)
	{
		if (targetTenantId != TenantId)
			throw new InvalidOperationException($"The updated entity doesn't belong to this tenant ({targetTenantId} vs. {TenantId}), cannot continue");
	}
}
