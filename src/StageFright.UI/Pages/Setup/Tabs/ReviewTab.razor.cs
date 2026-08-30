using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using StageFright.Core.Enums;
using StageFright.Core.Localization;
using StageFright.Core.Modules.Settings;
using StageFright.UI.Layout;
using StageFright.UI.Resources.Strings;

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

    [Inject] private IStringLocalizer<SetupResource> L { get; set; } = null!;
    [Inject] private ILocalizer Loc { get; set; } = null!;

    private string ThemeText() =>
        (ThemeProvider?.CurrentTheme == Theme.Dark ? Theme.Dark : Theme.Light).LocalizeEnum();

    private string YesNo(bool value) =>
        value ? L["Setup_Review_Yes"].Value : L["Setup_Review_No"].Value;

    private string TaxCodeText(TaxCode? code) =>
        code?.LocalizeEnum() ?? L["Setup_Review_TaxExemptDefault"].Value;

    private string AuditRetentionText() =>
        Loc.Plural<SetupResource>("Setup_Review_AuditRetentionYears", Model.AuditRetentionYears);

    private string AccountTypeText(QueuedAccountRequest account) =>
        account.Type.LocalizeEnum() + (account.IsBankAccount ? L["Setup_Review_BankCashSuffix"].Value : string.Empty);
}
