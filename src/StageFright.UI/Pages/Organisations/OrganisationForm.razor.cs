using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using StageFright.Core.Contracts;
using StageFright.Core.Exceptions;
using StageFright.Core.Modules.Organisations;
using StageFright.UI.Resources.Strings;

namespace StageFright.UI.Pages.Organisations;

/// <summary>
/// Add or edit an external organisation (spec 032, US1, ui-contract §3.2). Field-level messages come
/// from <see cref="ExternalOrganisationValidationService"/> before the service is called (FR-004);
/// service refusals such as a duplicate name (FR-003) show in the form's error banner.
/// </summary>
public partial class OrganisationForm : ComponentBase
{
    /// <summary>The organisation being edited; null on the new route.</summary>
    [Parameter] public Guid? Id { get; set; }

    [Inject] private IExternalOrganisationService OrganisationService { get; set; } = null!;
    [Inject] private ExternalOrganisationValidationService Validation { get; set; } = null!;
    [Inject] private NavigationManager Nav { get; set; } = null!;
    [Inject] private IStringLocalizer<OrganisationsResource> L { get; set; } = null!;
    [Inject] private IStringLocalizer<SharedResource> Shared { get; set; } = null!;

    private readonly SaveExternalOrganisationRequest _request = new();
    private Dictionary<string, string> _errors = new();
    private string? _formError;
    private bool _loading = true;
    private bool _notFound;
    private bool _saving;

    private bool IsNew => Id is null;

    private string Heading => IsNew ? L["Organisations_Form_PageTitleNew"] : L["Organisations_Form_PageTitleEdit"];

    protected override async Task OnInitializedAsync()
    {
        if (!IsNew)
        {
            var organisation = await OrganisationService.GetByIdAsync(Id!.Value);
            if (organisation is null)
            {
                _notFound = true;
            }
            else
            {
                _request.Name = organisation.Name;
                _request.Address = organisation.Address;
                _request.Email = organisation.Email;
            }
        }

        _loading = false;
    }

    private async Task SaveAsync()
    {
        _formError = null;
        _errors = new Dictionary<string, string>(Validation.GetErrors(_request));
        if (_errors.Count > 0)
            return;

        _saving = true;
        try
        {
            if (IsNew)
            {
                var created = await OrganisationService.CreateAsync(_request);
                Nav.NavigateTo($"/organisations/{created.Id}");
            }
            else
            {
                await OrganisationService.UpdateAsync(Id!.Value, _request);
                Nav.NavigateTo($"/organisations/{Id}");
            }
        }
        catch (ValidationException ex)
        {
            _formError = ex.Message;
        }
        catch (Exception)
        {
            _formError = L["Organisations_Error_Unexpected"];
        }
        finally
        {
            _saving = false;
        }
    }

    private void Cancel() =>
        Nav.NavigateTo(IsNew ? "/organisations" : $"/organisations/{Id}");

    /// <summary>The input's CSS classes, adding <c>is-invalid</c> when the field has a message.</summary>
    private string InputClass(string field) =>
        _errors.ContainsKey(field) ? "form-control form-control-sm is-invalid" : "form-control form-control-sm";
}
