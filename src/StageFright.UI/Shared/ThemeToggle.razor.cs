using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using StageFright.Data.Repositories;
using StageFright.Core.Entities;

namespace StageFright.UI.Shared;

public partial class ThemeToggle
{
    [Inject]
    public ISettingsRepository SettingsRepository { get; set; } = default!;

    [Inject]
    public IJSRuntime JS { get; set; } = default!;

    private bool IsDarkMode { get; set; } = true;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            var settings = await SettingsRepository.GetSettingsAsync();
            IsDarkMode = settings?.Theme == "Dark";
        }
        catch
        {
            IsDarkMode = true; // Default to dark
        }
    }

    private async Task ToggleTheme()
    {
        try
        {
            var settings = await SettingsRepository.GetSettingsAsync();
            IsDarkMode = !IsDarkMode;
            settings.Theme = IsDarkMode ? "Dark" : "Light";
            await SettingsRepository.UpdateSettingsAsync(settings);
            
            // Apply theme to document
            await JS.InvokeVoidAsync("applyTheme", IsDarkMode ? "dark" : "light");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error toggling theme: {ex}");
        }
    }
}
