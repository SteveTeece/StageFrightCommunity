using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using StageFright.Core.Contracts;
using StageFright.Core.Exceptions;
using StageFright.UI.Layout;
using AppSettings = StageFright.Core.Entities.Settings;

namespace StageFright.UI.Pages.Settings;

public partial class GeneralSettingsTab : ComponentBase
{
    [Inject] private ISettingsService SettingsService { get; set; } = null!;
    [Inject] private IServiceProvider ServiceProvider { get; set; } = null!;
    [Inject] private ILogger<GeneralSettingsTab> Logger { get; set; } = null!;

    [CascadingParameter] private ThemeProvider? ThemeProvider { get; set; }

    private AppSettings? _settings;
    private ICommitteeAnnualResetService? _committeeResetService;

    private bool _saving;
    private bool _resetting;
    private string? _errorMessage;
    private string? _successMessage;
    private string? _agmBanner;

    protected override async Task OnInitializedAsync()
    {
        Logger.LogInformation("GeneralSettingsTab.OnInitializedAsync start");

        try
        {
            Logger.LogDebug("GeneralSettingsTab: resolving ICommitteeAnnualResetService via IServiceProvider");
            _committeeResetService = ServiceProvider.GetService(typeof(ICommitteeAnnualResetService))
                as ICommitteeAnnualResetService;
            Logger.LogDebug("GeneralSettingsTab: ICommitteeAnnualResetService resolved={Found}", _committeeResetService is not null);
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "GeneralSettingsTab: failed to resolve ICommitteeAnnualResetService — AGM banner will be suppressed");
            _committeeResetService = null;
        }

        Logger.LogDebug("GeneralSettingsTab: calling SettingsService.GetAsync");
        try
        {
            _settings = await SettingsService.GetAsync();
            Logger.LogInformation("GeneralSettingsTab: settings loaded. HasSettings={HasSettings}", _settings is not null);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "GeneralSettingsTab: SettingsService.GetAsync failed");
            _errorMessage = $"Failed to load settings: {ex.Message}";
            return;
        }

        if (_committeeResetService is not null)
        {
            Logger.LogDebug("GeneralSettingsTab: checking AGM banner");
            try
            {
                _agmBanner = await _committeeResetService.CheckAgmBannerAsync();
                Logger.LogDebug("GeneralSettingsTab: AGM banner check complete. HasBanner={HasBanner}", _agmBanner is not null);
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "GeneralSettingsTab: CheckAgmBannerAsync failed — banner suppressed");
            }
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
            // Merge in the fields owned by the GST/BAS tab, so a stale in-memory copy
            // here never clobbers a concurrent GST-registration save made there.
            var current = await SettingsService.GetAsync();
            if (current is not null)
            {
                _settings.IsGstRegistered = current.IsGstRegistered;
                _settings.AnnualFeeGstCode = current.AnnualFeeGstCode;
                _settings.AttendanceFeeGstCode = current.AttendanceFeeGstCode;
            }

            await SettingsService.SaveAsync(_settings);
            _successMessage = "Settings saved successfully.";
        }
        catch (ValidationException ex)
        {
            _errorMessage = ex.Message;
        }
        catch (Exception ex)
        {
            _errorMessage = $"Failed to save settings: {ex.Message}";
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

    private async Task HandleResetCommitteeAsync()
    {
        if (_committeeResetService is null) return;

        _resetting = true;
        _errorMessage = null;
        _successMessage = null;

        try
        {
            await _committeeResetService.ResetAsync();
            _successMessage = "Committee positions have been reset for the new year.";
            _agmBanner = null;
        }
        catch (ValidationException ex)
        {
            _errorMessage = ex.Message;
        }
        catch (Exception ex)
        {
            _errorMessage = $"Committee reset failed: {ex.Message}";
        }
        finally
        {
            _resetting = false;
        }
    }
}
