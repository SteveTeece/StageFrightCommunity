using Microsoft.Extensions.Localization;
using StageFright.Core.Modules.Localization.Resources;
using StageFright.Plugins.Contracts;

namespace StageFright.Core.Modules.Organisations;

/// <summary>
/// Contributes the top-level Organisations navigation item (spec 032, FR-001).
/// DisplayOrder=2 places Organisations directly after Members (1) and before Rehearsals (3).
/// </summary>
public class OrganisationsMenuItemProvider : IMenuItemProvider
{
    private readonly IStringLocalizer<NavigationResource> _localizer;

    public OrganisationsMenuItemProvider(IStringLocalizer<NavigationResource> localizer)
    {
        _localizer = localizer;
    }

    public string ModuleName => "Organisations";
    public int DisplayOrder => 2;

    public IReadOnlyList<MenuItem> GetMenuItems() =>
    [
        new MenuItem
        {
            Title = _localizer["Nav_Organisations_Title"],
            Route = "/organisations",
            ShortLabel = _localizer["Nav_Organisations_ShortLabel"],
            DisplayOrder = 0
        }
    ];
}
