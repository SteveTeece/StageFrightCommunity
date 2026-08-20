using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using StageFright.Core.Contracts;
using StageFright.Core.Enums;
using StageFright.Core.Modules.Settings;
using StageFright.UI.Layout;

namespace StageFright.UI.Pages.Setup;

public partial class SetupWizard : ComponentBase
{
    [Inject] private IServiceProvider ServiceProvider { get; set; } = null!;

    [CascadingParameter] private ThemeProvider? ThemeProvider { get; set; }

    private readonly SetupFormModel _model = new();
    private EditContext _editContext = null!;
    private IDebugDataSeeder? _debugSeeder;
    private int _currentStep = 1;
    private bool _submitting;
    private bool _seedingInProgress;
    private bool _seedWithTestData;
    private string? _errorMessage;
    private string? _seedingProgress;

    protected override void OnInitialized()
    {
        _editContext = new EditContext(_model);

        // IDebugDataSeeder is only registered in Debug builds (MauiProgram.cs) — there is
        // never a database seed in Release, so resolve it optionally rather than requiring
        // it via [Inject], and hide the "Load sample data" checkbox when it's unavailable.
        _debugSeeder = ServiceProvider.GetService(typeof(IDebugDataSeeder)) as IDebugDataSeeder;
    }

    private void HandleNext()
    {
        if (_editContext.Validate() && _currentStep < 5)
            _currentStep++;
    }

    private void HandleBack()
    {
        if (_currentStep > 1)
            _currentStep--;
    }

    private void HandleTaxToggleChanged()
    {
        if (!_model.IsTaxApplicable)
        {
            _model.TaxRate = null;
            _model.AnnualFeeTaxCode = null;
            _model.AttendanceFeeTaxCode = null;
        }
    }

    private async Task HandleThemeToggleAsync()
    {
        if (ThemeProvider is not null)
            await ThemeProvider.ToggleAsync();
    }

    private async Task HandleValidSubmitAsync()
    {
        _submitting = true;
        _errorMessage = null;

        try
        {
            var officeHolderTitles = (_model.CommitteeOfficeHolderTitlesText ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();

            var request = new SetupRequest(
                OrganizationName: _model.OrganizationName!,
                AnnualFee: _model.AnnualFee,
                AttendanceFee: _model.AttendanceFee,
                MembershipRenewalMonth: _model.MembershipRenewalMonth,
                IsTaxApplicable: _model.IsTaxApplicable,
                TaxRate: _model.TaxRate,
                AnnualFeeTaxCode: _model.AnnualFeeTaxCode,
                AttendanceFeeTaxCode: _model.AttendanceFeeTaxCode,
                Theme: ThemeProvider?.CurrentTheme ?? Theme.Dark,
                CommitteeRenewalMonth: _model.CommitteeRenewalMonth,
                CommitteeOfficeHolderTitles: officeHolderTitles,
                GeneralCommitteeSeatCountTarget: _model.GeneralCommitteeSeatCountTarget,
                AuditRetentionYears: _model.AuditRetentionYears);

            await SetupService.InitializeAsync(request);

            if (_seedWithTestData && _debugSeeder is not null)
            {
                _seedingInProgress = true;
                try
                {
                    var progress = new Progress<string>(msg =>
                    {
                        _seedingProgress = msg;
                        InvokeAsync(StateHasChanged);
                    });
                    await Task.Run(() => _debugSeeder.SeedAsync(progress));
                }
                finally
                {
                    _seedingInProgress = false;
                }
            }

            Nav.NavigateTo("/dashboard");
        }
        catch (Core.Exceptions.ValidationException ex)
        {
            _errorMessage = ex.Message;
        }
        catch
        {
            _errorMessage = "An unexpected error occurred. Please try again.";
        }
        finally
        {
            _submitting = false;
        }
    }
}
