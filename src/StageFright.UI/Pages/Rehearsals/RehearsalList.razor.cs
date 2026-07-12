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
    private string _searchTerm = string.Empty;
    private string? _errorMessage;

    private IEnumerable<Rehearsal> DisplayRehearsals =>
        string.IsNullOrWhiteSpace(_searchTerm)
            ? _rehearsals
            : _rehearsals.Where(r =>
                r.Date.ToString("d MMM yyyy").Contains(_searchTerm, StringComparison.OrdinalIgnoreCase) ||
                (r.Notes?.Contains(_searchTerm, StringComparison.OrdinalIgnoreCase) ?? false));

    private const int MaxFutureRehearsals = 3;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            var today = DateTime.Today;
            var result = await RehearsalService.GetAllAsync();

            var futureRehearsals = result
                .Where(r => r.Date.Date >= today)
                .OrderBy(r => r.Date)
                .ThenBy(r => r.Time)
                .Take(MaxFutureRehearsals);

            var pastRehearsals = result
                .Where(r => r.Date.Date < today && r.Date.Year == today.Year);

            _rehearsals = futureRehearsals
                .Concat(pastRehearsals)
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
