using StageFright.Plugins.Contracts;

namespace StageFright.Core.Modules.Rehearsals;

/// <summary>
/// Navigation menu provider for the Rehearsals module.
/// Appears second in the nav bar (DisplayOrder=2), after Members (1) and before Events (3).
/// </summary>
public class RehearsalMenuItemProvider : IMenuItemProvider
{
    public string ModuleName => "Rehearsals";
    public int DisplayOrder => 2;

    public IReadOnlyList<MenuItem> GetMenuItems() =>
        new List<MenuItem>
        {
            new()
            {
                Title = "Rehearsals",
                Route = "/rehearsals",
                Icon = "schedule",
                ShortLabel = "REH",
                DisplayOrder = 0,
                SubItems = new List<MenuItem>
                {
                    new() { Title = "All Rehearsals", Route = "/rehearsals", DisplayOrder = 0 },
                    new() { Title = "Schedule Rehearsal", Route = "/rehearsals/new", DisplayOrder = 1 }
                }
            }
        };
}
