using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Exceptions;
using StageFright.Core.Localization;
using StageFright.Core.Modules.Finance;
using StageFright.Core.Modules.Localization.Resources;
using StageFright.UI.Resources.Strings;
using StageFright.UI.Shared;

namespace StageFright.UI.Pages.Finance;

/// <summary>Record cash withdrawal (spec 033, US4): cash taken from a bank account into Petty Cash, e.g. a float.</summary>
public partial class CashWithdrawalPage : ComponentBase
{
    private const string BankingPath = "/finance/banking";
    private const string BankField = nameof(CashWithdrawalModel.BankAccountId);
    private const string AmountField = nameof(CashWithdrawalModel.Amount);

    [Inject] private IBankingService BankingService { get; set; } = null!;
    [Inject] private NavigationManager Nav { get; set; } = null!;
    [Inject] private IStringLocalizer<FinanceResource> L { get; set; } = null!;
    [Inject] private IStringLocalizer<SharedResource> Shared { get; set; } = null!;
    [Inject] private ILocalizer Loc { get; set; } = null!;
    [Inject] private ILogger<CashWithdrawalPage> Logger { get; set; } = null!;

    private readonly CashWithdrawalModel _form = new();
    private readonly Dictionary<string, string> _errors = [];
    private IReadOnlyList<Account> _banks = [];
    private bool _loading = true;
    private bool _saving;
    private string? _errorMessage;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            var overview = await BankingService.GetOverviewAsync();
            _banks = overview.DepositBankAccounts;

            if (_banks.Count == 1)
                _form.BankAccountId = _banks[0].Id;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to load the cash withdrawal page");
            _errorMessage = Loc.Get<FinanceResource>("Finance_Banking_LoadError", Detail(ex));
        }
        finally
        {
            _loading = false;
        }
    }

    private async Task SaveAsync()
    {
        // A second submit while the first is in flight would post the withdrawal twice.
        if (_saving)
            return;

        _errors.Clear();
        _errorMessage = null;

        if (_form.BankAccountId == Guid.Empty)
            _errors[BankField] = Loc.Get<ValidationResource>("Validation_Withdrawal_BankAccountInvalid");

        if (_form.Amount is not > 0m)
            _errors[AmountField] = L["Finance_Common_AmountPositiveError"];

        if (_errors.Count > 0)
            return;

        _saving = true;
        try
        {
            await BankingService.RecordWithdrawalAsync(new RecordCashWithdrawalRequest
            {
                Date = _form.Date,
                BankAccountId = _form.BankAccountId,
                Amount = _form.Amount!.Value,
                Purpose = string.IsNullOrWhiteSpace(_form.Purpose)
                    ? L["Finance_Banking_WithdrawalDefaultDescription"]
                    : _form.Purpose.Trim()
            });
            Nav.NavigateTo(BankingPath);
        }
        catch (ValidationException ex)
        {
            _errorMessage = ex.Message;
        }
        catch (ClosedPeriodException)
        {
            _errorMessage = Loc.Get<ValidationResource>("Validation_ClosedPeriod_PostingRejected");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to record a cash withdrawal");
            _errorMessage = Loc.Get<FinanceResource>("Finance_Banking_WithdrawalSaveError", Detail(ex));
        }
        finally
        {
            _saving = false;
        }
    }

    private string Detail(Exception ex) => ErrorDetail.For(ex, L["Finance_Banking_UnexpectedErrorDetail"]);

    private void Cancel() => Nav.NavigateTo(BankingPath);
}
