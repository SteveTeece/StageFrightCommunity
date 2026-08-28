using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using StageFright.Core.Contracts;
using StageFright.Core.Enums;
using StageFright.Core.Exceptions;
using StageFright.Core.Localization;
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

    [CascadingParameter] private ThemeProvider? ThemeProvider { get; set; }

    private AppSettings? _settings;

    private bool _saving;
    private string? _errorMessage;
    private string? _successMessage;

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

            await SettingsService.SaveAsync(_settings);
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
