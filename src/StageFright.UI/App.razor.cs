using Microsoft.AspNetCore.Components;
using StageFright.Core.Contracts;

namespace StageFright.UI;

public partial class App : ComponentBase
{
    [Inject] private ISetupService SetupService { get; set; } = null!;
    [Inject] private NavigationManager Nav { get; set; } = null!;

    protected override async Task OnInitializedAsync()
    {
        if (!await SetupService.IsSetupCompleteAsync())
            Nav.NavigateTo("/setup", forceLoad: false);
    }
}
