using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using StageFright.Core.Contracts;
using StageFright.Core.Enums;
using StageFright.Core.Exceptions;
using StageFright.Core.Localization;
using StageFright.Core.Modules.Finance;
using StageFright.Core.Modules.Localization.Resources;
using StageFright.UI.Resources.Strings;
using StageFright.UI.Shared;

namespace StageFright.UI.Pages.Finance;

/// <summary>A bank deposit's cash and cheques, with an inline two-step reverse (spec 033, US5).</summary>
public partial class DepositDetailPage : ComponentBase
{
    [Parameter] public Guid Id { get; set; }

    [Inject] private IBankingService BankingService { get; set; } = null!;
    [Inject] private IStringLocalizer<FinanceResource> L { get; set; } = null!;
    [Inject] private IStringLocalizer<SharedResource> Shared { get; set; } = null!;
    [Inject] private ILocalizer Loc { get; set; } = null!;
    [Inject] private ILogger<DepositDetailPage> Logger { get; set; } = null!;

    private DepositDetail? _detail;
    private bool _loading = true;
    private bool _loadFailed;
    private bool _confirming;
    private bool _reversing;
    private string? _errorMessage;
    private string? _successMessage;

    protected override async Task OnParametersSetAsync()
    {
        _loading = true;
        _errorMessage = null;
        _successMessage = null;
        _confirming = false;
        await LoadAsync();
        _loading = false;
    }

    private async Task LoadAsync()
    {
        _loadFailed = false;
        try
        {
            _detail = await BankingService.GetDepositAsync(Id);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to load deposit {DepositId}", Id);
            _detail = null;
            _loadFailed = true;
            _errorMessage = Loc.Get<FinanceResource>("Finance_Banking_LoadError", Detail(ex));
        }
    }

    private void StartReverse()
    {
        _errorMessage = null;
        _successMessage = null;
        _confirming = true;
    }

    private void CancelReverse() => _confirming = false;

    private async Task ConfirmReverseAsync()
    {
        // A second click while the first is in flight would be refused anyway; skip the round trip.
        if (_reversing || _detail is null)
            return;

        _reversing = true;
        _errorMessage = null;
        try
        {
            await BankingService.ReverseDepositAsync(_detail.Id);
            _successMessage = L["Finance_Banking_DepositReversed"];
            await LoadAsync();
        }
        catch (ValidationException ex)
        {
            _errorMessage = ex.Message;
            await LoadAsync();
        }
        catch (ClosedPeriodException)
        {
            _errorMessage = Loc.Get<ValidationResource>("Validation_ClosedPeriod_PostingRejected");
            await LoadAsync();
        }
        catch (DataIntegrityException)
        {
            // A racing reversal stamped it first: reload, which shows it as reversed.
            await LoadAsync();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to reverse deposit {DepositId}", _detail.Id);
            _errorMessage = Loc.Get<FinanceResource>("Finance_Banking_ReverseError", Detail(ex));
        }
        finally
        {
            _confirming = false;
            _reversing = false;
        }
    }

    private decimal ItemsTotal => _detail?.Items.Sum(i => i.Amount) ?? 0m;

    private string StatusText()
    {
        if (_detail is not { IsReversed: true })
            return L["Finance_Banking_Status_Recorded"];

        return _detail.ReversedAt is { } reversedAt
            ? $"{L["Finance_Banking_Status_Reversed"]} · {Loc.Get<FinanceResource>("Finance_Banking_ReversedOn", reversedAt.ToLocalTime().ToShortDateString())}"
            : L["Finance_Banking_Status_Reversed"];
    }

    private string PayerText(AwaitingDepositItem item) =>
        item.Source == ReceivedItemSource.OpeningBalance && string.IsNullOrEmpty(item.PayerName)
            ? L["Finance_Banking_OpeningBalancePayer"].Value
            : item.PayerName ?? string.Empty;

    private string Detail(Exception ex) => ErrorDetail.For(ex, L["Finance_Banking_UnexpectedErrorDetail"]);
}
