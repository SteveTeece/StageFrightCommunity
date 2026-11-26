using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using StageFright.UI.Resources.Strings;

namespace StageFright.UI.Pages.Organisations;

/// <summary>
/// The Details / Contacts / History tab strip shared by the three organisation detail pages
/// (spec 032, ui-contract §3.3). Builds its links from <see cref="Id"/> and marks the tab named by
/// <see cref="Active"/>. Loads no data.
/// </summary>
public partial class OrganisationTabs : ComponentBase
{
    /// <summary>Tab key for the Details tab (<c>/organisations/{Id}</c>).</summary>
    public const string DetailsTab = "details";

    /// <summary>Tab key for the Contacts tab (<c>/organisations/{Id}/contacts</c>).</summary>
    public const string ContactsTab = "contacts";

    /// <summary>Tab key for the History tab (<c>/organisations/{Id}/history</c>).</summary>
    public const string HistoryTab = "history";

    /// <summary>The organisation the links point at.</summary>
    [Parameter, EditorRequired] public Guid Id { get; set; }

    /// <summary>The current tab: <c>"details"</c>, <c>"contacts"</c> or <c>"history"</c>.</summary>
    [Parameter, EditorRequired] public string Active { get; set; } = DetailsTab;

    [Inject] private IStringLocalizer<OrganisationsResource> L { get; set; } = null!;

    private string DetailsHref => $"/organisations/{Id}";

    private string ContactsHref => $"/organisations/{Id}/contacts";

    private string HistoryHref => $"/organisations/{Id}/history";

    private bool IsActive(string tab) => string.Equals(Active, tab, StringComparison.Ordinal);

    private string LinkClass(string tab) => IsActive(tab) ? "nav-link active" : "nav-link";

    /// <summary>Returns <c>"page"</c> for the active tab; null omits the attribute on the others.</summary>
    private string? AriaCurrent(string tab) => IsActive(tab) ? "page" : null;
}
