﻿using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BTCPayServer.Plugins.Strike.Persistence;
public class StrikeStorageRemoved : IDisposable, IAsyncDisposable
{
	private readonly ILogger _logger;
	private readonly StrikeDbContext _db;

	public StrikeStorageRemoved(StrikeDbContext db, ILogger logger)
	{
		_db = db;
		_logger = logger;
	}

	public void Dispose() => _db.Dispose();
	public async ValueTask DisposeAsync() => await _db.DisposeAsync();

	public string TenantId { get; set; } = string.Empty;

	public async Task<StrikeQuote[]> GetUnobserved(CancellationToken cancellation)
	{
		ValidateTenantId();

		return await _db.Quotes
			.Where(x => x.TenantId == TenantId && !x.Observed)
			.ToArrayAsync(cancellation);
	}

	public async Task<StrikeQuote[]> GetPaidQuotesToConvert(CancellationToken cancellation)
	{
		ValidateTenantId();

		return await _db.Quotes
			.Where(x => x.TenantId == TenantId && x.PaidConvertTo != null && x.Observed && x.Paid)
			.ToArrayAsync(cancellation);
	}

	public async Task<StrikeQuote?> FindQuoteByInvoiceId(string invoiceId)
	{
		return await _db.Quotes
			.FirstOrDefaultAsync(x => x.TenantId == TenantId && x.InvoiceId == invoiceId);
	}

	public async Task<StrikeQuote?> FindQuoteByPaymentHash(string paymentHash)
	{
		return await _db.Quotes
			.FirstOrDefaultAsync(x => x.TenantId == TenantId && x.PaymentHash == paymentHash);
	}

	public async Task<StrikePayment?> FindPaymentByPaymentHash(string paymentHash)
	{
		return await _db.Payments
			.FirstOrDefaultAsync(x => x.TenantId == TenantId && x.PaymentHash == paymentHash);
	}

	public async Task<StrikePayment[]> GetPayments(bool onlyCompleted, int offset = 0)
	{
		return await _db.Payments
			.Where(x => x.TenantId == TenantId)
			.Where(x => onlyCompleted && x.CompletedAt != null)
			.OrderByDescending(x => x.CreatedAt)
			.Skip(offset)
			.ToArrayAsync();
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
