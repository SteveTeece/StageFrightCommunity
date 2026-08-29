using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Localization;
using StageFright.Core.Modules.Finance;
using StageFright.UI.Resources.Strings;

namespace StageFright.UI.Pages.Finance;

public partial class JournalEntryPage : ComponentBase
{
    [Inject] private IGeneralJournalService JournalService { get; set; } = null!;
    [Inject] private IStringLocalizer<FinanceResource> L { get; set; } = null!;
    [Inject] private IStringLocalizer<SharedResource> Shared { get; set; } = null!;
    [Inject] private ILocalizer Loc { get; set; } = null!;

    private readonly List<JournalLineModel> _lines = new();
    private IReadOnlyList<Account> _accounts = [];
    private DateTime _date = DateTime.Today;
    private string? _description;
    private bool _loading = true;
    private bool _saving;
    private string? _successMessage;
    private string? _errorMessage;

    private decimal TotalDebits => _lines.Sum(l => l.Debit);
    private decimal TotalCredits => _lines.Sum(l => l.Credit);
    private decimal OutOfBalance => TotalDebits - TotalCredits;
    private bool IsBalanced => OutOfBalance == 0m && TotalDebits > 0m;

    private bool CanSave =>
        !_saving
        && IsBalanced
        && _lines.Count >= 2
        && !string.IsNullOrWhiteSpace(_description)
        && _lines.All(l => l.AccountId != Guid.Empty && (l.Debit != 0m) != (l.Credit != 0m));

    /// <summary>"Out of balance by {amount}" badge — fixed-AUD formatted difference.</summary>
    private string OutOfBalanceText() =>
        Loc.Get<FinanceResource>("Finance_Journal_OutOfBalanceBadge", MoneyFormatter.Format(Math.Abs(OutOfBalance)));

    protected override async Task OnInitializedAsync()
    {
        try
        {
            _accounts = await JournalService.GetJournalAccountsAsync();
            ResetLines();
        }
        catch (Exception ex)
        {
            _errorMessage = Loc.Get<FinanceResource>("Finance_Journal_LoadError", ex.Message);
        }
        finally
        {
            _loading = false;
        }
    }

    private void AddLine() => _lines.Add(new JournalLineModel());

    private void RemoveLine(JournalLineModel line) => _lines.Remove(line);

    private void SetDebit(JournalLineModel line, ChangeEventArgs args)
    {
        line.Debit = ParseAmount(args.Value);
        if (line.Debit != 0m)
            line.Credit = 0m;
    }

    private void SetCredit(JournalLineModel line, ChangeEventArgs args)
    {
        line.Credit = ParseAmount(args.Value);
        if (line.Credit != 0m)
            line.Debit = 0m;
    }

    private void SetAccount(JournalLineModel line, ChangeEventArgs args)
    {
        line.AccountId = Guid.TryParse(args.Value?.ToString(), out var id) ? id : Guid.Empty;
    }

    // The value of an <input type="number"> is always serialised invariant, so it is parsed
    // invariant via the shared helper — never with CultureInfo.CurrentCulture, which reads the
    // period as a thousands separator under fr-FR / de-DE (spec 028, FR-007…FR-009).
    private static decimal ParseAmount(object? value) => MoneyInput.Parse(value?.ToString());

    private async Task SaveAsync()
    {
        _errorMessage = null;
        _saving = true;
        try
        {
            var request = new RecordJournalRequest
            {
                Date = _date,
                Description = _description?.Trim(),
                Lines = _lines.Select(l => new JournalLine
                {
                    AccountId = l.AccountId,
                    DebitAmount = l.Debit,
                    CreditAmount = l.Credit
                }).ToList()
            };

            await JournalService.RecordJournalAsync(request);
            _successMessage = Loc.Get<FinanceResource>("Finance_Journal_SuccessMessage",
                MoneyFormatter.Format(TotalDebits));
        }
        catch (Exception ex)
        {
            _errorMessage = Loc.Get<FinanceResource>("Finance_Journal_PostError", ex.Message);
        }
        finally
        {
            _saving = false;
        }
    }

    private void RecordAnother()
    {
        _successMessage = null;
        _errorMessage = null;
        _description = null;
        _date = DateTime.Today;
        ResetLines();
    }

    private void ResetLines()
    {
        _lines.Clear();
        _lines.Add(new JournalLineModel());
        _lines.Add(new JournalLineModel());
    }
}
