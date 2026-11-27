using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using StageFright.UI.Resources.Strings;

namespace StageFright.UI.Pages.Finance;

/// <summary>
/// Records an expense from the Banking screen (issue #400): wraps <see cref="ExpensePaymentPage"/>
/// and adds a Back to Banking link. The form, its validation and its posting are unchanged.
/// </summary>
public partial class BankingExpensePage : ComponentBase
{
    [Inject] private IStringLocalizer<FinanceResource> L { get; set; } = null!;
}
