using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BTCPayServer.Plugins.Strike.Persistence;
public class StrikeStorage : IDisposable, IAsyncDisposable
{
	private readonly ILogger _logger;
	private readonly StrikeDbContext _db;

	public StrikeStorage(StrikeDbContext db, ILogger logger)
	{
		_db = db;
		_logger = logger;
	}

	public string? TenantId { get; set; }

	public async Task<StrikeReceiveRequest[]> GetUnobserved(CancellationToken cancellation)
	{
		ValidateTenantId();

		return await _db.ReceiveRequests
			.Where(x => x.TenantId == TenantId && !x.Observed)
			.ToArrayAsync(cancellation);
	}

	public async Task<StrikeReceiveRequest[]> GetReceiveRequests(bool onlyPending, int offset = 0)
	{
		var now = DateTimeOffset.UtcNow;
		return await _db.ReceiveRequests
			.Where(x => TenantId == null || x.TenantId == TenantId)
			.Where(x => onlyPending && !x.Paid && x.ExpiresAt > now)
			.OrderByDescending(x => x.CreatedAt)
			.Skip(offset)
			.ToArrayAsync();
	}

	public async Task<StrikeReceiveRequest?> FindReceiveRequest(string receiveRequestId)
	{
		return await _db.ReceiveRequests
			.FirstOrDefaultAsync(x => (TenantId == null || x.TenantId == TenantId) && x.ReceiveRequestId == receiveRequestId);
	}

	public async Task<StrikeReceiveRequest?> FindReceiveRequestByPaymentHash(string paymentHash)
	{
		return await _db.ReceiveRequests
			.FirstOrDefaultAsync(x => (TenantId == null || x.TenantId == TenantId) && x.PaymentHash == paymentHash);
	}

	public async Task<StrikePayment?> FindPaymentByPaymentHash(string paymentHash)
	{
		return await _db.Payments
			.FirstOrDefaultAsync(x => (TenantId == null || x.TenantId == TenantId) && x.PaymentHash == paymentHash);
	}

	public async Task<StrikePayment[]> GetPayments(bool onlyCompleted, int offset = 0)
	{
		return await _db.Payments
			.Where(x => TenantId == null || x.TenantId == TenantId)
			.Where(x => onlyCompleted && x.CompletedAt != null)
			.OrderByDescending(x => x.CreatedAt)
			.Skip(offset)
			.ToArrayAsync();
	}

	public async Task Store(IHasTenantId entity)
	{
		try
		{
			if (_db.Entry(entity).State == EntityState.Detached)
			{
				ValidateTenantId();
				entity.TenantId = TenantId ?? string.Empty;
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

	public async Task Store()
	{
		try
		{
			await _db.SaveChangesAsync();
		}
		catch (Exception e)
		{
			_logger.LogError(e, "Failed to store entities into the DB, error: {error} / {inner}", e.Message, e.InnerException?.Message);
			throw;
		}
	}

	public void Dispose()
	{
		_db.Dispose();
	}

	public async ValueTask DisposeAsync()
	{
		await _db.DisposeAsync();
	}

	private void ValidateTenantId()
	{
		if (string.IsNullOrWhiteSpace(TenantId))
			throw new InvalidOperationException("StoreId is not set, cannot perform any DB operation");
	}

	private void ValidateTenantId(string targetTenantId)
	{
		if (TenantId == null)
		{
			// special case, we are not tenant-bound
			return;
		}

		if (targetTenantId != TenantId)
			throw new InvalidOperationException($"The updated entity doesn't belong to this tenant ({targetTenantId} vs. {TenantId}), cannot continue");
	}
}
