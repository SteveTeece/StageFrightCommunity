namespace StageFright.Maui.Services;

using Entities;
using Exceptions;
using StageFright.Data.Repositories;
using StageFright.Data.Context;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

/// <summary>Service for event scheduling and participation recording.</summary>
public class EventService : IEventService
{
	private readonly IEventRepository _eventRepository;
	private readonly IParticipationRepository _participationRepository;
	private readonly IMemberRepository _memberRepository;
	private readonly StageFrightContext _context;

	public EventService(
		IEventRepository eventRepository,
		IParticipationRepository participationRepository,
		IMemberRepository memberRepository,
		StageFrightContext context)
	{
		_eventRepository = eventRepository;
		_participationRepository = participationRepository;
		_memberRepository = memberRepository;
		_context = context;
	}

	public async Task<Event> ScheduleEventAsync(DateTime date, string eventType, string? notes = null)
	{
		if (date < DateTime.Today)
			throw new ValidationException("Event date cannot be in the past.");

		if (string.IsNullOrWhiteSpace(eventType))
			throw new ValidationException("Event type is required.");

		var @event = new Event
		{
			Date = date,
			Time = new TimeSpan(19, 0, 0), // Default to 7 PM
			EventType = eventType,
			Notes = notes ?? string.Empty,
			IsDeleted = false
		};

		await _eventRepository.CreateAsync(@event);
		return @event;
	}

	public async Task<Event?> GetEventByIdAsync(Guid id)
	{
		return await _eventRepository.GetByIdAsync(id);
	}

	public async Task<IEnumerable<Event>> GetEventsAsync(DateTime fromDate, DateTime toDate)
	{
		return await _eventRepository.GetByDateRangeAsync(fromDate, toDate);
	}

	public async Task<Event?> GetMostRecentEventAsync()
	{
		return await _eventRepository.GetMostRecentAsync();
	}

	public async Task RecordParticipationAsync(Guid eventId, Guid memberId)
	{
		var @event = await _eventRepository.GetByIdAsync(eventId);
		if (@event == null)
			throw new EntityNotFoundException($"Event with ID {eventId} not found.");

		var member = await _memberRepository.GetByIdAsync(memberId);
		if (member == null)
			throw new EntityNotFoundException($"Member with ID {memberId} not found.");

		await _participationRepository.RecordAsync(eventId, memberId);
	}

	public async Task<decimal> GetParticipationRateAsync(Guid memberId, DateTime fromDate, DateTime toDate)
	{
		return await _participationRepository.GetParticipationRateAsync(memberId, fromDate, toDate);
	}

	/// <summary>Records batch participation with atomic transaction: creates all Participation records together.</summary>
	public async Task RecordBatchParticipationAsync(Guid eventId, List<Guid> memberIds)
	{
		var @event = await _eventRepository.GetByIdAsync(eventId);
		if (@event == null)
			throw new EntityNotFoundException($"Event with ID {eventId} not found.");

		using var transaction = await _context.Database.BeginTransactionAsync();
		try
		{
			// Record participation for all members
			foreach (var memberId in memberIds)
			{
				var member = await _memberRepository.GetByIdAsync(memberId);
				if (member == null)
					throw new EntityNotFoundException($"Member with ID {memberId} not found.");

				await _participationRepository.RecordAsync(eventId, memberId);
			}

			// Calculate and store participation rate
			var activeMembers = await _memberRepository.GetHistoricalActiveMembersAsync(@event.Date);
			var activeMemberCount = activeMembers.Count();
			var participantCount = memberIds.Count;
			var participationRate = activeMemberCount > 0 ? (decimal)participantCount / activeMemberCount * 100 : 0;

			await _eventRepository.UpdateStoredParticipationRateAsync(eventId, participationRate);

			await transaction.CommitAsync();
		}
		catch
		{
			await transaction.RollbackAsync();
			throw;
		}
	}
}
