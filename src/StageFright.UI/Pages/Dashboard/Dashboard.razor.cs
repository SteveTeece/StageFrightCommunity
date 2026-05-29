using Microsoft.AspNetCore.Components;
using StageFright.Core.Entities;
using StageFright.Core.Services;
using StageFright.Data.Repositories;

namespace StageFright.UI.Pages.Dashboard;

public partial class Dashboard : ComponentBase
{
    private bool IsLoading = true;
    private string? ErrorMessage = null;

    [Inject]
    public IAppInitializationService InitService { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        // Ensure database is initialized before loading dashboard
        // This is a safety guard - the Index page should have already waited for this
        try
        {
            await InitService.WaitForInitializationAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Application initialization failed: {ex.Message}";
            IsLoading = false;
            return;
        }

        await LoadDashboard();
    }

    private async Task LoadDashboard()
    {
        try
        {
            IsLoading = true;
            ErrorMessage = null;
            // Dashboard tile components handle their own loading
            await Task.Delay(100); // Minimal initialization time
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
