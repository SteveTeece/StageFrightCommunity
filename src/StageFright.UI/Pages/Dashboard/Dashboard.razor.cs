using Microsoft.AspNetCore.Components;
using StageFright.Core.Entities;
using StageFright.Data.Repositories;

namespace StageFright.UI.Pages.Dashboard;

public partial class Dashboard : ComponentBase
{
    [Inject]
    public required IMemberRepository MemberRepository { get; set; }

    [Inject]
    public required IRehearsalRepository RehearsalRepository { get; set; }

    [Inject]
    public required IEventRepository EventRepository { get; set; }

    [Inject]
    public required ISettingsRepository SettingsRepository { get; set; }

    private bool IsLoading = true;
    private string? ErrorMessage = null;

    private int ActiveMembersCount = 0;
    private int InactiveMembersCount = 0;
    private Rehearsal? MostRecentRehearsal = null;
    private int TotalRehearsalsCount = 0;
    private Event? MostRecentEvent = null;
    private int TotalEventsCount = 0;
    private decimal OutstandingBalance = 0;

    protected override async Task OnInitializedAsync()
    {
        await LoadDashboardData();
    }

    private async Task LoadDashboardData()
    {
        try
        {
            IsLoading = true;
            ErrorMessage = null;

            // Load member counts
            var allMembers = await MemberRepository.GetAllAsync();
            ActiveMembersCount = allMembers.Count(m => m.Status == "Active");
            InactiveMembersCount = allMembers.Count(m => m.Status == "Inactive");

            // Load rehearsal data
            var rehearsals = await RehearsalRepository.GetAllAsync();
            TotalRehearsalsCount = rehearsals.Count();
            MostRecentRehearsal = rehearsals.OrderByDescending(r => r.Date).FirstOrDefault();

            // Load event data
            var events = await EventRepository.GetAllAsync();
            TotalEventsCount = events.Count();
            MostRecentEvent = events.OrderByDescending(e => e.Date).FirstOrDefault();

            // TODO: Calculate outstanding balance from financial data
            OutstandingBalance = 0;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error loading dashboard: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }
}
