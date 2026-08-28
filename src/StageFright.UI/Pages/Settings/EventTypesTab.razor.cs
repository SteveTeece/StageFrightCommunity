using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Localization;
using StageFright.UI.Resources.Strings;
using CoreValidationException = StageFright.Core.Exceptions.ValidationException;

namespace StageFright.UI.Pages.Settings;

public partial class EventTypesTab : ComponentBase
{
    [Inject] private IEventTypeService EventTypeService { get; set; } = null!;
    [Inject] private IStringLocalizer<SettingsResource> L { get; set; } = null!;
    [Inject] private IStringLocalizer<SharedResource> Shared { get; set; } = null!;
    [Inject] private ILocalizer Loc { get; set; } = null!;

    private bool _loading = true;
    private bool _creating;
    private string? _errorMessage;
    private string? _successMessage;

    private List<EventType> _activeTypes = new();
    private List<EventType> _archivedTypes = new();

    private NewEventTypeModel _newModel = new();

    private string ArchiveAriaLabel(string name) =>
        Loc.Get<SettingsResource>("Settings_EventTypes_ArchiveAriaLabel", name);

    private string RestoreAriaLabel(string name) =>
        Loc.Get<SettingsResource>("Settings_EventTypes_RestoreAriaLabel", name);

    protected override async Task OnInitializedAsync()
    {
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        _loading = true;
        _errorMessage = null;
        try
        {
            var all = await EventTypeService.GetAllAsync();
            _activeTypes = all.OrderBy(et => et.IsSystemDefault ? 0 : 1).ThenBy(et => et.Name).ToList();
            _archivedTypes = (await EventTypeService.GetArchivedAsync()).OrderBy(et => et.Name).ToList();
        }
        catch (Exception ex)
        {
            _errorMessage = Loc.Get<SettingsResource>("Settings_EventTypes_LoadError", ex.Message);
        }
        finally
        {
            _loading = false;
        }
    }

    private async Task HandleCreateAsync()
    {
        _creating = true;
        _errorMessage = null;
        _successMessage = null;

        try
        {
            var created = await EventTypeService.CreateAsync(_newModel.Name!);
            _successMessage = Loc.Get<SettingsResource>("Settings_EventTypes_Created", created.Name);
            _newModel = new NewEventTypeModel();
            await LoadAsync();
        }
        catch (CoreValidationException ex)
        {
            _errorMessage = ex.Message;
        }
        catch (Exception ex)
        {
            _errorMessage = Loc.Get<SettingsResource>("Settings_EventTypes_CreateError", ex.Message);
        }
        finally
        {
            _creating = false;
        }
    }

    private async Task ArchiveAsync(Guid id)
    {
        _errorMessage = null;
        _successMessage = null;

        try
        {
            await EventTypeService.ArchiveAsync(id);
            _successMessage = L["Settings_EventTypes_Archived"];
            await LoadAsync();
        }
        catch (CoreValidationException ex)
        {
            _errorMessage = ex.Message;
        }
        catch (Exception ex)
        {
            _errorMessage = Loc.Get<SettingsResource>("Settings_EventTypes_ArchiveError", ex.Message);
        }
    }

    private async Task RestoreAsync(Guid id)
    {
        _errorMessage = null;
        _successMessage = null;

        try
        {
            await EventTypeService.RestoreAsync(id);
            _successMessage = L["Settings_EventTypes_Restored"];
            await LoadAsync();
        }
        catch (Exception ex)
        {
            _errorMessage = Loc.Get<SettingsResource>("Settings_EventTypes_RestoreError", ex.Message);
        }
    }

    private sealed class NewEventTypeModel
    {
        [Required(ErrorMessage = "Event type name is required.")]
        [MaxLength(100, ErrorMessage = "Event type name must be 100 characters or fewer.")]
        public string? Name { get; set; }
    }
}
