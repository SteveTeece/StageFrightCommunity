using Microsoft.AspNetCore.Components;
using StageFright.UI.Layout;

namespace StageFright.UI.Pages.Setup.Tabs;

/// <summary>Review tab (US1) — read-only summary of every other tab's values, plus the
/// existing debug-only "Load sample data" checkbox (already lived on the old wizard's
/// last step, so relocating it here satisfies FR-025 as-is). US3 later upgrades the
/// committee-titles/queued-accounts lines to two BorderedListBox summaries.</summary>
public partial class ReviewTab : ComponentBase
{
    [Parameter, EditorRequired] public SetupFormModel Model { get; set; } = null!;
    [Parameter] public bool DebugSeederAvailable { get; set; }
    [Parameter] public bool SeedWithTestData { get; set; }
    [Parameter] public EventCallback<bool> SeedWithTestDataChanged { get; set; }

    [CascadingParameter] private ThemeProvider? ThemeProvider { get; set; }

    private async Task OnSeedWithTestDataChangedAsync(ChangeEventArgs e)
    {
        var isChecked = e.Value is bool b && b;
        SeedWithTestData = isChecked;
        await SeedWithTestDataChanged.InvokeAsync(isChecked);
    }
}
