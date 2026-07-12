using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Exceptions;

namespace StageFright.Core.Modules.Events;

/// <summary>
/// Application service for event scheduling and participation recording.
/// Events never create fees or GL entries per FR-006.
/// StoredParticipationRate is frozen on first participation batch; subsequent calls are idempotent.
/// </summary>
public class EventService : IEventService
{
    private readonly IEventRepository _eventRepo;
    private readonly IParticipationRepository _participationRepo;
    private readonly IMemberRepository _memberRepo;
    private readonly IAuditTrailService _audit;
    private readonly IUnitOfWork _unitOfWork;

    public EventService(
        IEventRepository eventRepo,
        IParticipationRepository participationRepo,
        IMemberRepository memberRepo,
        IAuditTrailService audit,
        IUnitOfWork unitOfWork)
    {
        _eventRepo = eventRepo;
        _participationRepo = participationRepo;
        _memberRepo = memberRepo;
        _audit = audit;
        _unitOfWork = unitOfWork;
    }

    public async Task<Event> ScheduleAsync(ScheduleEventRequest request, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var evt = new Event
        {
            Id = Guid.NewGuid(),
            Date = request.Date,
            EventTypeId = request.EventTypeId,
            Notes = request.Notes,
            CreatedAt = now,
            UpdatedAt = now
        };

        Event saved = null!;
        await _unitOfWork.ExecuteInTransactionAsync(async innerCt =>
        {
            saved = await _eventRepo.AddAsync(evt, innerCt);
            await _audit.LogAsync("Event", saved.Id, AuditAction.Create, ct: innerCt);
        }, ct);

        return saved;
    }

    public Task<IReadOnlyList<Event>> GetAllAsync(CancellationToken ct = default) =>
        _eventRepo.GetAllAsync(ct);

    public Task<Event?> GetMostRecentPastAsync(DateTime asOf, CancellationToken ct = default) =>
        _eventRepo.GetMostRecentPastAsync(asOf, ct);

    public Task<Event?> GetMostRecentPastWithoutParticipationAsync(DateTime asOf, CancellationToken ct = default) =>
        _eventRepo.GetMostRecentPastWithoutParticipationAsync(asOf, ct);

    public Task<Event?> GetMostRecentPastWithParticipationAsync(DateTime asOf, CancellationToken ct = default) =>
        _eventRepo.GetMostRecentPastWithParticipationAsync(asOf, ct);

    public Task<Event?> GetNextUpcomingAsync(DateTime asOf, CancellationToken ct = default) =>
        _eventRepo.GetNextUpcomingAsync(asOf, ct);

    public async Task RecordParticipationAsync(Guid eventId, IReadOnlyList<ParticipationBatchItem> items, CancellationToken ct = default)
    {
        var evt = await _eventRepo.GetByIdAsync(eventId, ct)
            ?? throw new EntityNotFoundException("Event", eventId, nameof(RecordParticipationAsync));

        if (evt.Date.Date > DateTime.Today)
            throw new ValidationException(
                "Participation cannot be recorded before the event date.",
                "Event", nameof(RecordParticipationAsync), eventId);

        int participatedCount = 0;
        var records = new List<ParticipationRecord>();

        await _unitOfWork.ExecuteInTransactionAsync(async innerCt =>
        {
            foreach (var item in items)
            {
                if (await _participationRepo.ExistsAsync(eventId, item.MemberId, innerCt))
                    continue;

                records.Add(new ParticipationRecord
                {
                    Id = Guid.NewGuid(),
                    EventId = eventId,
                    MemberId = item.MemberId,
                    Participated = item.Participated,
                    CreatedAt = DateTime.UtcNow
                });

                if (item.Participated)
                    participatedCount++;
            }

            if (records.Count > 0)
                await _participationRepo.AddBatchAsync(records, innerCt);

        }, ct);

        // Freeze participation rate after transaction commits (idempotent)
        if (!evt.StoredParticipationRate.HasValue)
        {
            var activeMembers = await _memberRepo.GetActiveAsOfAsync(evt.Date, ct);
            int denominator = activeMembers.Count;
            decimal rate = denominator > 0
                ? Math.Round((decimal)participatedCount / denominator * 100m, 2)
                : 0m;

            evt.StoredParticipationRate = rate;
            evt.UpdatedAt = DateTime.UtcNow;
            await _eventRepo.UpdateAsync(evt, ct);
        }
    }

    public Task<bool> AgmExistsInYearAsync(int year, CancellationToken ct = default) =>
        _eventRepo.AgmExistsInYearAsync(year, ct);

    public Task<Event?> GetByIdWithDetailsAsync(Guid id, CancellationToken ct = default) =>
        _eventRepo.GetByIdWithDetailsAsync(id, ct);

    public Task<IReadOnlyList<Event>> GetYearToDateWithParticipationAsync(int year, CancellationToken ct = default) =>
        _eventRepo.GetYearToDateWithParticipationAsync(year, ct);
}
