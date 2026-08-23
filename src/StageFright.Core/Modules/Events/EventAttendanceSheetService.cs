using StageFright.Core.Contracts;
using StageFright.Core.Exceptions;

namespace StageFright.Core.Modules.Events;

/// <summary>Assembles the printable event attendance sheet. Read-only.</summary>
public class EventAttendanceSheetService : IEventAttendanceSheetService
{
    private readonly IEventRepository _eventRepo;
    private readonly IMemberRepository _memberRepo;

    public EventAttendanceSheetService(IEventRepository eventRepo, IMemberRepository memberRepo)
    {
        _eventRepo = eventRepo;
        _memberRepo = memberRepo;
    }

    public async Task<EventAttendanceSheetData> GenerateAsync(Guid eventId, CancellationToken ct = default)
    {
        var evt = await _eventRepo.GetByIdWithDetailsAsync(eventId, ct)
            ?? throw new EntityNotFoundException("Event", eventId, nameof(GenerateAsync));

        var activeMembers = (await _memberRepo.GetActiveAsOfAsync(evt.Date, ct))
            .OrderBy(m => m.LastName).ThenBy(m => m.FirstName)
            .ToList();

        var participatedByMember = evt.ParticipationRecords
            .ToDictionary(p => p.MemberId, p => p.Participated);

        var members = activeMembers.Select(m => new EventAttendanceSheetMember
        {
            FirstName = m.FirstName,
            LastName = m.LastName,
            Participated = participatedByMember.TryGetValue(m.Id, out var wasParticipated) && wasParticipated
        }).ToList();

        return new EventAttendanceSheetData
        {
            EventDate = evt.Date,
            EventTypeName = evt.EventType?.Name ?? string.Empty,
            Members = members
        };
    }
}
