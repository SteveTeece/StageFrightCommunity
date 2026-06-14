using Microsoft.AspNetCore.Components;
using StageFright.Core.Contracts;

namespace StageFright.UI;

public partial class App : ComponentBase
{
    [Inject] private ISetupService SetupService { get; set; } = null!;
    [Inject] private IStartupDiagnosticService Diagnostics { get; set; } = null!;
    [Inject] private NavigationManager Nav { get; set; } = null!;

    protected override async Task OnInitializedAsync()
    {
        // Startup errors (e.g. corrupted database) take priority over the setup check
        if (Diagnostics.HasStartupError)
        {
            Nav.NavigateTo("/startup-error", replace: true);
            return;
        }

        if (!await SetupService.IsSetupCompleteAsync())
            Nav.NavigateTo("/setup", forceLoad: false);
    }
}
