using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Exceptions;
using StageFright.UI.Resources.Strings;

namespace StageFright.UI.Pages.Organisations;

/// <summary>
/// The Details tab of an organisation (spec 032, US1, ui-contract §3.4): name, multi-line address
/// and email, plus Edit and Archive while active, or Restore while archived (FR-006). A refused
/// restore (the name is now taken) shows in the error banner.
/// </summary>
public partial class OrganisationDetail : ComponentBase
{
    [Parameter] public Guid Id { get; set; }

    [Inject] private IExternalOrganisationService OrganisationService { get; set; } = null!;
    [Inject] private NavigationManager Nav { get; set; } = null!;
    [Inject] private IStringLocalizer<OrganisationsResource> L { get; set; } = null!;
    [Inject] private IStringLocalizer<SharedResource> Shared { get; set; } = null!;

    private ExternalOrganisation? _organisation;
    private string? _error;
    private bool _loading = true;
    private bool _busy;

    protected override async Task OnParametersSetAsync()
    {
        _loading = true;
        _error = null;
        _organisation = await OrganisationService.GetByIdAsync(Id);
        _loading = false;
    }

    private void Edit() => Nav.NavigateTo($"/organisations/{Id}/edit");

    private Task ArchiveAsync() => RunAsync(() => OrganisationService.ArchiveAsync(Id));

    private Task RestoreAsync() => RunAsync(() => OrganisationService.RestoreAsync(Id));

    /// <summary>Runs an archive or restore, then reloads; a refusal leaves the page as it was.</summary>
    private async Task RunAsync(Func<Task> action)
    {
        _error = null;
        _busy = true;
        try
        {
            await action();
            _organisation = await OrganisationService.GetByIdAsync(Id);
        }
        catch (ValidationException ex)
        {
            _error = ex.Message;
        }
        catch (Exception)
        {
            _error = L["Organisations_Error_Unexpected"];
        }
        finally
        {
            _busy = false;
        }
    }
}
