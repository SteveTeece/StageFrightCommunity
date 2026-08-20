using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Exceptions;

namespace StageFright.Core.Modules.Finance;

/// <summary>
/// Posts opening balances under an OpeningBalance journal entry: one line per
/// entered account at its normal side, with any residual plugged to the Opening
/// Balance Equity account (3100). Member Receivable and the tax clearing accounts
/// are excluded — those balances only arise from their own workflows.
/// </summary>
public class OpeningBalanceService : IOpeningBalanceService
{
    private static readonly Guid[] ExcludedAccountIds =
    {
        SystemAccounts.MemberReceivableId,
        SystemAccounts.TaxCollectedId,
        SystemAccounts.TaxPaidId,
        SystemAccounts.OpeningBalanceEquityId
    };

    private readonly IAccountRepository _accountRepo;
    private readonly IGLRepository _glRepo;
    private readonly IJournalEntryRepository _journalRepo;
    private readonly IAuditTrailService _audit;
    private readonly IUnitOfWork _unitOfWork;

    public OpeningBalanceService(
        IAccountRepository accountRepo,
        IGLRepository glRepo,
        IJournalEntryRepository journalRepo,
        IAuditTrailService audit,
        IUnitOfWork unitOfWork)
    {
        _accountRepo = accountRepo;
        _glRepo = glRepo;
        _journalRepo = journalRepo;
        _audit = audit;
        _unitOfWork = unitOfWork;
    }

    public async Task<IReadOnlyList<Account>> GetOpeningBalanceAccountsAsync(CancellationToken ct = default)
    {
        var all = await _accountRepo.GetAllAsync(ct);
        return all
            .Where(a => !ExcludedAccountIds.Contains(a.Id))
            .OrderBy(a => a.AccountNumber)
            .ToList();
    }

    public Task<bool> HasExistingOpeningBalancesAsync(CancellationToken ct = default) =>
        _journalRepo.AnyOfTypeAsync(JournalEntryType.OpeningBalance, ct);

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
        var entries = request.Entries.Where(e => e.Amount != 0m).ToList();
        if (entries.Count == 0)
            throw new ValidationException(
                "At least one non-zero opening balance is required.",
                nameof(JournalEntry), nameof(RecordOpeningBalancesAsync));

        if (entries.GroupBy(e => e.AccountId).Any(g => g.Count() > 1))
            throw new ValidationException(
                "Each account may only appear once in the opening balances.",
                nameof(JournalEntry), nameof(RecordOpeningBalancesAsync));

        var eligible = await GetOpeningBalanceAccountsAsync(ct);
        var accountsById = eligible.ToDictionary(a => a.Id);

        foreach (var entry in entries)
        {
            if (!accountsById.ContainsKey(entry.AccountId))
                throw new ValidationException(
                    "One or more accounts are not eligible for opening balances.",
                    nameof(Account), nameof(RecordOpeningBalancesAsync), entry.AccountId);
        }

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

            await _glRepo.AddBalancedSetAsync(lines, innerCt);

            await _audit.LogAsync(
                nameof(JournalEntry), journalEntry.Id, AuditAction.Create,
                oldValue: null,
                newValue: $"Opening balances ({entries.Count} accounts, plug {plug:C} to Opening Balance Equity) as at {request.AsAtDate:yyyy-MM-dd}",
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
}
