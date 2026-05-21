namespace StageFright.Core.Services;

using Entities;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

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
