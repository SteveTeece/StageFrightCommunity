using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using StageFright.Core.Contracts;
using StageFright.Core.Enums;
using StageFright.Core.Localization;
using StageFright.Core.Modules.Finance;
using StageFright.UI.Resources.Strings;

namespace StageFright.UI.Pages.Finance;

public partial class BankingPage : ComponentBase
{
    [Inject] private IBankingService BankingService { get; set; } = null!;
    [Inject] private NavigationManager Nav { get; set; } = null!;
    [Inject] private IStringLocalizer<FinanceResource> L { get; set; } = null!;
    [Inject] private IStringLocalizer<SharedResource> Shared { get; set; } = null!;
    [Inject] private ILocalizer Loc { get; set; } = null!;

    private BankingOverview? _overview;
    private IReadOnlyList<BankingHistoryEntry> _history = [];
    private bool _loading = true;
    private string? _errorMessage;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            _overview = await BankingService.GetOverviewAsync();
            _history = await BankingService.GetHistoryAsync();
        }
        catch (Exception ex)
        {
            _errorMessage = Loc.Get<FinanceResource>("Finance_Banking_LoadError", ex.Message);
        }
        finally
        {
            _loading = false;
        }
    }

    // The opening-balance item has no payer name, so it shows a translated label instead.
    private string PayerText(AwaitingDepositItem item) =>
        item.Source == ReceivedItemSource.OpeningBalance && string.IsNullOrEmpty(item.PayerName)
            ? L["Finance_Banking_OpeningBalancePayer"].Value
            : item.PayerName ?? string.Empty;

    // Withdrawals have no reversal, so they show a dash.
    private string StatusText(BankingHistoryEntry entry) =>
        entry.Type == BankingHistoryEntryType.Withdrawal ? "—"
        : entry.IsReversed ? L["Finance_Banking_Status_Reversed"].Value
        : L["Finance_Banking_Status_Recorded"].Value;

    private static string DepositLink(BankingHistoryEntry entry) => $"/finance/banking/deposits/{entry.Id}";

    private void GoToDeposit() => Nav.NavigateTo("/finance/banking/deposit");

    private void GoToWithdrawal() => Nav.NavigateTo("/finance/banking/withdrawal");
}
