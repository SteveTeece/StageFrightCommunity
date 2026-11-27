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

public partial class OpeningBalancesWizard : ComponentBase
{
    [Inject] private IOpeningBalanceService OpeningBalanceService { get; set; } = null!;
    [Inject] private ISettingsService SettingsService { get; set; } = null!;
    [Inject] private IStringLocalizer<FinanceResource> L { get; set; } = null!;
    [Inject] private IStringLocalizer<SharedResource> Shared { get; set; } = null!;
    [Inject] private ILocalizer Loc { get; set; } = null!;
    [Inject] private ILogger<OpeningBalancesWizard> Logger { get; set; } = null!;
    [Inject] private NavigationManager Nav { get; set; } = null!;

    private IReadOnlyList<Account> _accounts = [];
    private int _step = 1;
    private DateTime _asAtDate = DateTime.Today;
    // Non-null once opening balances are posted: they're locked and shown read-only (issue #398).
    private RecordedOpeningBalances? _recorded;
    private bool _loading = true;
    private string? _successMessage;
    private string? _errorMessage;

    /// <summary>"Step N of 2" wizard progress caption.</summary>
    private string StepText() =>
        Loc.Get<FinanceResource>("Finance_OpeningBalances_StepOf", _step);

    /// <summary>"Posted as at {Date} and locked" caption above the read-only balances.</summary>
    private string RecordedIntroText() =>
        Loc.Get<FinanceResource>("Finance_OpeningBalances_RecordedIntro",
            _recorded!.AsAtDate.ToString("d MMMM yyyy"));

    protected override async Task OnInitializedAsync()
    {
        try
        {
            _accounts = await OpeningBalanceService.GetOpeningBalanceAccountsAsync();

            _recorded = await OpeningBalanceService.GetRecordedOpeningBalancesAsync();

            var settings = await SettingsService.GetAsync();
            var startMonth = settings?.FinancialYearStartMonth ?? FinancialYearCalculator.DefaultStartMonth;
            var startDay = settings?.FinancialYearStartDay ?? FinancialYearCalculator.DefaultStartDay;
            var (fyStart, _) = FinancialYearCalculator.GetRange(DateTime.Today, startMonth, startDay);
            _asAtDate = fyStart;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Unexpected error in {Operation}", "OpeningBalancesWizard.OnInitializedAsync");
            _errorMessage = Loc.Get<FinanceResource>("Finance_OpeningBalances_LoadError", ErrorDetail.For(ex, Loc));
        }
        finally
        {
            _loading = false;
        }
    }

    private void GoToStep(int step) => _step = step;

    private void GoToJournal() => Nav.NavigateTo("/finance/journal");

    private async Task PostAsync(RecordOpeningBalancesRequest request)
    {
        _errorMessage = null;
        try
        {
            await OpeningBalanceService.RecordOpeningBalancesAsync(request);
        }
        catch (ClosedPeriodException)
        {
            _errorMessage = Loc.Get<ValidationResource>("Validation_ClosedPeriod_PostingRejected");
            return;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Unexpected error in {Operation}", "OpeningBalancesWizard.PostAsync");
            _errorMessage = Loc.Get<FinanceResource>("Finance_OpeningBalances_PostError", ErrorDetail.For(ex, Loc));
            return;
        }

        _successMessage = Loc.Get<FinanceResource>("Finance_OpeningBalances_SuccessMessage",
            request.AsAtDate.ToString("d MMMM yyyy"));

        // The balances just posted are now locked: show them read-only in place of the form.
        try
        {
            _recorded = await OpeningBalanceService.GetRecordedOpeningBalancesAsync();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Unexpected error in {Operation}", "OpeningBalancesWizard.PostAsync");
            _errorMessage = Loc.Get<FinanceResource>("Finance_OpeningBalances_LoadError", ErrorDetail.For(ex, Loc));
        }
    }
}
