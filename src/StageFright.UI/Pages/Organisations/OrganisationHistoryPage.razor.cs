using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Modules.Organisations;
using StageFright.UI.Resources.Strings;

namespace StageFright.UI.Pages.Organisations;

/// <summary>
/// The History tab of an organisation (spec 032, US3, ui-contract §3.7): every linked expense and
/// income entry newest first, with the total paid and total received (FR-011, FR-012). Amounts go
/// through <see cref="StageFright.Core.Localization.MoneyFormatter"/> (FR-013). Works for archived
/// organisations, whose history stays viewable.
/// </summary>
public partial class OrganisationHistoryPage : ComponentBase
{
    [Parameter] public Guid Id { get; set; }

    [Inject] private IExternalOrganisationService OrganisationService { get; set; } = null!;
    [Inject] private IOrganisationHistoryService HistoryService { get; set; } = null!;
    [Inject] private IStringLocalizer<OrganisationsResource> L { get; set; } = null!;
    [Inject] private IStringLocalizer<SharedResource> Shared { get; set; } = null!;

    private ExternalOrganisation? _organisation;
    private OrganisationHistory? _history;
    private string? _error;
    private bool _loading = true;

    protected override async Task OnParametersSetAsync()
    {
        _loading = true;
        _error = null;
        _organisation = null;
        _history = null;
        try
        {
            _organisation = await OrganisationService.GetByIdAsync(Id);
            if (_organisation is not null)
                _history = await HistoryService.GetHistoryAsync(Id);
        }
        catch (Exception)
        {
            _error = L["Organisations_Error_Unexpected"];
        }
        finally
        {
            _loading = false;
        }
    }
}
