using Microsoft.AspNetCore.Components;
using StageFright.Core.Contracts;
using StageFright.Core.Enums;

namespace StageFright.UI.Layout;

/// <summary>
/// Cascading component that owns the current UI theme.
/// Wraps content in an element with data-bs-theme="light"|"dark" so Bootstrap 5.3
/// applies the correct colour scheme to all descendants.
/// Reads the initial theme from Settings on mount; when no Settings row exists yet
/// (pre-setup), falls back to the device's OS theme preference, defaulting to Dark
/// when that preference is unavailable. Exposes ToggleAsync to persist changes.
/// </summary>
public partial class ThemeProvider : ComponentBase
{
    [Inject] private ISettingsService SettingsService { get; set; } = null!;
    [Inject] private IDeviceThemePreferenceProvider DeviceThemePreferenceProvider { get; set; } = null!;

    [Parameter] public RenderFragment? ChildContent { get; set; }

    private Theme _currentTheme = Theme.Dark;

    /// <summary>The active theme — read by ShellLayout, GeneralSettingsTab, and SetupWizard.</summary>
    public Theme CurrentTheme => _currentTheme;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            var settings = await SettingsService.GetAsync();
            _currentTheme = settings?.Theme ?? FallbackTheme();
        }
        catch (Exception)
        {
            _currentTheme = FallbackTheme();
        }
    }

    private Theme FallbackTheme()
    {
        try
        {
            return DeviceThemePreferenceProvider.GetPreference() switch
            {
                PlatformThemePreference.Light => Theme.Light,
                PlatformThemePreference.Dark => Theme.Dark,
                _ => Theme.Dark
            };
        }
        catch (Exception)
        {
            return Theme.Dark;
        }
    }

    /// <summary>
    /// Toggles the theme between Light and Dark, persists the choice to Settings,
    /// and triggers a re-render so all cascaded consumers update.
    /// </summary>
    public async Task ToggleAsync()
    {
        _currentTheme = _currentTheme == Theme.Light ? Theme.Dark : Theme.Light;

        var settings = await SettingsService.GetAsync();
        if (settings is not null)
        {
            settings.Theme = _currentTheme;
            await SettingsService.SaveAsync(settings);
        }

        StateHasChanged();
    }
}
