using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Radzen;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.UI.Resources.Strings;

namespace StageFright.UI.Pages.Organisations;

/// <summary>
/// The Organisations register (spec 032, US1, ui-contract §3.1): a name search that filters as the
/// user types, a "show archived" switch, and a grid of name, email and contact count (FR-001, FR-007).
/// </summary>
public partial class OrganisationList : ComponentBase
{
    [Inject] private IExternalOrganisationService OrganisationService { get; set; } = null!;
    [Inject] private NavigationManager Nav { get; set; } = null!;
    [Inject] private IStringLocalizer<OrganisationsResource> L { get; set; } = null!;

    private List<ExternalOrganisation> _organisations = new();
    private string _searchTerm = string.Empty;
    private bool _showArchived;
    private bool _loading = true;

    /// <summary>Rows whose name contains the trimmed search term, ignoring case; every row when blank.</summary>
    private IEnumerable<ExternalOrganisation> DisplayOrganisations
    {
        get
        {
            var term = _searchTerm.Trim();
            return term.Length == 0
                ? _organisations
                : _organisations.Where(o => o.Name.Contains(term, StringComparison.OrdinalIgnoreCase));
        }
    }

    protected override async Task OnInitializedAsync()
    {
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        _loading = true;
        try
        {
            var organisations = (await OrganisationService.GetActiveAsync()).ToList();
            if (_showArchived)
                organisations.AddRange(await OrganisationService.GetArchivedAsync());

            _organisations = organisations
                .OrderBy(o => o.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }
        finally
        {
            _loading = false;
        }
    }

    private void AddOrganisation() => Nav.NavigateTo("/organisations/new");

    private void ViewOrganisation(Guid id) => Nav.NavigateTo($"/organisations/{id}");

    private void OnRowClick(DataGridRowMouseEventArgs<ExternalOrganisation> args)
    {
        if (args.Data is not null)
            ViewOrganisation(args.Data.Id);
    }
}
