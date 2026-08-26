using Microsoft.AspNetCore.Components;

namespace StageFright.UI.Pages.Setup.Tabs;

/// <summary>Organisation Settings tab (US1) — the debug-only "Load sample data" checkbox,
/// relocated here from ReviewTab (FR-001) so the coordinator decides up front, before
/// reaching the Chart of Accounts/Opening Balances/Committee tabs it now also gates
/// (see SetupWizard.razor.cs's IsTabBypassed). Markup and behavior are unchanged from
/// the original checkbox — only its host component moved.</summary>
public partial class SampleDataTab : ComponentBase
{
    [Parameter] public bool DebugSeederAvailable { get; set; }
    [Parameter] public bool SeedWithTestData { get; set; }
    [Parameter] public EventCallback<bool> SeedWithTestDataChanged { get; set; }

    private async Task OnSeedWithTestDataChangedAsync(ChangeEventArgs e)
    {
        var isChecked = e.Value is bool b && b;
        SeedWithTestData = isChecked;
        await SeedWithTestDataChanged.InvokeAsync(isChecked);
    }
}
