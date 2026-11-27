using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using StageFright.Core.Contracts;
using StageFright.Core.Localization;
using StageFright.Core.Modules.Finance;
using StageFright.UI.Resources.Strings;
using StageFright.UI.Shared;

namespace StageFright.UI.Pages.Finance;

/// <summary>
/// Finance → Opening Balances: a read-only view of the opening balances recorded at setup —
/// every account, at zero where none was entered. They are locked; a correction is a journal
/// entry, which the page links to (issue #398).
/// </summary>
public partial class OpeningBalancesPage : ComponentBase
{
    [Inject] private IOpeningBalanceService OpeningBalanceService { get; set; } = null!;
    [Inject] private IStringLocalizer<FinanceResource> L { get; set; } = null!;
    [Inject] private IStringLocalizer<SharedResource> Shared { get; set; } = null!;
    [Inject] private ILocalizer Loc { get; set; } = null!;
    [Inject] private ILogger<OpeningBalancesPage> Logger { get; set; } = null!;
    [Inject] private NavigationManager Nav { get; set; } = null!;

    private RecordedOpeningBalances? _recorded;
    private bool _loading = true;
    private string? _errorMessage;

    /// <summary>
    /// "Recorded as at {Date} and locked" caption; a dataset that never recorded any (from before
    /// issue #398) gets the "every account opened at zero" wording instead.
    /// </summary>
    private string IntroText() =>
        _recorded!.AsAtDate is { } asAt
            ? Loc.Get<FinanceResource>("Finance_OpeningBalances_RecordedIntro", asAt.ToString("d MMMM yyyy"))
            : L["Finance_OpeningBalances_NoneRecordedIntro"];

    protected override async Task OnInitializedAsync()
    {
        try
        {
            _recorded = await OpeningBalanceService.GetRecordedOpeningBalancesAsync();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Unexpected error in {Operation}", "OpeningBalancesPage.OnInitializedAsync");
            _errorMessage = Loc.Get<FinanceResource>("Finance_OpeningBalances_LoadError", ErrorDetail.For(ex, Loc));
        }
        finally
        {
            _loading = false;
        }
    }

    private void GoToJournal() => Nav.NavigateTo("/finance/journal");
}
