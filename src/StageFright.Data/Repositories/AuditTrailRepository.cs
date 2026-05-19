namespace StageFright.Data.Repositories;

using Microsoft.EntityFrameworkCore;
using StageFright.Core.Entities;
using StageFright.Data.Context;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

/// <summary>Repository implementation for Audit Trail entity.</summary>
public class AuditTrailRepository : BaseRepository<AuditTrail>, IAuditTrailRepository
{
	public AuditTrailRepository(StageFrightContext context) : base(context) { }

	public async Task LogAsync(string entityType, Guid entityId, string action, string? userId = null, string? oldValue = null, string? newValue = null)
	{
		var auditEntry = new AuditTrail
		{
			EntityType = entityType,
			EntityId = entityId,
			Action = action,
			UserId = userId,
			Timestamp = DateTime.UtcNow,
			OldValue = oldValue,
			NewValue = newValue
		};
		await CreateAsync(auditEntry);
	}

	public async Task<IEnumerable<AuditTrail>> GetByEntityAsync(string entityType, Guid entityId)
	{
		return await _dbSet
			.Where(at => at.EntityType == entityType && at.EntityId == entityId)
			.OrderByDescending(at => at.Timestamp)
			.ToListAsync();
	}

	public async Task PurgeExpiredAsync()
	{
		var thirteenMonthsAgo = DateTime.UtcNow.AddMonths(-13);
		var expiredRecords = await _dbSet
			.Where(at => at.Timestamp < thirteenMonthsAgo)
			.ToListAsync();

		foreach (var record in expiredRecords)
		{
			_dbSet.Remove(record);
		}

		await SaveChangesAsync();
	}
}
