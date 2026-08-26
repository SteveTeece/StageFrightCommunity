namespace StageFright.Core.Modules.Events;

/// <summary>
/// A read-only row shown on the All Events screen — one <see cref="Entities.Event"/> or one
/// <see cref="Entities.AnnualGeneralMeeting"/>, projected into a single display shape (spec 023's
/// "Combined Events List Entry"). Not persisted; rebuilt fresh on every
/// <see cref="Contracts.ICombinedEventListService.GetAllAsync"/> call.
/// </summary>
public class CombinedEventListItem
{
    /// <summary>The source record's own identity (Event.Id or AnnualGeneralMeeting.Id).</summary>
    public Guid Id { get; set; }

    /// <summary>The source record's date; drives the combined sort (FR-002) and the Date column (FR-003).</summary>
    public DateTime Date { get; set; }

    /// <summary>The source record's notes, rendered and searched in the existing Notes column (FR-003, FR-008).</summary>
    public string? Notes { get; set; }

    /// <summary>The event's own type name, or the fixed literal "Annual General Meeting" for an AGM row (FR-004).</summary>
    public string TypeName { get; set; } = string.Empty;

    /// <summary>Discriminates which per-kind Status/Actions branch and Print pipeline this row uses.</summary>
    public CombinedEventListItemKind Kind { get; set; }

    /// <summary>Event.StoredParticipationRate; always null for an AGM row.</summary>
    public decimal? ParticipationRate { get; set; }

    /// <summary>AnnualGeneralMeeting.IsRecorded; always null for an Event row. Drives the Recorded/Scheduled badge (FR-005).</summary>
    public bool? IsAgmRecorded { get; set; }

    /// <summary>
    /// Row-click navigation target: "/events/{Id}" for an Event, "/events/agm/{Id}" for an AGM.
    /// Safety-critical (FR-006) — an AGM row must never resolve to the generic event detail route.
    /// </summary>
    public string DetailUrl { get; set; } = string.Empty;
}
