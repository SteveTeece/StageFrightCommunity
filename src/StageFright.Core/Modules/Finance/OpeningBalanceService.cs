using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Exceptions;
using StageFright.Core.Localization;
using StageFright.Core.Modules.Localization.Resources;

namespace StageFright.Core.Modules.Finance;

/// <summary>
/// Posts opening balances under an OpeningBalance journal entry: one line per
/// entered account at its normal side, with any residual plugged to the Opening
/// Balance Equity account (3100). Every other account — including Member Receivable
/// and the tax clearing accounts, so a coordinator migrating from another system can
/// seed real carried-over balances for them — is eligible; only Opening Balance
/// Equity itself is excluded, since it's the plug target rather than an enterable row.
/// A positive Undeposited Funds (1105) amount also becomes one "opening balance"
/// <see cref="ReceivedItem"/> awaiting deposit; a negative one is refused (spec 033).
/// Opening balances post once only and are then locked (issue #398).
/// </summary>
public class OpeningBalanceService : IOpeningBalanceService
{
    private static readonly Guid[] ExcludedAccountIds =
    {
        SystemAccounts.OpeningBalanceEquityId
    };

    private readonly IAccountRepository _accountRepo;
    private readonly IGLRepository _glRepo;
    private readonly IJournalEntryRepository _journalRepo;
    private readonly IReceivedItemRepository _receivedItemRepo;
    private readonly IAuditTrailService _audit;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILocalizer _localizer;

    public OpeningBalanceService(
        IAccountRepository accountRepo,
        IGLRepository glRepo,
        IJournalEntryRepository journalRepo,
        IReceivedItemRepository receivedItemRepo,
        IAuditTrailService audit,
        IUnitOfWork unitOfWork,
        ILocalizer localizer)
    {
        _accountRepo = accountRepo;
        _glRepo = glRepo;
        _journalRepo = journalRepo;
        _receivedItemRepo = receivedItemRepo;
        _audit = audit;
        _unitOfWork = unitOfWork;
        _localizer = localizer;
    }

    public async Task<IReadOnlyList<Account>> GetOpeningBalanceAccountsAsync(CancellationToken ct = default)
    {
        var all = await _accountRepo.GetAllAsync(ct);
        return all
            .Where(a => !ExcludedAccountIds.Contains(a.Id))
            .OrderBy(a => a.AccountNumber)
            .ToList();
    }

    public async Task<RecordedOpeningBalances> GetRecordedOpeningBalancesAsync(CancellationToken ct = default)
    {
        var journals = await _journalRepo.GetByTypesAsync([JournalEntryType.OpeningBalance], ct);
        var transactions = journals.SelectMany(j => j.Transactions).ToList();

        var posted = transactions
            .Where(t => t.AccountId != SystemAccounts.OpeningBalanceEquityId)
            .GroupBy(t => t.AccountId)
            .ToDictionary(g => g.Key, g => (
                g.First().Account,
                Amount: g.Sum(t => FromNormalSide(t.Account.Type, t))));

        // Every eligible account is listed — at zero when it had no opening balance — plus any
        // account posted to that is no longer eligible (e.g. archived since).
        var eligible = await GetOpeningBalanceAccountsAsync(ct);
        var lines = eligible
            .Select(a => new RecordedOpeningBalanceLine
            {
                Account = a,
                Amount = posted.TryGetValue(a.Id, out var p) ? p.Amount : 0m
            })
            .Concat(posted.Values
                .Where(p => eligible.All(a => a.Id != p.Account.Id))
                .Select(p => new RecordedOpeningBalanceLine { Account = p.Account, Amount = p.Amount }))
            .OrderBy(l => l.Account.AccountNumber)
            .ToList();

        // Opening Balance Equity is credit-normal, so the plug is its credit minus debit —
        // the same sign convention as ComputePlug.
        var plug = transactions
            .Where(t => t.AccountId == SystemAccounts.OpeningBalanceEquityId)
            .Sum(t => t.CreditAmount - t.DebitAmount);

        return new RecordedOpeningBalances
        {
            AsAtDate = journals.Count > 0 ? journals.Min(j => j.Date) : null,
            Lines = lines,
            Plug = plug
        };
    }

    public decimal ComputePlug(IReadOnlyList<OpeningBalanceEntry> entries, IReadOnlyList<Account> accounts)
    {
        var accountsById = accounts.ToDictionary(a => a.Id);
        decimal debits = 0m, credits = 0m;

        foreach (var entry in entries.Where(e => e.Amount != 0m))
        {
            if (!accountsById.TryGetValue(entry.AccountId, out var account))
                continue;

            var (debit, credit) = ToNormalSide(account.Type, entry.Amount);
            debits += debit;
            credits += credit;
        }

        return debits - credits;
    }

