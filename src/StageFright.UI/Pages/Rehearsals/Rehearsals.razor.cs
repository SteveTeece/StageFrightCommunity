using Microsoft.AspNetCore.Components;
using StageFright.Data.Repositories;
using StageFright.Core.Entities;

namespace StageFright.UI.Pages.Rehearsals;

public partial class Rehearsals
{
    [Inject]
    public IRehearsalRepository RehearsalRepository { get; set; } = default!;

    [Inject]
    public IMemberRepository MemberRepository { get; set; } = default!;

    private List<Rehearsal> RehearsalList = new();
    private bool IsLoading = true;
    private bool ShowScheduleForm = false;
    private bool ShowAttendanceRecorder = false;
    private Guid SelectedRehearsalId = Guid.Empty;
    private string? ErrorMessage = null;

    protected override async Task OnInitializedAsync()
    {
        await LoadRehearsals();
    }

    private async Task LoadRehearsals()
    {
        try
        {
            IsLoading = true;
            ErrorMessage = null;

            var rehearsals = await RehearsalRepository.GetAllAsync();
            RehearsalList = rehearsals.OrderByDescending(r => r.Date).ToList();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error loading rehearsals: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void OpenScheduleForm() => ShowScheduleForm = true;
    private void HideScheduleForm() => ShowScheduleForm = false;
    private void HideAttendanceRecorder() => ShowAttendanceRecorder = false;

    private async Task RehearsalScheduled()
    {
        HideScheduleForm();
        await LoadRehearsals();
    }

    private async Task AttendanceRecorded()
    {
        HideAttendanceRecorder();
        await LoadRehearsals();
    }

    private void RecordAttendance(Guid rehearsalId)
    {
        SelectedRehearsalId = rehearsalId;
        ShowAttendanceRecorder = true;
    }
}
