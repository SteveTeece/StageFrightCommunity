using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Exceptions;
using StageFright.Core.Localization;
using StageFright.Core.Modules.Localization.Resources;

namespace StageFright.Core.Modules.Finance;

/// <summary>
/// Banking (spec 033): deposits of received items and Petty Cash, cash withdrawals, deposit
/// reversals, and the Banking overview, history and deposit detail.
/// Postings (research §4, §5):
/// <list type="bullet">
/// <item>Deposit (<c>BankDeposit</c>): DR bank total · CR Undeposited Funds cheque total · CR Petty Cash cash amount.</item>
/// <item>Reversal (<c>BankDepositReversal</c>): the exact opposite of the deposit, on the deposit's date.</item>
/// <item>Withdrawal (<c>CashWithdrawal</c>): DR Petty Cash · CR bank.</item>
/// </list>
/// A zero line is never written. Each posting runs in one transaction and writes an audit entry.
/// </summary>
public class BankingService : IBankingService
{
    private const int MaxPurposeLength = 200;

    private readonly IAccountRepository _accountRepo;
    private readonly IGLRepository _glRepo;
    private readonly IJournalEntryRepository _journalRepo;
    private readonly IReceivedItemRepository _receivedItemRepo;
    private readonly IBankDepositRepository _depositRepo;
    private readonly IBankReconciliationRepository _reconciliationRepo;
    private readonly IClosedPeriodGuard _closedPeriodGuard;
    private readonly IAuditTrailService _audit;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILocalizer _localizer;

    public BankingService(
        IAccountRepository accountRepo,
        IGLRepository glRepo,
        IJournalEntryRepository journalRepo,
        IReceivedItemRepository receivedItemRepo,
        IBankDepositRepository depositRepo,
        IBankReconciliationRepository reconciliationRepo,
        IClosedPeriodGuard closedPeriodGuard,
        IAuditTrailService audit,
        IUnitOfWork unitOfWork,
        ILocalizer localizer)
    {
        _accountRepo = accountRepo;
        _glRepo = glRepo;
        _journalRepo = journalRepo;
        _receivedItemRepo = receivedItemRepo;
        _depositRepo = depositRepo;
        _reconciliationRepo = reconciliationRepo;
        _closedPeriodGuard = closedPeriodGuard;
        _audit = audit;
        _unitOfWork = unitOfWork;
        _localizer = localizer;
    }

    // --- Queries ---

    public async Task<BankingOverview> GetOverviewAsync(CancellationToken ct = default)
    {
        var items = await _receivedItemRepo.GetAwaitingAsync(ct);
        var accounts = await _accountRepo.GetAllAsync(ct);

        return new BankingOverview
        {
            AwaitingItems = items.Select(ToAwaitingItem).ToList(),
            UndepositedTotal = items.Sum(i => i.Amount),
            PettyCashBalance = await _glRepo.GetAccountBalanceAsync(SystemAccounts.CashId, DateTime.MaxValue, ct),
            CashAvailableToday = await GetCashAvailableAsync(DateTime.Today, ct),
            DepositBankAccounts = accounts
                .Where(IsDepositBankAccount)
                .OrderBy(a => a.SortOrder)
                .ThenBy(a => a.Name)
                .ToList()
        };
    }

    public async Task<decimal> GetCashAvailableAsync(DateTime depositDate, CancellationToken ct = default)
    {
        // Research §8: the balance at the end of the deposit's day and the current balance both bind.
        var endOfDay = depositDate.Date.AddDays(1).AddTicks(-1);
        var atDate = await _glRepo.GetAccountBalanceAsync(SystemAccounts.CashId, endOfDay, ct);
        var current = await _glRepo.GetAccountBalanceAsync(SystemAccounts.CashId, DateTime.MaxValue, ct);
        return Math.Min(atDate, current);
    }

