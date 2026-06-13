using Microsoft.AspNetCore.Components;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;

namespace StageFright.UI.Pages.Rehearsals;

public partial class RehearsalList
{
    [Inject] private IRehearsalService RehearsalService { get; set; } = null!;
    [Inject] private NavigationManager Nav { get; set; } = null!;

    private bool _loading = true;
    private List<Rehearsal> _rehearsals = new();
    private string? _errorMessage;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            var result = await RehearsalService.GetAllAsync();
            _rehearsals = result
                .OrderByDescending(r => r.Date)
                .ThenByDescending(r => r.Time)
                .ToList();
        }
        catch (Exception ex)
        {
            _errorMessage = $"Failed to load rehearsals: {ex.Message}";
        }
        finally
        {
            _loading = false;
        }
    }

    private void AddRehearsal() => Nav.NavigateTo("/rehearsals/new");

    private void RecordAttendance(Guid rehearsalId) =>
        Nav.NavigateTo($"/rehearsals/{rehearsalId}/attendance");
}
