namespace StageFright.Core.Modules.Events;

/// <summary>
/// Discriminates which source record a <see cref="CombinedEventListItem"/> row was built from —
/// there are exactly two kinds being merged onto the All Events screen (spec 023).
/// </summary>
public enum CombinedEventListItemKind
{
    /// <summary>The row was built from an <see cref="Entities.Event"/>.</summary>
    Event,

    /// <summary>The row was built from an <see cref="Entities.AnnualGeneralMeeting"/>.</summary>
    Agm
}
