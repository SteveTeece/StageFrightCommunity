using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Exceptions;
using StageFright.Core.Localization;
using StageFright.UI.Resources.Strings;
using AppSettings = StageFright.Core.Entities.Settings;

namespace StageFright.UI.Pages.Settings;

/// <summary>
/// 5th hardcoded core Settings tab (research D6 — not ISettingsTabProvider, matching the
/// other 4 core tabs' OnClick/lazy-render pattern to avoid the documented MAUI WebView
/// concurrent-DbContext gotcha). Manages committee office-holder titles (FR-012/FR-013)
/// and the general-committee seat-count target (FR-014).
/// </summary>
public partial class CommitteeSettingsTab : ComponentBase
{
    [Inject] private ICommitteeOfficeHolderTypeService OfficeHolderTypeService { get; set; } = null!;
    [Inject] private ISettingsService SettingsService { get; set; } = null!;
    [Inject] private ILogger<CommitteeSettingsTab> Logger { get; set; } = null!;
    [Inject] private IStringLocalizer<SettingsResource> L { get; set; } = null!;
    [Inject] private IStringLocalizer<SharedResource> Shared { get; set; } = null!;
    [Inject] private ILocalizer Loc { get; set; } = null!;

    private bool _loading = true;
    private bool _creating;
    private bool _savingSeatCount;
    private string? _errorMessage;
    private string? _successMessage;

    private List<CommitteeOfficeHolderType> _activeTypes = new();
    private List<CommitteeOfficeHolderType> _archivedTypes = new();
    private AppSettings? _settings;
    private int? _seatCountTarget;

    private NewOfficeHolderTypeModel _newModel = new();

    private string ArchiveAriaLabel(string name) =>
        Loc.Get<SettingsResource>("Settings_Committee_ArchiveAriaLabel", name);

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
            var all = await OfficeHolderTypeService.GetActiveAsync();
            _activeTypes = all.OrderByDescending(t => t.IsBuiltIn).ThenBy(t => t.DisplayOrder).ToList();

            _settings = await SettingsService.GetAsync();
            _seatCountTarget = _settings?.GeneralCommitteeSeatCountTarget;
        }
        catch (Exception ex)
        {
            _errorMessage = Loc.Get<SettingsResource>("Settings_Committee_LoadError", ex.Message);
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
            var created = await OfficeHolderTypeService.AddAsync(_newModel.Name!);
            _successMessage = Loc.Get<SettingsResource>("Settings_Committee_TitleCreated", created.Name);
            _newModel = new NewOfficeHolderTypeModel();
            await LoadAsync();
        }
        catch (ValidationException ex)
        {
            _errorMessage = ex.Message;
        }
        catch (Exception ex)
        {
            _errorMessage = Loc.Get<SettingsResource>("Settings_Committee_CreateError", ex.Message);
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
            await OfficeHolderTypeService.ArchiveAsync(id, "coordinator");
            _successMessage = L["Settings_Committee_TitleArchived"];
            await LoadAsync();
        }
        catch (ValidationException ex)
        {
            _errorMessage = ex.Message;
        }
        catch (Exception ex)
        {
            _errorMessage = Loc.Get<SettingsResource>("Settings_Committee_ArchiveError", ex.Message);
        }
    }

    private async Task HandleSaveSeatCountAsync()
    {
        _savingSeatCount = true;
        _errorMessage = null;
        _successMessage = null;

        try
        {
            // Merge in every field NOT owned by this tab, so a stale in-memory copy of
            // (e.g.) the General or GST tab's fields never clobbers a concurrent save made there.
            var current = await SettingsService.GetAsync();
            if (current is null) return;

            current.GeneralCommitteeSeatCountTarget = _seatCountTarget;
            await SettingsService.SaveAsync(current);
            _settings = current;
            _successMessage = L["Settings_Committee_SeatTargetSaved"];
        }
        catch (ValidationException ex)
        {
            _errorMessage = ex.Message;
        }
        catch (Exception ex)
        {
            _errorMessage = Loc.Get<SettingsResource>("Settings_Committee_SeatTargetError", ex.Message);
        }
        finally
        {
            _savingSeatCount = false;
        }
    }

    private sealed class NewOfficeHolderTypeModel
    {
        public string? Name { get; set; }
    }
}
