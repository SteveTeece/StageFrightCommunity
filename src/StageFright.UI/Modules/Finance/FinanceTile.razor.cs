using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using StageFright.Core.Contracts;
using StageFright.Core.Localization;
using StageFright.Core.Modules.Finance;
using StageFright.UI.Resources.Strings;

namespace StageFright.UI.Modules.Finance;

/// <summary>
/// Dashboard tile body for the Finance module (design 3a): current organisation
/// balance with month-to-date income and expenses from the GL summary service.
/// </summary>
public partial class FinanceTile : ComponentBase
{
    [Inject] private IFinanceSummaryService FinanceSummaryService { get; set; } = null!;
    [Inject] private IStringLocalizer<FinanceResource> L { get; set; } = null!;
    [Inject] private IStringLocalizer<SharedResource> Shared { get; set; } = null!;
    [Inject] private ILocalizer Loc { get; set; } = null!;

    private bool _loading = true;
    private bool _error;
    private FinanceSummary? _summary;
    private string _monthName = string.Empty;

    /// <summary>"{month} income +{amount}" month-to-date income line for the tile note.</summary>
    private string MonthIncomeText() =>
        Loc.Get<FinanceResource>("Finance_Tile_MonthIncome",
            _monthName, MoneyFormatter.Format(_summary!.MonthIncome));

    /// <summary>"expenses −{amount}" month-to-date expenses line for the tile note.</summary>
    private string MonthExpensesText() =>
        Loc.Get<FinanceResource>("Finance_Tile_MonthExpenses",
            MoneyFormatter.Format(_summary!.MonthExpenses));

    protected override async Task OnInitializedAsync()
    {
        try
        {
            var today = DateTime.Today;
            _monthName = CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(today.Month);
            _summary = await FinanceSummaryService.GetSummaryAsync(today);
        }
        catch
        {
            _error = true;
        }
        finally
        {
            _loading = false;
        }
    }
}
