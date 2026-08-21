using Microsoft.AspNetCore.Components;
using StageFright.Core.Modules.Settings;
using StageFright.UI.Layout;

namespace StageFright.UI.Pages.Setup.Tabs;

/// <summary>Review tab (US1/US3) — read-only summary of every other tab's values,
/// including two BorderedListBox summaries (queued committee titles, queued Chart of
/// Accounts entries — FR-006), plus the existing debug-only "Load sample data" checkbox
/// (already lived on the old wizard's last step, so relocating it here satisfies FR-025
/// as-is).</summary>
public partial class ReviewTab : ComponentBase
{
    [Parameter, EditorRequired] public SetupFormModel Model { get; set; } = null!;
    [Parameter, EditorRequired] public IReadOnlyList<string> QueuedCommitteeTitles { get; set; } = Array.Empty<string>();
    [Parameter, EditorRequired] public IReadOnlyList<QueuedAccountRequest> QueuedAccounts { get; set; } = Array.Empty<QueuedAccountRequest>();
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
