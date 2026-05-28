using Microsoft.AspNetCore.Components;
using StageFright.Core.Entities;
using StageFright.Data.Repositories;

namespace StageFright.UI.Pages.Dashboard;

public partial class Dashboard : ComponentBase
{
    private bool IsLoading = true;
    private string? ErrorMessage = null;

    protected override async Task OnInitializedAsync()
    {
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