    public async Task<IReadOnlyList<BankingHistoryEntry>> GetHistoryAsync(CancellationToken ct = default)
    {
        var deposits = await _depositRepo.GetAllAsync(ct);
        var journals = await _journalRepo.GetByTypesAsync(
            [JournalEntryType.BankDeposit, JournalEntryType.CashWithdrawal], ct);
        var journalsById = journals.ToDictionary(j => j.Id);
        var depositJournalIds = deposits.Select(d => d.JournalEntryId).ToHashSet();

        var rows = new List<(BankingHistoryEntry Entry, DateTime CreatedAt)>();

        foreach (var deposit in deposits)
        {
            rows.Add((new BankingHistoryEntry
            {
                Id = deposit.Id,
                Type = BankingHistoryEntryType.Deposit,
                Date = deposit.Date,
                BankAccountName = deposit.BankAccount?.Name ?? string.Empty,
                Total = deposit.Total,
                IsReversed = deposit.ReversalJournalEntryId is not null,
                Description = journalsById.GetValueOrDefault(deposit.JournalEntryId)?.Description
            }, deposit.CreatedAt));
        }

        foreach (var journal in journals.Where(j => !depositJournalIds.Contains(j.Id)))
        {
            var isWithdrawal = journal.Type == JournalEntryType.CashWithdrawal;
            // A legacy deposit debits the bank; a withdrawal credits it.
            var bankLine = isWithdrawal
                ? journal.Transactions.FirstOrDefault(t => t.CreditAmount > 0m)
                : journal.Transactions.FirstOrDefault(t => t.DebitAmount > 0m);

            rows.Add((new BankingHistoryEntry
            {
                Id = journal.Id,
                Type = isWithdrawal ? BankingHistoryEntryType.Withdrawal : BankingHistoryEntryType.Deposit,
                Date = journal.Date,
                BankAccountName = bankLine?.Account?.Name ?? string.Empty,
                Total = journal.Transactions.Sum(t => t.DebitAmount),
                IsLegacy = !isWithdrawal,
                Description = journal.Description
            }, journal.CreatedAt));
        }

        return rows
            .OrderByDescending(r => r.Entry.Date)
            .ThenByDescending(r => r.CreatedAt)
            .Select(r => r.Entry)
            .ToList();
    }

    public async Task<DepositDetail?> GetDepositAsync(Guid id, CancellationToken ct = default)
    {
        var deposit = await _depositRepo.GetByIdAsync(id, ct);
        if (deposit is not null)
            return await ToDetailAsync(deposit, ct);

        var legacy = (await _journalRepo.GetByTypesAsync([JournalEntryType.BankDeposit], ct))
            .FirstOrDefault(j => j.Id == id);
        if (legacy is null)
            return null;

        // The journal id of a deposit recorded since spec 033 opens that deposit, not a legacy view.
        var owner = (await _depositRepo.GetAllAsync(ct)).FirstOrDefault(d => d.JournalEntryId == id);
        if (owner is not null)
            return await GetDepositAsync(owner.Id, ct);

        var total = legacy.Transactions.Sum(t => t.DebitAmount);
        return new DepositDetail
        {
            Id = legacy.Id,
            Date = legacy.Date,
            BankAccountName = legacy.Transactions.FirstOrDefault(t => t.DebitAmount > 0m)?.Account?.Name ?? string.Empty,
            CashAmount = total, // a legacy deposit banked Petty Cash only
            Total = total,
            IsLegacy = true,
            CanReverse = false
        };
    }

    // --- Postings ---

