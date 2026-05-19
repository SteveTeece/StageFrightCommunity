namespace StageFright.Data.Repositories;

using Microsoft.EntityFrameworkCore;
using StageFright.Core.Entities;
using StageFright.Data.Context;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

/// <summary>Repository implementation for Rehearsal entity.</summary>
public class RehearsalRepository : BaseRepository<Rehearsal>, IRehearsalRepository
{
	public RehearsalRepository(StageFrightContext context) : base(context) { }

	public async Task<IEnumerable<Rehearsal>> GetByDateRangeAsync(DateTime startDate, DateTime endDate)
	{
		return await _dbSet
			.Where(r => r.Date >= startDate && r.Date <= endDate && !r.IsDeleted)
			.OrderBy(r => r.Date)
			.ToListAsync();
	}

	public async Task<Rehearsal?> GetMostRecentAsync()
	{
		return await _dbSet
			.Where(r => !r.IsDeleted)
			.OrderByDescending(r => r.Date)
			.FirstOrDefaultAsync();
	}
}

/// <summary>Repository implementation for Event entity.</summary>
public class EventRepository : BaseRepository<Event>, IEventRepository
{
	public EventRepository(StageFrightContext context) : base(context) { }

	public async Task<IEnumerable<Event>> GetByDateRangeAsync(DateTime startDate, DateTime endDate)
	{
		return await _dbSet
			.Where(e => e.Date >= startDate && e.Date <= endDate && !e.IsDeleted)
			.OrderBy(e => e.Date)
			.ToListAsync();
	}

	public async Task<Event?> GetMostRecentAsync()
	{
		return await _dbSet
			.Where(e => !e.IsDeleted)
			.OrderByDescending(e => e.Date)
			.FirstOrDefaultAsync();
	}
}

/// <summary>Repository implementation for Attendance entity.</summary>
public class AttendanceRepository : BaseRepository<Attendance>, IAttendanceRepository
{
	public AttendanceRepository(StageFrightContext context) : base(context) { }

	public async Task RecordAsync(Guid rehearsalId, Guid memberId)
	{
		var attendance = new Attendance
		{
			RehearsalId = rehearsalId,
			MemberId = memberId,
			RecordedAt = DateTime.UtcNow
		};
		await CreateAsync(attendance);
	}

	public async Task<IEnumerable<Attendance>> GetByRehearsalAsync(Guid rehearsalId)
	{
		return await _dbSet
			.Where(a => a.RehearsalId == rehearsalId)
			.ToListAsync();
	}

	public async Task<IEnumerable<Attendance>> GetByMemberAsync(Guid memberId)
	{
		return await _dbSet
			.Where(a => a.MemberId == memberId)
			.ToListAsync();
	}

	public async Task<decimal> GetAttendanceRateAsync(Guid memberId, DateTime fromDate, DateTime toDate)
	{
		var rehearsals = await _context.Rehearsals
			.Where(r => r.Date >= fromDate && r.Date <= toDate && !r.IsDeleted)
			.ToListAsync();

		if (rehearsals.Count == 0)
			return 0;

		var attendances = await _dbSet
			.Where(a => a.MemberId == memberId && rehearsals.Select(r => r.Id).Contains(a.RehearsalId))
			.ToListAsync();

		return (decimal)attendances.Count / rehearsals.Count * 100;
	}
}

/// <summary>Repository implementation for Participation entity.</summary>
public class ParticipationRepository : BaseRepository<Participation>, IParticipationRepository
{
	public ParticipationRepository(StageFrightContext context) : base(context) { }

	public async Task RecordAsync(Guid eventId, Guid memberId)
	{
		var participation = new Participation
		{
			EventId = eventId,
			MemberId = memberId,
			RecordedAt = DateTime.UtcNow
		};
		await CreateAsync(participation);
	}

	public async Task<IEnumerable<Participation>> GetByEventAsync(Guid eventId)
	{
		return await _dbSet
			.Where(p => p.EventId == eventId)
			.ToListAsync();
	}

	public async Task<IEnumerable<Participation>> GetByMemberAsync(Guid memberId)
	{
		return await _dbSet
			.Where(p => p.MemberId == memberId)
			.ToListAsync();
	}

	public async Task<decimal> GetParticipationRateAsync(Guid memberId, DateTime fromDate, DateTime toDate)
	{
		var events = await _context.Events
			.Where(e => e.Date >= fromDate && e.Date <= toDate && !e.IsDeleted)
			.ToListAsync();

		if (events.Count == 0)
			return 0;

		var participations = await _dbSet
			.Where(p => p.MemberId == memberId && events.Select(e => e.Id).Contains(p.EventId))
			.ToListAsync();

		return (decimal)participations.Count / events.Count * 100;
	}
}

