namespace StageFright.Core.Services;

using Entities;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>Service for member-related business logic.</summary>
public interface IMemberService
{
	Task<Member> CreateMemberAsync(Member member);
	Task<Member?> GetMemberByIdAsync(Guid id);
	Task<IEnumerable<Member>> GetActiveMembersAsync();
	Task<IEnumerable<Member>> GetInactiveMembersAsync();
	Task UpdateMemberAsync(Member member);
	Task InactivateMemberAsync(Guid id);
	Task ActivateMemberAsync(Guid id);
	Task<int> GetActiveMemberCountAsync();
	Task<int> CalculateAgeAsync(DateTime dateOfBirth);
}

/// <summary>Service for rehearsal-related business logic.</summary>
public interface IRehearsalService
{
	Task<Rehearsal> ScheduleRehearsalAsync(DateTime date, TimeSpan time, string? notes = null);
	Task<Rehearsal?> GetRehearsalByIdAsync(Guid id);
	Task<IEnumerable<Rehearsal>> GetRehearsalsAsync(DateTime fromDate, DateTime toDate);
	Task<Rehearsal?> GetMostRecentRehearsalAsync();
	Task RecordAttendanceAsync(Guid rehearsalId, Guid memberId);
	Task<decimal> GetAttendanceRateAsync(Guid memberId, DateTime fromDate, DateTime toDate);
}

/// <summary>Service for event-related business logic.</summary>
public interface IEventService
{
	Task<Event> ScheduleEventAsync(DateTime date, string eventType, string? notes = null);
	Task<Event?> GetEventByIdAsync(Guid id);
	Task<IEnumerable<Event>> GetEventsAsync(DateTime fromDate, DateTime toDate);
	Task<Event?> GetMostRecentEventAsync();
	Task RecordParticipationAsync(Guid eventId, Guid memberId);
	Task<decimal> GetParticipationRateAsync(Guid memberId, DateTime fromDate, DateTime toDate);
}

/// <summary>Service for category management.</summary>
public interface ICategoryService
{
	Task<Category> CreateCategoryAsync(Category category);
	Task<Category?> GetCategoryByIdAsync(Guid id);
	Task<IEnumerable<Category>> GetCategoriesAsync(string type);
	Task UpdateCategoryAsync(Category category);
	Task ArchiveCategoryAsync(Guid id);
	Task RestoreCategoryAsync(Guid id);
}

/// <summary>Service for committee membership tracking.</summary>
public interface ICommitteeMembershipService
{
	Task<IEnumerable<CommitteeMembership>> GetMemberCommitteeHistoryAsync(Guid memberId);
	Task<IEnumerable<CommitteeMembership>> GetCommitteeForYearAsync(int year);
	Task RecordCommitteeMembershipAsync(Guid memberId, int year, string position);
	Task ResetCommitteeYearAsync(int year);
	Task<CommitteeMembership?> GetCurrentCommitteeMembershipAsync(Guid memberId);
}

/// <summary>Service for settings management.</summary>
public interface ISettingsService
{
	Task<Settings> GetSettingsAsync();
	Task UpdateSettingsAsync(Settings settings);
	Task InitializeDefaultSettingsAsync(string organizationName, decimal annualFee, decimal attendanceFee);
}

/// <summary>Service for first-run setup.</summary>
public interface ISetupService
{
	Task InitializeApplicationAsync(string organizationName, decimal annualFee, decimal attendanceFee, int renewalMonth);
}
