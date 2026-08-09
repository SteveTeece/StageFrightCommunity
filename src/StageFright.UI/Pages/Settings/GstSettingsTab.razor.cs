using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using StageFright.Core.Contracts;
using StageFright.Core.Exceptions;
using AppSettings = StageFright.Core.Entities.Settings;

namespace StageFright.UI.Pages.Settings;

public partial class GstSettingsTab : ComponentBase
{
    [Inject] private ISettingsService SettingsService { get; set; } = null!;
    [Inject] private ILogger<GstSettingsTab> Logger { get; set; } = null!;

    private AppSettings? _settings;

    private bool _saving;
    private string? _errorMessage;
    private string? _successMessage;
    private bool? _pendingGstToggle;

    protected override async Task OnInitializedAsync()
    {
        Logger.LogInformation("GstSettingsTab.OnInitializedAsync start");

        try
        {
            _settings = await SettingsService.GetAsync();
            Logger.LogInformation("GstSettingsTab: settings loaded. HasSettings={HasSettings}", _settings is not null);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "GstSettingsTab: SettingsService.GetAsync failed");
            _errorMessage = $"Failed to load settings: {ex.Message}";
        }
    }

    private void HandleGstToggleRequested(ChangeEventArgs e)
    {
        if (_settings is null) return;

        var requested = (bool)(e.Value ?? false);
        _pendingGstToggle = requested == _settings.IsGstRegistered ? null : requested;
    }

    private void ConfirmGstToggle()
    {
        if (_settings is null || _pendingGstToggle is null) return;

        _settings.IsGstRegistered = _pendingGstToggle.Value;
        _pendingGstToggle = null;
    }

    private void CancelGstToggle()
    {
        _pendingGstToggle = null;
    }

    private async Task HandleSaveAsync()
    {
        if (_settings is null) return;

        _saving = true;
        _errorMessage = null;
        _successMessage = null;

        try
        {
            // Merge in every field NOT owned by this tab, so a stale in-memory copy of
            // (e.g.) the General tab's fields never clobbers a concurrent save made there.
            var current = await SettingsService.GetAsync();
            if (current is not null)
            {
                _settings.OrganizationName = current.OrganizationName;
                _settings.Abn = current.Abn;
                _settings.AnnualFee = current.AnnualFee;
                _settings.AttendanceFee = current.AttendanceFee;
                _settings.MembershipRenewalMonth = current.MembershipRenewalMonth;
                _settings.CommitteeRenewalMonth = current.CommitteeRenewalMonth;
                _settings.FinancialYearStartMonth = current.FinancialYearStartMonth;
                _settings.MaxAgeRangeYears = current.MaxAgeRangeYears;
                _settings.MinimumMemberAge = current.MinimumMemberAge;
                _settings.Theme = current.Theme;
                _settings.ShowParticipationGraphs = current.ShowParticipationGraphs;
                _settings.GeneralCommitteeSeatCountTarget = current.GeneralCommitteeSeatCountTarget;
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
}
