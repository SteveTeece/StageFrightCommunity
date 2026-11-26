using Microsoft.Extensions.Localization;
using StageFright.Core.Modules.Localization.Resources;
using StageFright.Plugins.Contracts;

namespace StageFright.Core.Modules.Rehearsals;

/// <summary>
/// Navigation menu provider for the Rehearsals module.
/// DisplayOrder=3 places Rehearsals after Organisations (2) and before Events (4).
/// </summary>
public class RehearsalMenuItemProvider : IMenuItemProvider
{
    private readonly IStringLocalizer<NavigationResource> _localizer;

    public RehearsalMenuItemProvider(IStringLocalizer<NavigationResource> localizer)
    {
        _localizer = localizer;
    }

    public string ModuleName => "Rehearsals";
    public int DisplayOrder => 3;

    public IReadOnlyList<MenuItem> GetMenuItems() =>
        new List<MenuItem>
        {
            new()
            {
                Title = _localizer["Nav_Rehearsals_Title"],
                Route = "/rehearsals",
                Icon = "schedule",
                ShortLabel = _localizer["Nav_Rehearsals_ShortLabel"],
                DisplayOrder = 0
            }
        };
}
