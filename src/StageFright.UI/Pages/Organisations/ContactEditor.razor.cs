using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Exceptions;
using StageFright.Core.Modules.Organisations;
using StageFright.UI.Resources.Strings;

namespace StageFright.UI.Pages.Organisations;

/// <summary>
/// The inline add/edit form for one contact on the Contacts tab (spec 032, US2, ui-contract §3.6).
/// Field-level messages come from <see cref="ExternalOrganisationValidationService"/> before the
/// service is called (FR-004/FR-008); service refusals show in the editor banner. The parent gives
/// each open editor its own <c>@key</c>, so the fields are seeded once, on initialisation.
/// </summary>
public partial class ContactEditor : ComponentBase
{
    /// <summary>The organisation a new contact is added to.</summary>
    [Parameter, EditorRequired] public Guid OrganisationId { get; set; }

    /// <summary>The contact being edited; null when adding.</summary>
    [Parameter] public ExternalOrganisationContact? Contact { get; set; }

    /// <summary>Raised after a successful add or update.</summary>
    [Parameter] public EventCallback OnSaved { get; set; }

    /// <summary>Raised by the Cancel button.</summary>
    [Parameter] public EventCallback OnCancelled { get; set; }

    [Inject] private IExternalOrganisationContactService ContactService { get; set; } = null!;
    [Inject] private ExternalOrganisationValidationService Validation { get; set; } = null!;
    [Inject] private IStringLocalizer<OrganisationsResource> L { get; set; } = null!;

    private readonly SaveExternalOrganisationContactRequest _request = new();
    private Dictionary<string, string> _errors = new();
    private string? _formError;
    private bool _saving;

    protected override void OnInitialized()
    {
        if (Contact is null)
            return;

        _request.Name = Contact.Name;
        _request.Role = Contact.Role;
        _request.Phone = Contact.Phone;
        _request.Mobile = Contact.Mobile;
        _request.Email = Contact.Email;
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
            if (Contact is null)
                await ContactService.AddAsync(OrganisationId, _request);
            else
                await ContactService.UpdateAsync(Contact.Id, _request);

            await OnSaved.InvokeAsync();
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

    /// <summary>The input's CSS classes, adding <c>is-invalid</c> when the field has a message.</summary>
    private string InputClass(string field) =>
        _errors.ContainsKey(field) ? "form-control form-control-sm is-invalid" : "form-control form-control-sm";
}
