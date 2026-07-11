using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using StageFright.Core.Contracts;
using StageFright.Core.Modules.Settings;

namespace StageFright.UI.Pages.Setup;

public partial class SetupWizard : ComponentBase
{
    [Inject] private IServiceProvider ServiceProvider { get; set; } = null!;

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
        if (_editContext.Validate() && _currentStep < 4)
            _currentStep++;
    }

    private void HandleBack()
    {
        if (_currentStep > 1)
            _currentStep--;
    }

    private void HandleGstToggleChanged()
    {
        if (!_model.IsGstRegistered)
        {
            _model.AnnualFeeGstCode = null;
            _model.AttendanceFeeGstCode = null;
        }
    }

    private async Task HandleValidSubmitAsync()
    {
        _submitting = true;
        _errorMessage = null;

        try
        {
            var request = new SetupRequest(
                OrganizationName: _model.OrganizationName!,
                Abn: _model.Abn!,
                AnnualFee: _model.AnnualFee,
                AttendanceFee: _model.AttendanceFee,
                MembershipRenewalMonth: _model.MembershipRenewalMonth,
                IsGstRegistered: _model.IsGstRegistered,
                AnnualFeeGstCode: _model.AnnualFeeGstCode,
                AttendanceFeeGstCode: _model.AttendanceFeeGstCode);

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
