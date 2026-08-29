using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using StageFright.Core.Contracts;
using StageFright.Core.Enums;
using StageFright.Core.Exceptions;
using StageFright.Core.Localization;
using StageFright.Core.Modules.Localization;
using StageFright.UI.Layout;
using StageFright.UI.Resources.Strings;
using AppSettings = StageFright.Core.Entities.Settings;

namespace StageFright.UI.Pages.Settings;

public partial class GeneralSettingsTab : ComponentBase
{
    [Inject] private ISettingsService SettingsService { get; set; } = null!;
    [Inject] private ILogger<GeneralSettingsTab> Logger { get; set; } = null!;
    [Inject] private IStringLocalizer<SettingsResource> L { get; set; } = null!;
    [Inject] private IStringLocalizer<SharedResource> Shared { get; set; } = null!;
    [Inject] private ILocalizer Loc { get; set; } = null!;
    [Inject] private ISupportedLanguagesCatalog Languages { get; set; } = null!;

    [CascadingParameter] private ThemeProvider? ThemeProvider { get; set; }

    private AppSettings? _settings;

    private bool _saving;
    private string? _errorMessage;
    private string? _successMessage;

    // Close-period control (spec 028, US6 / FR-016). `_closeThroughDate` is seeded from the
    // persisted value; the closed-through date only moves when the treasurer ticks
    // `_confirmClosePeriod` — an unconfirmed save leaves it untouched.
    private DateTime? _closeThroughDate;
    private bool _confirmClosePeriod;

    // Display-language picker (spec 027, US3). The selection is applied on the next launch
    // (FR-021) — persisted with the rest of the General tab on Save. `_initialLanguageCode`
    // captures the loaded value so the restart notice shows the moment the selection differs.
    // Clearing the explicit choice back to "follow the OS language" (null) is only observable
    // with more than one shipped language and is out of scope while en-AU is the only set.
    private string _selectedLanguageCode = SupportedLanguagesCatalog.DefaultCultureCode;
    private string _initialLanguageCode = SupportedLanguagesCatalog.DefaultCultureCode;

    private bool LanguageChanged =>
        !string.Equals(_selectedLanguageCode, _initialLanguageCode, StringComparison.OrdinalIgnoreCase);

    /// <summary>"{n} year(s)" audit-retention select option, pluralised.</summary>
    private string AuditRetentionOptionText(int years) =>
        Loc.Plural<SettingsResource>("Settings_General_AuditRetentionYears", years);

    /// <summary>"Current: {theme}" label beside the theme toggle.</summary>
    private string ThemeCurrentText(Theme theme) =>
        Loc.Get<SettingsResource>("Settings_General_ThemeCurrent", theme.LocalizeEnum());

    protected override async Task OnInitializedAsync()
    {
        Logger.LogInformation("GeneralSettingsTab.OnInitializedAsync start");

        Logger.LogDebug("GeneralSettingsTab: calling SettingsService.GetAsync");
        try
        {
            _settings = await SettingsService.GetAsync();
            Logger.LogInformation("GeneralSettingsTab: settings loaded. HasSettings={HasSettings}", _settings is not null);

            _initialLanguageCode = _settings?.LanguageCode ?? Languages.Default.CultureCode;
            _selectedLanguageCode = _initialLanguageCode;

            _closeThroughDate = _settings?.ClosedThroughDate;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "GeneralSettingsTab: SettingsService.GetAsync failed");
            _errorMessage = Loc.Get<SettingsResource>("Settings_Common_LoadError", ex.Message);
            return;
        }

        Logger.LogInformation("GeneralSettingsTab.OnInitializedAsync complete");
    }

    private async Task HandleSaveAsync()
    {
        if (_settings is null) return;

        _saving = true;
        _errorMessage = null;
        _successMessage = null;

        try
        {
            // Merge in the fields owned by the Sales Tax tab, so a stale in-memory copy
            // here never clobbers a concurrent tax-applicability save made there.
            var current = await SettingsService.GetAsync();
            if (current is not null)
            {
                _settings.IsTaxApplicable = current.IsTaxApplicable;
                _settings.TaxRate = current.TaxRate;
                _settings.AnnualFeeTaxCode = current.AnnualFeeTaxCode;
                _settings.AttendanceFeeTaxCode = current.AttendanceFeeTaxCode;
                _settings.GeneralCommitteeSeatCountTarget = current.GeneralCommitteeSeatCountTarget;
            }

            _settings.LanguageCode = _selectedLanguageCode;

            // The closed-through date only advances on an explicit confirmation; otherwise the
            // persisted value loaded into _settings is saved back unchanged (spec 028, FR-016).
            if (_confirmClosePeriod && _closeThroughDate is not null)
                _settings.ClosedThroughDate = _closeThroughDate;

            await SettingsService.SaveAsync(_settings);
            _initialLanguageCode = _selectedLanguageCode;
            _successMessage = L["Settings_Common_SaveSuccess"];
        }
        catch (ValidationException ex)
        {
            _errorMessage = ex.Message;
        }
        catch (Exception ex)
        {
            _errorMessage = Loc.Get<SettingsResource>("Settings_Common_SaveError", ex.Message);
        }
        finally
        {
            _saving = false;
        }
    }

    private async Task HandleThemeToggleAsync()
    {
        if (ThemeProvider is not null)
        {
            await ThemeProvider.ToggleAsync();
            // Sync the local settings model so the label stays accurate
            if (_settings is not null)
                _settings.Theme = ThemeProvider.CurrentTheme;
        }
    }

}