/// <summary>Repository implementation for Category entity.</summary>
public class CategoryRepository : BaseRepository<Category>, ICategoryRepository
{
	public CategoryRepository(StageFrightContext context) : base(context) { }

	public async Task<IEnumerable<Category>> GetIncomeCategoriesAsync()
	{
		return await _dbSet
			.Where(c => c.Type == "Income" && !c.IsArchived && !c.IsDeleted)
			.OrderBy(c => c.SortOrder)
			.ToListAsync();
	}

	public async Task<IEnumerable<Category>> GetExpenseCategoriesAsync()
	{
		return await _dbSet
			.Where(c => c.Type == "Expense" && !c.IsArchived && !c.IsDeleted)
			.OrderBy(c => c.SortOrder)
			.ToListAsync();
	}

	public async Task ArchiveAsync(Guid id)
	{
		var category = await GetByIdAsync(id);
		if (category == null)
			throw new InvalidOperationException($"Category with ID {id} not found.");

		category.IsArchived = true;
		await UpdateAsync(category);
	}

	public async Task RestoreAsync(Guid id)
	{
		var category = await GetByIdAsync(id);
		if (category == null)
			throw new InvalidOperationException($"Category with ID {id} not found.");

		category.IsArchived = false;
		await UpdateAsync(category);
	}

	public async Task<bool> ValidateArchivalAsync(Guid id)
	{
		var category = await GetByIdAsync(id);
		if (category == null)
			return false;

		// Check if this category is referenced by any transactions
		var hasTransactions = await _context.Transactions
			.AnyAsync(t => t.Category == category.Name);

		return !hasTransactions;
	}
}

/// <summary>Repository implementation for Settings entity (singleton).</summary>
public class SettingsRepository : BaseRepository<Settings>, ISettingsRepository
{
	public SettingsRepository(StageFrightContext context) : base(context) { }

	public async Task<Settings?> GetSettingsAsync()
	{
		return await _dbSet.FirstOrDefaultAsync();
	}

	public async Task UpdateSettingsAsync(Settings settings)
	{
		var existing = await GetSettingsAsync();
		if (existing != null)
		{
			existing.OrganizationName = settings.OrganizationName;
			existing.AnnualFee = settings.AnnualFee;
			existing.AttendanceFee = settings.AttendanceFee;
			existing.RenewalMonth = settings.RenewalMonth;
			existing.CommitteeRenewalMonth = settings.CommitteeRenewalMonth;
			existing.MaxAgeRange = settings.MaxAgeRange;
			existing.MinimumMemberAge = settings.MinimumMemberAge;
			existing.Theme = settings.Theme;
			existing.ModifiedAt = DateTime.UtcNow;
			await UpdateAsync(existing);
		}
		else
		{
			settings.CreatedAt = DateTime.UtcNow;
			settings.ModifiedAt = DateTime.UtcNow;
			await CreateAsync(settings);
		}
	}
}

/// <summary>Repository implementation for Committee Membership entity.</summary>
public class CommitteeMembershipRepository : BaseRepository<CommitteeMembership>, ICommitteeMembershipRepository
{
	public CommitteeMembershipRepository(StageFrightContext context) : base(context) { }

	public async Task<IEnumerable<CommitteeMembership>> GetByMemberAsync(Guid memberId)
	{
		return await _dbSet
			.Where(cm => cm.MemberId == memberId && !cm.IsDeleted)
			.OrderByDescending(cm => cm.Year)
			.ToListAsync();
	}

	public async Task<IEnumerable<CommitteeMembership>> GetByYearAsync(int year)
	{
		return await _dbSet
			.Where(cm => cm.Year == year && !cm.IsDeleted)
			.ToListAsync();
	}

	public async Task RecordAsync(Guid memberId, int year, string position)
	{
		var membership = new CommitteeMembership
		{
			MemberId = memberId,
			Year = year,
			Position = position,
			CreatedAt = DateTime.UtcNow,
			ModifiedAt = DateTime.UtcNow
		};
		await CreateAsync(membership);
	}

	public async Task ClearYearAsync(int year)
	{
		var memberships = await GetByYearAsync(year);
		foreach (var membership in memberships)
		{
			await SoftDeleteAsync(membership.Id);
		}
	}

	public async Task<IEnumerable<CommitteeMembership>> GetHistoryAsync(Guid memberId)
	{
		return await _dbSet
			.Where(cm => cm.MemberId == memberId)
			.OrderByDescending(cm => cm.Year)
			.ToListAsync();
	}
}

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
