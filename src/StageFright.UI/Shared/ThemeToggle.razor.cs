using Microsoft.AspNetCore.Components;
using StageFright.Core.Services;

namespace StageFright.UI.Shared;

public partial class ThemeToggle
{
    [Inject]
    public IThemeService? ThemeService { get; set; }

    private bool IsDarkMode { get; set; } = true;

    protected override async Task OnInitializedAsync()
    {
        if (ThemeService == null)
            return;

        try
        {
            IsDarkMode = ThemeService.CurrentTheme == "Dark";
            ThemeService.ThemeChanged += OnThemeChanged;
        }
        catch
        {
            IsDarkMode = true; // Default to dark
        }
    }

    private async Task ToggleTheme()
    {
        if (ThemeService == null)
            return;

        try
        {
            await ThemeService.ToggleThemeAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error toggling theme: {ex}");
        }
    }

    private void OnThemeChanged(object? sender, ThemeChangedEventArgs args)
    {
        IsDarkMode = args.NewTheme == "Dark";
        StateHasChanged();
    }

    public void Dispose()
    {
        if (ThemeService != null)
        {
            ThemeService.ThemeChanged -= OnThemeChanged;
        }
    }
}
