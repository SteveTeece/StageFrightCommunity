using Microsoft.AspNetCore.Components;
using StageFright.Data.Repositories;
using StageFright.Core.Entities;

namespace StageFright.UI.Shared;

public partial class ShellLayout : IAsyncDisposable
{
    [Inject]
    public NavigationManager Navigation { get; set; } = default!;

    [Inject]
    public ISettingsRepository SettingsRepository { get; set; } = default!;

    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    private Settings? Settings { get; set; }

    protected override async Task OnInitializedAsync()
    {
        try
        {
            Settings = await SettingsRepository.GetSettingsAsync();
        }
        catch (Exception ex)
        {
            // Log error, use defaults
            System.Diagnostics.Debug.WriteLine($"Error loading settings: {ex}");
            Settings = new Settings { OrganizationName = "Community" };
        }
    }

    async ValueTask IAsyncDisposable.DisposeAsync()
    {
        // Cleanup if needed
        await ValueTask.CompletedTask;
    }
}
