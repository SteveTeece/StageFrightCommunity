using System.Globalization;
using Microsoft.AspNetCore.Components;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Modules.Finance;

namespace StageFright.UI.Pages.Finance;

public partial class JournalEntryPage : ComponentBase
{
    [Inject] private IGeneralJournalService JournalService { get; set; } = null!;

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

    protected override async Task OnInitializedAsync()
    {
        try
        {
            _accounts = await JournalService.GetJournalAccountsAsync();
            ResetLines();
        }
        catch (Exception ex)
        {
            _errorMessage = $"Failed to load accounts: {ex.Message}";
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

    private static decimal ParseAmount(object? value) =>
        decimal.TryParse(value?.ToString(), NumberStyles.Number, CultureInfo.CurrentCulture, out var amount)
            ? amount
            : 0m;

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
            _successMessage = $"Journal of {TotalDebits:C} posted successfully.";
        }
        catch (Exception ex)
        {
            _errorMessage = $"Failed to post journal: {ex.Message}";
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
