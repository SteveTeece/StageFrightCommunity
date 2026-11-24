using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Exceptions;
using StageFright.UI.Resources.Strings;

namespace StageFright.UI.Pages.Organisations;

/// <summary>
/// The Contacts tab of an organisation (spec 032, US2, ui-contract §3.5): a grid of its contacts
/// (FR-008) with an inline <see cref="ContactEditor"/> for add and edit, and Remove as a soft delete
/// (FR-009). Add, edit and remove are offered only while the organisation is active.
/// </summary>
public partial class OrganisationContacts : ComponentBase
{
    [Parameter] public Guid Id { get; set; }

    [Inject] private IExternalOrganisationService OrganisationService { get; set; } = null!;
    [Inject] private IExternalOrganisationContactService ContactService { get; set; } = null!;
    [Inject] private IStringLocalizer<OrganisationsResource> L { get; set; } = null!;
    [Inject] private IStringLocalizer<SharedResource> Shared { get; set; } = null!;

    private ExternalOrganisation? _organisation;
    private string? _error;
    private bool _loading = true;

    // The inline editor: open or closed, the contact it edits (null for add), and a fresh key per open
    // so each open starts from clean fields.
    private bool _editorOpen;
    private ExternalOrganisationContact? _editing;
    private Guid _editorKey;

    private bool CanEdit => _organisation is { IsDeleted: false };

    protected override async Task OnParametersSetAsync()
    {
        _loading = true;
        _error = null;
        _editorOpen = false;
        _organisation = await OrganisationService.GetByIdAsync(Id);
        _loading = false;
    }

    private void OpenEditor(ExternalOrganisationContact? contact)
    {
        _error = null;
        _editing = contact;
        _editorKey = Guid.NewGuid();
        _editorOpen = true;
    }

    private void CloseEditor() => _editorOpen = false;

    private async Task OnContactSavedAsync()
    {
        _editorOpen = false;
        _organisation = await OrganisationService.GetByIdAsync(Id);
    }

    private async Task RemoveAsync(ExternalOrganisationContact contact)
    {
        _error = null;
        try
        {
            await ContactService.RemoveAsync(contact.Id);
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
    }
}
