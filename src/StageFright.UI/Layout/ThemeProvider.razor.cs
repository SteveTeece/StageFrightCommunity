using Microsoft.AspNetCore.Components;
using StageFright.Core.Contracts;
using StageFright.Core.Enums;

namespace StageFright.UI.Layout;

/// <summary>
/// Cascading component that owns the current UI theme.
/// Wraps content in an element with data-bs-theme="light"|"dark" so Bootstrap 5.3
/// applies the correct colour scheme to all descendants.
/// Reads the initial theme from Settings on mount; exposes ToggleAsync to persist changes.
/// </summary>
public partial class ThemeProvider : ComponentBase
{
    [Inject] private ISettingsService SettingsService { get; set; } = null!;

    [Parameter] public RenderFragment? ChildContent { get; set; }

    private Theme _currentTheme = Theme.Light;

    /// <summary>The active theme — read by ShellLayout and GeneralSettingsTab.</summary>
    public Theme CurrentTheme => _currentTheme;

    protected override async Task OnInitializedAsync()
    {
        var settings = await SettingsService.GetAsync();
        _currentTheme = settings?.Theme ?? Theme.Light;
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
