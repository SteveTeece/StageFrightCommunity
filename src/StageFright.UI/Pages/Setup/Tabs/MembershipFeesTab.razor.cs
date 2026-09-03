using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using StageFright.Core.Localization;
using StageFright.UI.Resources.Strings;

namespace StageFright.UI.Pages.Setup.Tabs;

/// <summary>Membership &amp; Fees tab (US1) — fees, renewal month, audit retention,
/// relocated unchanged from the old single-page wizard's Step 2.</summary>
public partial class MembershipFeesTab : ComponentBase
{
    [Parameter, EditorRequired] public SetupFormModel Model { get; set; } = null!;

    [Inject] private IStringLocalizer<SetupResource> L { get; set; } = null!;
    [Inject] private ILocalizer Loc { get; set; } = null!;

    /// <summary>"{n} year(s)" audit-retention select option, pluralised.</summary>
    private string AuditRetentionOptionText(int years) =>
        Loc.Plural<SetupResource>("Setup_Fees_AuditRetentionYears", years);
}
