using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using StageFright.Core.Entities;
using StageFright.Core.Localization;
using StageFright.UI.Resources.Strings;

namespace StageFright.UI.Pages.Events;

/// <summary>
/// Embeddable attendance grid for an in-progress AGM. Unlike AttendanceGrid (rehearsals),
/// this component owns no data loading of its own — the parent (RecordAgm) supplies the
/// active member list and reads back the checked set via <see cref="GetAttendedMemberIds"/>
/// at save time, since attendance is only one part of one atomic AGM save (FR-009).
/// </summary>
public partial class AgmAttendanceGrid : ComponentBase
{
    [Parameter] public IReadOnlyList<Member> Members { get; set; } = [];

    [Inject] private IStringLocalizer<EventsResource> L { get; set; } = null!;
    [Inject] private ILocalizer Loc { get; set; } = null!;

    private List<AttendanceRow> _rows = [];
    private IReadOnlyList<Member> _lastMembers = [];

    private string RowAriaLabel(string memberName) =>
        Loc.Get<EventsResource>("Events_AgmAttendance_RowAriaLabel", memberName);

    protected override void OnParametersSet()
    {
        if (!ReferenceEquals(_lastMembers, Members))
        {
            _lastMembers = Members;
            _rows = Members
                .OrderBy(m => m.LastName).ThenBy(m => m.FirstName)
                .Select(m => new AttendanceRow { MemberId = m.Id, MemberName = m.SortableFullName })
                .ToList();
        }
    }

    /// <summary>The member ids currently checked as attended.</summary>
    public IReadOnlyList<Guid> GetAttendedMemberIds() =>
        _rows.Where(r => r.Attended).Select(r => r.MemberId).ToList();

    private bool AllAttended => _rows.Count > 0 && _rows.All(r => r.Attended);

    private void ToggleSelectAll(bool value)
    {
        foreach (var row in _rows)
            row.Attended = value;
    }

    private sealed class AttendanceRow
    {
        public Guid MemberId { get; init; }
        public string MemberName { get; init; } = string.Empty;
        public bool Attended { get; set; }
    }
}
