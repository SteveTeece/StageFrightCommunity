using Microsoft.AspNetCore.Components;

namespace StageFright.UI.Pages.Settings;

public partial class EventTypesTab
{
    private bool IsLoading = false;

    protected override async Task OnInitializedAsync()
    {
        await Task.Delay(100); // Simulate loading
    }
}
