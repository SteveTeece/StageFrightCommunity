namespace StageFright.Core.Services;

using Entities;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

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
