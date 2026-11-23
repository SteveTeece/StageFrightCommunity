using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using StageFright.Core.Localization;
using StageFright.UI.Resources.Strings;

namespace StageFright.UI.Pages.Setup.Tabs;

/// <summary>Membership section of the Organisation Settings tab — renewal month and audit
/// retention. The fee amounts moved to <see cref="AccountingTab"/> beside their tax
/// treatment (spec 031).</summary>
public partial class MembershipFeesTab : ComponentBase
{
    [Parameter, EditorRequired] public SetupFormModel Model { get; set; } = null!;

    [Inject] private IStringLocalizer<SetupResource> L { get; set; } = null!;
    [Inject] private ILocalizer Loc { get; set; } = null!;

    /// <summary>"{n} year(s)" audit-retention select option, pluralised.</summary>
    private string AuditRetentionOptionText(int years) =>
        Loc.Plural<SetupResource>("Setup_Fees_AuditRetentionYears", years);
}
