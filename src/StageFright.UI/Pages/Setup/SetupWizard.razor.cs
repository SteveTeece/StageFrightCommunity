using Microsoft.AspNetCore.Components;
using StageFright.Core.Contracts;
using StageFright.Core.Modules.Settings;

namespace StageFright.UI.Pages.Setup;

public partial class SetupWizard : ComponentBase
{
    [Inject] private IDebugDataSeeder DebugSeeder { get; set; } = null!;

    private readonly SetupFormModel _model = new();
    private bool _submitting;
    private bool _seedWithTestData;
    private string? _errorMessage;
    private string? _seedingProgress;

    private async Task HandleValidSubmitAsync()
    {
        _submitting = true;
        _errorMessage = null;

        try
        {
            var request = new SetupRequest(
                OrganizationName: _model.OrganizationName!,
                AnnualFee: _model.AnnualFee,
                AttendanceFee: _model.AttendanceFee,
                MembershipRenewalMonth: _model.MembershipRenewalMonth);

            await SetupService.InitializeAsync(request);

            if (_seedWithTestData)
            {
                var progress = new Progress<string>(msg =>
                {
                    _seedingProgress = msg;
                    InvokeAsync(StateHasChanged);
                });
                await Task.Run(() => DebugSeeder.SeedAsync(progress));
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
