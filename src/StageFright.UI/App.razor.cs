using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using StageFright.Core.Contracts;
using StageFright.UI.Resources.Strings;

namespace StageFright.UI;

public partial class App : ComponentBase
{
    [Inject] private ISetupService SetupService { get; set; } = null!;
    [Inject] private IStartupDiagnosticService Diagnostics { get; set; } = null!;
    [Inject] private ILanguagePreferenceStore LanguagePreferenceStore { get; set; } = null!;
    [Inject] private NavigationManager Nav { get; set; } = null!;
    [Inject] private ILogger<App> Logger { get; set; } = null!;
    [Inject] private IStringLocalizer<SharedResource> L { get; set; } = null!;

    private ErrorBoundary? _errorBoundary;

    protected override async Task OnInitializedAsync()
    {
        // Startup errors (e.g. corrupted database) take priority over the setup check
        if (Diagnostics.HasStartupError)
        {
            Nav.NavigateTo("/startup-error", replace: true);
            return;
        }

        if (!await SetupService.IsSetupCompleteAsync())
        {
            // First run with no recorded language preference yet (spec 029, FR-001/FR-005):
            // show /language-select before the wizard. A preference already recorded — e.g. a
            // prior launch that chose a language but didn't finish setup — skips straight to
            // /setup, exactly as before this feature.
            var target = string.IsNullOrWhiteSpace(LanguagePreferenceStore.Get()) ? "/language-select" : "/setup";
            Nav.NavigateTo(target, forceLoad: false);
        }
    }

    protected override void OnParametersSet()
    {
        // Reset the error boundary on each navigation so a new page gets a fresh boundary
        _errorBoundary?.Recover();
    }

    private void LogCircuitError(Exception ex)
    {
        Logger.LogError(ex, "Unhandled Blazor component error on route {Uri}", Nav.Uri);
    }

    private void RecoverFromError()
    {
        _errorBoundary?.Recover();
    }
}
