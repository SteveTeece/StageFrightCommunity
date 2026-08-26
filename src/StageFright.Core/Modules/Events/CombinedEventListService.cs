using StageFright.Core.Contracts;
using StageFright.Core.Entities;

namespace StageFright.Core.Modules.Events;

/// <summary>
/// Merges IEventService and IAgmService results into one date-sorted list of
/// CombinedEventListItem for the All Events screen (spec 023). Read-only — depends only on the
/// two published service contracts, never on the Agm module's concrete AgmService (constitution
/// §4.1's no-cross-module-dependency rule).
/// </summary>
public class CombinedEventListService : ICombinedEventListService
{
    private readonly IEventService _eventService;
    private readonly IAgmService _agmService;

    public CombinedEventListService(IEventService eventService, IAgmService agmService)
    {
        _eventService = eventService;
        _agmService = agmService;
    }

    public async Task<IReadOnlyList<CombinedEventListItem>> GetAllAsync(CancellationToken ct = default)
    {
        var events = await _eventService.GetAllAsync(ct);
        var agms = await _agmService.GetAllAsync(ct);

        var items = new List<CombinedEventListItem>(events.Count + agms.Count);
        items.AddRange(events.Select(MapEvent));
        items.AddRange(agms.Select(MapAgm));

        // Secondary sort key (Kind) makes same-date ordering deterministic — e.g. an Event and an
        // AGM scheduled on the same day — rather than relying on OrderByDescending's stability
        // across the two concatenated source orderings.
        return items
            .OrderByDescending(item => item.Date)
            .ThenBy(item => item.Kind)
            .ToList();
    }

    private static CombinedEventListItem MapEvent(Event e) => new()
    {
        Id = e.Id,
        Date = e.Date,
        Notes = e.Notes,
        TypeName = e.EventType?.Name ?? "—",
        Kind = CombinedEventListItemKind.Event,
        ParticipationRate = e.StoredParticipationRate,
        IsAgmRecorded = null,
        DetailUrl = $"/events/{e.Id}"
    };

    private static CombinedEventListItem MapAgm(AnnualGeneralMeeting agm) => new()
    {
        Id = agm.Id,
        Date = agm.Date,
        Notes = agm.Notes,
        TypeName = "Annual General Meeting",
        Kind = CombinedEventListItemKind.Agm,
        ParticipationRate = null,
        IsAgmRecorded = agm.IsRecorded,
        DetailUrl = $"/events/agm/{agm.Id}"
    };
}
