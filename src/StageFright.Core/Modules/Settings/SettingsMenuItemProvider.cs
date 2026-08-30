using Microsoft.Extensions.Localization;
using StageFright.Core.Modules.Localization.Resources;
using StageFright.Plugins.Contracts;

namespace StageFright.Core.Modules.Settings;

/// <summary>
/// Contributes the Settings navigation item as the last entry in the main menu bar.
/// DisplayOrder=999 ensures Settings always appears after all other module providers.
/// </summary>
public class SettingsMenuItemProvider : IMenuItemProvider
{
    private readonly IStringLocalizer<NavigationResource> _localizer;

    public SettingsMenuItemProvider(IStringLocalizer<NavigationResource> localizer)
    {
        _localizer = localizer;
    }

    public string ModuleName => "Settings";
    public int DisplayOrder => 999;

    public IReadOnlyList<MenuItem> GetMenuItems() =>
    [
        new MenuItem
        {
            Title = _localizer["Nav_Settings_Title"],
            Route = "/settings",
            ShortLabel = _localizer["Nav_Settings_ShortLabel"],
            DisplayOrder = 0
        }
    ];
}
