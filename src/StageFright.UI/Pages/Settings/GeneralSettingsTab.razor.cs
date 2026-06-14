using Microsoft.AspNetCore.Components;
using StageFright.Core.Contracts;
using StageFright.Core.Exceptions;
using StageFright.UI.Layout;
using AppSettings = StageFright.Core.Entities.Settings;

namespace StageFright.UI.Pages.Settings;

public partial class GeneralSettingsTab : ComponentBase
{
    [Inject] private ISettingsService SettingsService { get; set; } = null!;
    [Inject] private IServiceProvider ServiceProvider { get; set; } = null!;

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
        _committeeResetService = ServiceProvider.GetService(typeof(ICommitteeAnnualResetService))
            as ICommitteeAnnualResetService;

        _settings = await SettingsService.GetAsync();

        if (_committeeResetService is not null)
        {
            try
            {
                _agmBanner = await _committeeResetService.CheckAgmBannerAsync();
            }
            catch (Exception)
            {
                // Banner is non-critical; startup continues
            }
        }
    }

    private async Task HandleSaveAsync()
    {
        if (_settings is null) return;

        _saving = true;
        _errorMessage = null;
        _successMessage = null;

        try
        {
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
