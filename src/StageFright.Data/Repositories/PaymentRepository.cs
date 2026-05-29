namespace StageFright.Data.Repositories;

using StageFright.Core.Entities;
using StageFright.Data.Context;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

/// <summary>Repository implementation for Payment entity.</summary>
public class PaymentRepository : BaseRepository<Payment>, IPaymentRepository
{
	public PaymentRepository(StageFrightContext context) : base(context) { }

	public override async Task UpdateAsync(Payment entity)
	{
		// Only allow Notes to be updated
		var existing = await GetByIdAsync(entity.Id);
		if (existing == null)
			throw new InvalidOperationException($"Payment with ID {entity.Id} not found.");

		existing.Notes = entity.Notes;
		existing.UpdatedAt = DateTime.UtcNow;
		_dbSet.Update(existing);
		await SaveChangesAsync();
	}

	public async Task<IEnumerable<Payment>> GetByMemberAsync(Guid memberId)
	{
		return await _dbSet
			.Where(p => p.MemberId == memberId)
			.OrderByDescending(p => p.Date)
			.ToListAsync();
	}

	public async Task<IEnumerable<Payment>> GetPaymentHistoryAsync(Guid memberId, DateTime fromDate, DateTime toDate)
	{
		return await _dbSet
			.Where(p => p.MemberId == memberId && p.Date >= fromDate && p.Date <= toDate)
			.OrderByDescending(p => p.Date)
			.ToListAsync();
	}

	public async Task UpdateNotesAsync(Guid paymentId, string? notes)
	{
		var payment = await GetByIdAsync(paymentId);
		if (payment == null)
			throw new InvalidOperationException($"Payment with ID {paymentId} not found.");

		payment.Notes = notes;
		payment.UpdatedAt = DateTime.UtcNow;
		await UpdateAsync(payment);
	}
}
