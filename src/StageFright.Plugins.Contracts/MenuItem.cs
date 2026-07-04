namespace StageFright.Plugins.Contracts;

/// <summary>A single navigation item in the shell menu, optionally with sub-items.</summary>
public class MenuItem
{
    /// <summary>Display text shown in the nav bar.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Blazor route navigated via NavigationManager. Prefixes are module-owned.</summary>
    public string Route { get; set; } = string.Empty;

    /// <summary>Optional icon name or CSS class.</summary>
    public string? Icon { get; set; }

    /// <summary>
    /// Optional short label (≤4 chars, e.g. "MEMB") for compact navigation surfaces.
    /// The current sidebar shows the full <see cref="Title"/> and ignores this.
    /// </summary>
    public string? ShortLabel { get; set; }

    /// <summary>Sort order within the module's menu items.</summary>
    public int DisplayOrder { get; set; }

    /// <summary>
    /// Nested sub-items. The shell sidebar renders top-level items only and
    /// ignores this list; it is retained for plugin compatibility.
    /// </summary>
    public List<MenuItem> SubItems { get; set; } = new();

    /// <summary>Optional badge text (e.g., count of pending items). Null = no badge.</summary>
    public string? BadgeText { get; set; }
}
