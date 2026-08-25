using Microsoft.AspNetCore.Components;
using StageFright.Core.Modules.Settings;
using StageFright.UI.Layout;

namespace StageFright.UI.Pages.Setup.Tabs;

/// <summary>Review tab (US1/US3) — read-only summary of every other tab's values,
/// including two BorderedListBox summaries (queued committee titles, queued Chart of
/// Accounts entries — FR-006) and, when a debug seeder is registered, a read-only
/// "Load sample data" row (FR-002). The interactive checkbox itself now lives on the
/// Organisation Settings tab (<see cref="SampleDataTab"/>, FR-001) — Review only
/// displays the choice, it no longer writes it.</summary>
public partial class ReviewTab : ComponentBase
{
    [Parameter, EditorRequired] public SetupFormModel Model { get; set; } = null!;
    [Parameter, EditorRequired] public IReadOnlyList<string> QueuedCommitteeTitles { get; set; } = Array.Empty<string>();
    [Parameter, EditorRequired] public IReadOnlyList<QueuedAccountRequest> QueuedAccounts { get; set; } = Array.Empty<QueuedAccountRequest>();
    [Parameter] public bool DebugSeederAvailable { get; set; }
    [Parameter] public bool SeedWithTestData { get; set; }

    [CascadingParameter] private ThemeProvider? ThemeProvider { get; set; }
}