    public async Task<BankDeposit> RecordDepositAsync(RecordDepositRequest request, CancellationToken ct = default)
    {
        if (request.CashAmount < 0m)
            throw Refusal("Validation_Deposit_CashNegative", nameof(BankDeposit), nameof(RecordDepositAsync));

        var itemIds = request.ReceivedItemIds.Distinct().ToList();
        if (itemIds.Count == 0 && request.CashAmount == 0m)
            throw Refusal("Validation_Deposit_Empty", nameof(BankDeposit), nameof(RecordDepositAsync));

        var bank = await RequireDepositBankAsync(request.BankAccountId, "Validation_Deposit_BankAccountInvalid", nameof(RecordDepositAsync), ct);

        BankDeposit? saved = null;
        await _unitOfWork.ExecuteInTransactionAsync(async innerCt =>
        {
            // Research §10: re-read inside the transaction so a racing deposit cannot take the same item twice.
            var items = itemIds.Count == 0
                ? []
                : await _receivedItemRepo.GetAwaitingByIdsAsync(itemIds, innerCt);
            if (items.Count != itemIds.Count)
                throw Refusal("Validation_Deposit_ItemNotAwaiting", nameof(ReceivedItem), nameof(RecordDepositAsync));

            if (items.Any(i => i.Date.Date > request.Date.Date))
                throw Refusal("Validation_Deposit_DateBeforeItem", nameof(BankDeposit), nameof(RecordDepositAsync));

            if (request.CashAmount > 0m)
            {
                var available = await GetCashAvailableAsync(request.Date, innerCt);
                if (request.CashAmount > available)
                    throw new ValidationException(
                        _localizer.Get<ValidationResource>("Validation_Deposit_CashExceedsAvailable", MoneyFormatter.Format(available)),
                        nameof(BankDeposit), nameof(RecordDepositAsync));
            }

            var now = DateTime.UtcNow;
            var chequeTotal = items.Sum(i => i.Amount);
            var total = chequeTotal + request.CashAmount;
            var description = string.IsNullOrWhiteSpace(request.Description)
                ? $"Bank deposit — {bank.Name}"
                : request.Description.Trim();

            var entry = await _journalRepo.AddAsync(new JournalEntry
            {
                Id = Guid.NewGuid(),
                Type = JournalEntryType.BankDeposit,
                Date = request.Date,
                Description = description,
                CreatedAt = now
            }, innerCt);

            // FR-013: one bank line for the full total; zero credit lines are left out.
            var lines = new List<Transaction>
            {
                Line(entry, request.Date, bank.Id, bank.AccountNumber, total, 0m, description, now)
            };
            if (chequeTotal > 0m)
                lines.Add(Line(entry, request.Date, SystemAccounts.UndepositedFundsId, SystemAccounts.UndepositedFundsNumber, 0m, chequeTotal, description, now));
            if (request.CashAmount > 0m)
                lines.Add(Line(entry, request.Date, SystemAccounts.CashId, SystemAccounts.CashNumber, 0m, request.CashAmount, description, now));

            await _glRepo.AddBalancedSetAsync(lines, innerCt);

            var depositId = Guid.NewGuid();
            saved = await _depositRepo.AddAsync(new BankDeposit
            {
                Id = depositId,
                Date = request.Date,
                BankAccountId = bank.Id,
                CashAmount = request.CashAmount,
                ChequeTotal = chequeTotal,
                JournalEntryId = entry.Id,
                CreatedAt = now,
                Lines = items.Select(i => new BankDepositLine
                {
                    Id = Guid.NewGuid(),
                    BankDepositId = depositId,
                    ReceivedItemId = i.Id,
                    CreatedAt = now
                }).ToList()
            }, innerCt);

            await _audit.LogAsync(
                nameof(BankDeposit), depositId, AuditAction.Create,
                oldValue: null,
                newValue: $"Bank deposit {MoneyFormatter.Format(total)} to '{bank.Name}' on {request.Date:yyyy-MM-dd}: {items.Count} item(s) {MoneyFormatter.Format(chequeTotal)}, cash {MoneyFormatter.Format(request.CashAmount)}",
                ct: innerCt);
        }, ct);

        return saved!;
    }

