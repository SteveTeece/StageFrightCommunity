using Microsoft.AspNetCore.Components;
using StageFright.Data.Repositories;
using StageFright.Core.Entities;

namespace StageFright.UI.Pages.Rehearsals;

public partial class ScheduleRehearsalForm
{
    [Parameter]
    public EventCallback OnSaved { get; set; }

    [Parameter]
    public EventCallback OnCancelled { get; set; }

    [Inject]
    public IRehearsalRepository RehearsalRepository { get; set; } = default!;

    private string DateString = DateTime.UtcNow.Date.ToString("yyyy-MM-dd");
    private string TimeString = "19:00";
    private string Notes = "";
    private string? ErrorMessage = null;

    private async Task ScheduleRehearsal()
    {
        try
        {
            ErrorMessage = null;

            if (!DateTime.TryParse(DateString, out var date))
            {
                ErrorMessage = "Invalid date.";
                return;
            }

            if (!TimeSpan.TryParse(TimeString, out var time))
            {
                ErrorMessage = "Invalid time.";
                return;
            }

            var rehearsal = new Rehearsal
            {
                Id = Guid.NewGuid(),
                Date = date,
                Time = time,
                Notes = Notes,
                StoredAttendanceRate = 0
            };

            await RehearsalRepository.CreateAsync(rehearsal);
            await OnSaved.InvokeAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error scheduling rehearsal: {ex.Message}";
            System.Diagnostics.Debug.WriteLine($"Error: {ex}");
        }
    }

    private async Task Cancel()
    {
        await OnCancelled.InvokeAsync();
    }
}