    public async Task RecordOpeningBalancesAsync(RecordOpeningBalancesRequest request, CancellationToken ct = default)
    {
        // Opening balances are locked once posted (issue #398): a second posting would add to
        // the first rather than replace it, so an adjustment has to be a general journal.
        if (await _journalRepo.AnyOfTypeAsync(JournalEntryType.OpeningBalance, ct))
            throw new ValidationException(
                _localizer.Get<ValidationResource>("Validation_OpeningBalance_AlreadyPosted"),
                nameof(JournalEntry), nameof(RecordOpeningBalancesAsync));

        // No non-zero entry is valid: the journal is still recorded, with no lines, meaning every
        // account opened at zero — so the balances are locked all the same (issue #398).
        var entries = request.Entries.Where(e => e.Amount != 0m).ToList();

        if (entries.GroupBy(e => e.AccountId).Any(g => g.Count() > 1))
            throw new ValidationException(
                _localizer.Get<ValidationResource>("Validation_OpeningBalance_AccountOncePerBatch"),
                nameof(JournalEntry), nameof(RecordOpeningBalancesAsync));

        var eligible = await GetOpeningBalanceAccountsAsync(ct);
        var accountsById = eligible.ToDictionary(a => a.Id);

        foreach (var entry in entries)
        {
            if (!accountsById.ContainsKey(entry.AccountId))
                throw new ValidationException(
                    _localizer.Get<ValidationResource>("Validation_OpeningBalance_AccountNotEligible"),
                    nameof(Account), nameof(RecordOpeningBalancesAsync), entry.AccountId);
        }

        var undepositedFunds = entries.FirstOrDefault(e => e.AccountId == SystemAccounts.UndepositedFundsId);
        if (undepositedFunds is not null && undepositedFunds.Amount < 0m)
            throw new ValidationException(
                _localizer.Get<ValidationResource>("Validation_OpeningBalance_UndepositedFundsNegative"),
                nameof(Account), nameof(RecordOpeningBalancesAsync), SystemAccounts.UndepositedFundsId);

        await _unitOfWork.ExecuteInTransactionAsync(async innerCt =>
        {
            var now = DateTime.UtcNow;
            var description = $"Opening balance as at {request.AsAtDate:yyyy-MM-dd}";

            var journalEntry = await _journalRepo.AddAsync(new JournalEntry
            {
                Id = Guid.NewGuid(),
                Type = JournalEntryType.OpeningBalance,
                Date = request.AsAtDate,
                Description = description,
                CreatedAt = now
            }, innerCt);

            var lines = new List<Transaction>();
            foreach (var entry in entries)
            {
                var account = accountsById[entry.AccountId];
                var (debit, credit) = ToNormalSide(account.Type, entry.Amount);
                lines.Add(new Transaction
                {
                    Id = Guid.NewGuid(),
                    Date = request.AsAtDate,
                    AccountId = account.Id,
                    DebitAmount = debit,
                    CreditAmount = credit,
                    GLAccount = account.AccountNumber,
                    JournalEntryId = journalEntry.Id,
                    Description = description,
                    CreatedAt = now
                });
            }

            var plug = lines.Sum(l => l.DebitAmount) - lines.Sum(l => l.CreditAmount);
            if (plug != 0m)
            {
                lines.Add(new Transaction
                {
                    Id = Guid.NewGuid(),
                    Date = request.AsAtDate,
                    AccountId = SystemAccounts.OpeningBalanceEquityId,
                    DebitAmount = plug < 0m ? -plug : 0m,
                    CreditAmount = plug > 0m ? plug : 0m,
                    GLAccount = SystemAccounts.OpeningBalanceEquityNumber,
                    JournalEntryId = journalEntry.Id,
                    Description = description,
                    CreatedAt = now
                });
            }

            if (lines.Count > 0)
                await _glRepo.AddBalancedSetAsync(lines, innerCt);

            if (undepositedFunds is not null)
            {
                await _receivedItemRepo.AddAsync(new ReceivedItem
                {
                    Id = Guid.NewGuid(),
                    Date = request.AsAtDate,
                    Amount = undepositedFunds.Amount,
                    Method = PaymentMethod.Other,
                    JournalEntryId = journalEntry.Id,
                    CreatedAt = now
                }, innerCt);
            }

            await _audit.LogAsync(
                nameof(JournalEntry), journalEntry.Id, AuditAction.Create,
                oldValue: null,
                newValue: $"Opening balances ({entries.Count} accounts, plug {MoneyFormatter.Format(plug)} to Opening Balance Equity) as at {request.AsAtDate:yyyy-MM-dd}",
                ct: innerCt);

        }, ct);
    }

    /// <summary>
    /// Maps a signed balance to the account's normal side: positive amounts post as a
    /// debit for Asset/Expense and a credit for Liability/Equity/Income; negative
    /// amounts post the absolute value to the opposite side.
    /// </summary>
    private static (decimal Debit, decimal Credit) ToNormalSide(AccountType type, decimal amount)
    {
        var debitNormal = type is AccountType.Asset or AccountType.Expense;
        if (amount < 0m)
        {
            debitNormal = !debitNormal;
            amount = -amount;
        }

        return debitNormal ? (amount, 0m) : (0m, amount);
    }

    /// <summary>The inverse of <see cref="ToNormalSide"/>: a posted line as a signed balance.</summary>
    private static decimal FromNormalSide(AccountType type, Transaction line) =>
        type is AccountType.Asset or AccountType.Expense
            ? line.DebitAmount - line.CreditAmount
            : line.CreditAmount - line.DebitAmount;
}