    public async Task<JournalEntry> RecordWithdrawalAsync(RecordCashWithdrawalRequest request, CancellationToken ct = default)
    {
        if (request.Amount <= 0m)
            throw Refusal("Validation_Withdrawal_AmountPositive", nameof(JournalEntry), nameof(RecordWithdrawalAsync));

        var purpose = string.IsNullOrWhiteSpace(request.Purpose) ? null : request.Purpose.Trim();
        if (purpose is { Length: > MaxPurposeLength })
            throw Refusal("Validation_Withdrawal_PurposeTooLong", nameof(JournalEntry), nameof(RecordWithdrawalAsync));

        var bank = await RequireDepositBankAsync(request.BankAccountId, "Validation_Withdrawal_BankAccountInvalid", nameof(RecordWithdrawalAsync), ct);

        JournalEntry? saved = null;
        await _unitOfWork.ExecuteInTransactionAsync(async innerCt =>
        {
            var now = DateTime.UtcNow;
            var description = purpose ?? $"Cash withdrawal — {bank.Name}";

            saved = await _journalRepo.AddAsync(new JournalEntry
            {
                Id = Guid.NewGuid(),
                Type = JournalEntryType.CashWithdrawal,
                Date = request.Date,
                Description = description,
                CreatedAt = now
            }, innerCt);

            await _glRepo.AddBalancedSetAsync(
            [
                Line(saved, request.Date, SystemAccounts.CashId, SystemAccounts.CashNumber, request.Amount, 0m, description, now),
                Line(saved, request.Date, bank.Id, bank.AccountNumber, 0m, request.Amount, description, now)
            ], innerCt);

            await _audit.LogAsync(
                nameof(JournalEntry), saved.Id, AuditAction.Create,
                oldValue: null,
                newValue: $"Cash withdrawal {MoneyFormatter.Format(request.Amount)} from '{bank.Name}' to Petty Cash on {request.Date:yyyy-MM-dd}",
                ct: innerCt);
        }, ct);

        return saved!;
    }

    public async Task ReverseDepositAsync(Guid bankDepositId, CancellationToken ct = default)
    {
        await _unitOfWork.ExecuteInTransactionAsync(async innerCt =>
        {
            var deposit = await _depositRepo.GetByIdAsync(bankDepositId, innerCt)
                ?? throw Refusal("Validation_Deposit_NotFound", nameof(BankDeposit), nameof(ReverseDepositAsync));

            // Research §9, in order: already reversed, finalised reconciliation, draft line removal,
            // then the opposite posting (whose GL write runs the closed-period guard), then the link.
            if (deposit.ReversalJournalEntryId is not null)
                throw Refusal("Validation_Deposit_AlreadyReversed", nameof(BankDeposit), nameof(ReverseDepositAsync));

            var original = deposit.JournalEntry?.Transactions ?? [];
            var bankLines = original.Where(t => t.AccountId == deposit.BankAccountId).ToList();

            foreach (var line in bankLines)
            {
                if (await _reconciliationRepo.IsTransactionInFinalisedAsync(line.Id, innerCt))
                    throw Refusal("Validation_Deposit_ReversalBlockedByFinalisedReconciliation", nameof(BankDeposit), nameof(ReverseDepositAsync));
            }

            foreach (var line in bankLines)
            {
                if (await _reconciliationRepo.GetDraftLineReconciliationIdAsync(line.Id, innerCt) is { } draftId)
                    await _reconciliationRepo.RemoveLineAsync(draftId, line.Id, innerCt);
            }

            var now = DateTime.UtcNow;
            var description = $"Reversal — {deposit.JournalEntry?.Description ?? "Bank deposit"}";

            var reversal = await _journalRepo.AddAsync(new JournalEntry
            {
                Id = Guid.NewGuid(),
                Type = JournalEntryType.BankDepositReversal,
                Date = deposit.Date,
                Description = description,
                CreatedAt = now
            }, innerCt);

            var lines = original
                .Select(t => Line(reversal, deposit.Date, t.AccountId, t.GLAccount, t.CreditAmount, t.DebitAmount, description, now))
                .ToList();

            await _glRepo.AddBalancedSetAsync(lines, innerCt);
            await _depositRepo.MarkReversedAsync(deposit.Id, reversal.Id, now, innerCt);

            await _audit.LogAsync(
                nameof(BankDeposit), deposit.Id, AuditAction.Update,
                oldValue: "Recorded",
                newValue: $"Reversed {MoneyFormatter.Format(deposit.Total)} deposit of {deposit.Date:yyyy-MM-dd} by journal entry {reversal.Id}",
                ct: innerCt);
        }, ct);
    }

    // --- Helpers ---

