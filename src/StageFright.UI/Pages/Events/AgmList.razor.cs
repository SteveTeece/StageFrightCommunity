using Microsoft.AspNetCore.Components;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;

namespace StageFright.UI.Pages.Events;

/// <summary>
/// Browsable list of past AGMs, most-recent-first, with date and attendance count (FR-015).
/// Row click opens the read-only AGM detail view.
/// </summary>
public partial class AgmList : ComponentBase
{
    [Inject] private IAgmService AgmService { get; set; } = null!;
    [Inject] private NavigationManager Nav { get; set; } = null!;

    private List<AnnualGeneralMeeting> _agms = [];
    private bool _loading = true;

    protected override async Task OnInitializedAsync()
    {
        _agms = (await AgmService.GetPastAsync()).ToList();
        _loading = false;
    }

    private static int AttendedCount(AnnualGeneralMeeting agm) =>
        agm.AttendanceRecords.Count(a => a.Attended);

    private void OpenDetail(AnnualGeneralMeeting agm) =>
        Nav.NavigateTo($"/events/agm/{agm.Id}");
}
