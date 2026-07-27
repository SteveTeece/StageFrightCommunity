namespace StageFright.Core.Modules.Rehearsals;

/// <summary>One row on a printable attendance roll. Attended and Rehearsal Fee Paid checkboxes are always blank on print, so they have no corresponding field here.</summary>
public sealed class AttendanceRollMember
{
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public bool AnnualFeePaid { get; init; }
}