    /// <summary>A bank account a deposit or withdrawal may use: a bank account other than Petty Cash.</summary>
    private static bool IsDepositBankAccount(Account account) =>
        account.IsBankAccount && account.Id != SystemAccounts.CashId;

    private async Task<Account> RequireDepositBankAsync(Guid bankAccountId, string refusalKey, string operation, CancellationToken ct)
    {
        var accounts = await _accountRepo.GetAllAsync(ct);
        var bank = accounts.FirstOrDefault(a => a.Id == bankAccountId);
        if (bank is null || !IsDepositBankAccount(bank))
            throw Refusal(refusalKey, nameof(Account), operation);
        return bank;
    }

    private async Task<DepositDetail> ToDetailAsync(BankDeposit deposit, CancellationToken ct)
    {
        var reason = await CannotReverseReasonAsync(deposit, ct);
        return new DepositDetail
        {
            Id = deposit.Id,
            Date = deposit.Date,
            BankAccountName = deposit.BankAccount?.Name ?? string.Empty,
            CashAmount = deposit.CashAmount,
            Items = deposit.Lines
                .Where(l => l.ReceivedItem is not null)
                .Select(l => ToAwaitingItem(l.ReceivedItem!))
                .OrderBy(i => i.ReceivedDate)
                .ToList(),
            Total = deposit.Total,
            IsReversed = deposit.ReversalJournalEntryId is not null,
            ReversedAt = deposit.ReversedAt,
            CanReverse = reason is null,
            CannotReverseReason = reason
        };
    }

    /// <summary>The translated reason a reversal would be refused now, or null when it is allowed.</summary>
    private async Task<string?> CannotReverseReasonAsync(BankDeposit deposit, CancellationToken ct)
    {
        if (deposit.ReversalJournalEntryId is not null)
            return _localizer.Get<ValidationResource>("Validation_Deposit_AlreadyReversed");

        var bankLines = (deposit.JournalEntry?.Transactions ?? []).Where(t => t.AccountId == deposit.BankAccountId);
        foreach (var line in bankLines)
        {
            if (await _reconciliationRepo.IsTransactionInFinalisedAsync(line.Id, ct))
                return _localizer.Get<ValidationResource>("Validation_Deposit_ReversalBlockedByFinalisedReconciliation");
        }

        try
        {
            await _closedPeriodGuard.EnsureOpen(deposit.Date, ct);
        }
        catch (ClosedPeriodException)
        {
            return _localizer.Get<ValidationResource>("Validation_ClosedPeriod_PostingRejected");
        }

        return null;
    }

    /// <summary>Maps a received item to its display row, resolving the payer (research §13).</summary>
    private static AwaitingDepositItem ToAwaitingItem(ReceivedItem item)
    {
        var source = item.PaymentId is not null ? ReceivedItemSource.MemberPayment
            : item.JournalEntry?.Type == JournalEntryType.OpeningBalance ? ReceivedItemSource.OpeningBalance
            : ReceivedItemSource.IncomeEntry;

        var payer = source switch
        {
            ReceivedItemSource.MemberPayment => item.Payment?.Member?.FullName,
            ReceivedItemSource.IncomeEntry => item.JournalEntry?.ExternalOrganisation?.Name ?? item.JournalEntry?.Description,
            _ => null
        };

        return new AwaitingDepositItem
        {
            Id = item.Id,
            ReceivedDate = item.Date,
            Source = source,
            PayerName = payer,
            Reference = item.Reference,
            Amount = item.Amount
        };
    }

    private static Transaction Line(
        JournalEntry entry, DateTime date, Guid accountId, string glAccount,
        decimal debit, decimal credit, string description, DateTime now) => new()
    {
        Id = Guid.NewGuid(),
        Date = date,
        AccountId = accountId,
        DebitAmount = debit,
        CreditAmount = credit,
        GLAccount = glAccount,
        JournalEntryId = entry.Id,
        Description = description,
        CreatedAt = now
    };

    private ValidationException Refusal(string key, string entityType, string operation) =>
        new(_localizer.Get<ValidationResource>(key), entityType, operation);
}
