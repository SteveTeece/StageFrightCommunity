namespace StageFright.Data.Repositories;

using StageFright.Core.Entities;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>Repository interface for Rehearsal operations.</summary>
public interface IRehearsalRepository : IRepository<Rehearsal>
{
	/// <summary>Gets rehearsals within a date range.</summary>
	Task<IEnumerable<Rehearsal>> GetByDateRangeAsync(DateTime startDate, DateTime endDate);

	/// <summary>Gets the most recent rehearsal.</summary>
	Task<Rehearsal?> GetMostRecentAsync();
}

/// <summary>Repository interface for Event operations.</summary>
public interface IEventRepository : IRepository<Event>
{
	/// <summary>Gets events within a date range.</summary>
	Task<IEnumerable<Event>> GetByDateRangeAsync(DateTime startDate, DateTime endDate);

	/// <summary>Gets the most recent event.</summary>
	Task<Event?> GetMostRecentAsync();
}

/// <summary>Repository interface for Attendance operations.</summary>
public interface IAttendanceRepository : IRepository<Attendance>
{
	/// <summary>Records attendance for a member at a rehearsal.</summary>
	Task RecordAsync(Guid rehearsalId, Guid memberId);

	/// <summary>Gets all attendance records for a rehearsal.</summary>
	Task<IEnumerable<Attendance>> GetByRehearsalAsync(Guid rehearsalId);

	/// <summary>Gets all attendance records for a member.</summary>
	Task<IEnumerable<Attendance>> GetByMemberAsync(Guid memberId);

	/// <summary>Calculates attendance rate for a member.</summary>
	Task<decimal> GetAttendanceRateAsync(Guid memberId, DateTime fromDate, DateTime toDate);
}

/// <summary>Repository interface for Participation operations.</summary>
public interface IParticipationRepository : IRepository<Participation>
{
	/// <summary>Records participation for a member in an event.</summary>
	Task RecordAsync(Guid eventId, Guid memberId);

	/// <summary>Gets all participation records for an event.</summary>
	Task<IEnumerable<Participation>> GetByEventAsync(Guid eventId);

	/// <summary>Gets all participation records for a member.</summary>
	Task<IEnumerable<Participation>> GetByMemberAsync(Guid memberId);

	/// <summary>Calculates participation rate for a member.</summary>
	Task<decimal> GetParticipationRateAsync(Guid memberId, DateTime fromDate, DateTime toDate);
}

/// <summary>Repository interface for Category operations.</summary>
public interface ICategoryRepository : IRepository<Category>
{
	/// <summary>Gets all income categories.</summary>
	Task<IEnumerable<Category>> GetIncomeCategoriesAsync();

	/// <summary>Gets all expense categories.</summary>
	Task<IEnumerable<Category>> GetExpenseCategoriesAsync();

	/// <summary>Archives a category (soft-delete for categories).</summary>
	Task ArchiveAsync(Guid id);

	/// <summary>Restores an archived category.</summary>
	Task RestoreAsync(Guid id);

	/// <summary>Validates whether a category can be archived.</summary>
	Task<bool> ValidateArchivalAsync(Guid id);
}

/// <summary>Repository interface for Settings operations (singleton).</summary>
public interface ISettingsRepository : IRepository<Settings>
{
	/// <summary>Gets the singleton settings record.</summary>
	Task<Settings?> GetSettingsAsync();

	/// <summary>Updates the singleton settings record.</summary>
	Task UpdateSettingsAsync(Settings settings);
}

/// <summary>Repository interface for Committee Membership operations.</summary>
public interface ICommitteeMembershipRepository : IRepository<CommitteeMembership>
{
	/// <summary>Gets committee memberships for a member.</summary>
	Task<IEnumerable<CommitteeMembership>> GetByMemberAsync(Guid memberId);

	/// <summary>Gets committee memberships for a specific year.</summary>
	Task<IEnumerable<CommitteeMembership>> GetByYearAsync(int year);

	/// <summary>Records a committee membership.</summary>
	Task RecordAsync(Guid memberId, int year, string position);

	/// <summary>Clears all committee memberships for a year.</summary>
	Task ClearYearAsync(int year);

	/// <summary>Gets committee membership history for a member.</summary>
	Task<IEnumerable<CommitteeMembership>> GetHistoryAsync(Guid memberId);
}

/// <summary>Repository interface for Audit Trail operations.</summary>
public interface IAuditTrailRepository : IRepository<AuditTrail>
{
	/// <summary>Logs an audit trail entry.</summary>
	Task LogAsync(string entityType, Guid entityId, string action, string? userId = null, string? oldValue = null, string? newValue = null);

	/// <summary>Gets audit trail entries for an entity.</summary>
	Task<IEnumerable<AuditTrail>> GetByEntityAsync(string entityType, Guid entityId);

	/// <summary>Purges audit entries older than 12 months.</summary>
	Task PurgeExpiredAsync();
}
