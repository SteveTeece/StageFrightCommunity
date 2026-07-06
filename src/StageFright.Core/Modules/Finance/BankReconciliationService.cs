using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Exceptions;

namespace StageFright.Core.Modules.Finance;

/// <summary>
/// Manual bank reconciliation workflow. One draft per account; drafts are ticked off
/// against a paper statement and finalised only when the difference is $0.00.
/// Finalised reconciliations are immutable and undeletable. GL Transaction rows are
/// never modified — cleared state lives entirely in ReconciliationLine join rows.
/// </summary>
public class BankReconciliationService : IBankReconciliationService
{
    private const decimal FinaliseTolerance = 0.005m;

    private readonly IBankReconciliationRepository _reconciliationRepo;
    private readonly IAccountRepository _accountRepo;
    private readonly IGLRepository _glRepo;
    private readonly IAuditTrailService _audit;

    public BankReconciliationService(
        IBankReconciliationRepository reconciliationRepo,
        IAccountRepository accountRepo,
        IGLRepository glRepo,
        IAuditTrailService audit)
    {
        _reconciliationRepo = reconciliationRepo;
        _accountRepo = accountRepo;
        _glRepo = glRepo;
        _audit = audit;
    }

    public Task<IReadOnlyList<BankReconciliation>> GetHistoryAsync(Guid accountId, CancellationToken ct = default) =>
        _reconciliationRepo.GetByAccountAsync(accountId, ct);

    public Task<BankReconciliation?> GetDraftForAccountAsync(Guid accountId, CancellationToken ct = default) =>
        _reconciliationRepo.GetDraftForAccountAsync(accountId, ct);

    public async Task<BankReconciliation> StartDraftAsync(StartReconciliationRequest request, CancellationToken ct = default)
    {
        var account = await _accountRepo.GetByIdAsync(request.AccountId, ct)
            ?? throw new EntityNotFoundException(nameof(Account), request.AccountId, nameof(StartDraftAsync));

        if (!account.IsBankAccount)
            throw new ValidationException(
                "Only bank/cash accounts can be reconciled.",
                nameof(Account), nameof(StartDraftAsync), request.AccountId);

        if (await _reconciliationRepo.GetDraftForAccountAsync(request.AccountId, ct) is not null)
            throw new ReconciliationException(
                "This account already has a draft reconciliation. Finalise or delete it first.",
                nameof(BankReconciliation), nameof(StartDraftAsync));

        var lastFinalised = await _reconciliationRepo.GetLastFinalisedForAccountAsync(request.AccountId, ct);
        if (lastFinalised is not null && request.StatementDate <= lastFinalised.StatementDate)
            throw new ValidationException(
                $"The statement date must be after the last finalised statement date ({lastFinalised.StatementDate:yyyy-MM-dd}).",
                nameof(BankReconciliation), nameof(StartDraftAsync));

        var draft = await _reconciliationRepo.CreateDraftAsync(
            request.AccountId, request.StatementDate, request.StatementClosingBalance, request.Notes, ct);

        await _audit.LogAsync(nameof(BankReconciliation), draft.Id, AuditAction.Create,
            oldValue: null,
            newValue: $"Draft reconciliation for '{account.Name}' to {request.StatementDate:yyyy-MM-dd}, closing balance {request.StatementClosingBalance:C}",
            ct: ct);

        return draft;
    }

    public async Task<ReconciliationWorkspaceView> GetWorkspaceAsync(Guid reconciliationId, CancellationToken ct = default)
    {
        var reconciliation = await _reconciliationRepo.GetByIdAsync(reconciliationId, ct)
            ?? throw new EntityNotFoundException(nameof(BankReconciliation), reconciliationId, nameof(GetWorkspaceAsync));

        var account = reconciliation.Account
            ?? await _accountRepo.GetByIdAsync(reconciliation.AccountId, ct)
            ?? throw new EntityNotFoundException(nameof(Account), reconciliation.AccountId, nameof(GetWorkspaceAsync));

        var cleared = reconciliation.Lines
            .Where(l => l.Transaction is not null)
            .Select(l => l.Transaction!)
            .ToList();

        var unreconciled = await _glRepo.GetUnreconciledByAccountAsync(
            reconciliation.AccountId, reconciliation.StatementDate, ct);

        var transactions = cleared
            .Select(t => new ReconciliationTransactionView { Transaction = t, IsCleared = true })
            .Concat(unreconciled.Select(t => new ReconciliationTransactionView { Transaction = t, IsCleared = false }))
            .OrderBy(v => v.Transaction.Date)
            .ThenBy(v => v.Transaction.CreatedAt)
            .ToList();

        var clearedTotal = cleared.Sum(t => t.DebitAmount) - cleared.Sum(t => t.CreditAmount);
        var difference = reconciliation.StatementClosingBalance - (reconciliation.OpeningBalance + clearedTotal);

        return new ReconciliationWorkspaceView
        {
            Reconciliation = reconciliation,
            Account = account,
            Transactions = transactions,
            ClearedTotal = clearedTotal,
            Difference = difference
        };
    }

    public async Task ToggleClearAsync(Guid reconciliationId, Guid transactionId, CancellationToken ct = default)
    {
        var reconciliation = await _reconciliationRepo.GetByIdAsync(reconciliationId, ct)
            ?? throw new EntityNotFoundException(nameof(BankReconciliation), reconciliationId, nameof(ToggleClearAsync));

        if (reconciliation.Status == ReconciliationStatus.Finalised)
            throw new ReconciliationException(
                "Finalised reconciliations are immutable and cannot be modified.",
                nameof(BankReconciliation), nameof(ToggleClearAsync), reconciliationId);

        if (reconciliation.Lines.Any(l => l.TransactionId == transactionId))
        {
            await _reconciliationRepo.RemoveLineAsync(reconciliationId, transactionId, ct);
            return;
        }

        if (await _reconciliationRepo.IsTransactionClearedElsewhereAsync(transactionId, reconciliationId, ct))
            throw new ReconciliationException(
                "This transaction is already cleared by another reconciliation.",
                nameof(ReconciliationLine), nameof(ToggleClearAsync), transactionId);

        var candidates = await _glRepo.GetUnreconciledByAccountAsync(
            reconciliation.AccountId, reconciliation.StatementDate, ct);

        if (candidates.All(t => t.Id != transactionId))
            throw new ValidationException(
                "The transaction does not belong to this account or is dated after the statement date.",
                nameof(Transaction), nameof(ToggleClearAsync), transactionId);

        await _reconciliationRepo.AddLineAsync(reconciliationId, transactionId, ct);
    }

    public async Task FinaliseAsync(Guid reconciliationId, CancellationToken ct = default)
    {
        var workspace = await GetWorkspaceAsync(reconciliationId, ct);

        if (workspace.Reconciliation.Status == ReconciliationStatus.Finalised)
            throw new ReconciliationException(
                "This reconciliation is already finalised.",
                nameof(BankReconciliation), nameof(FinaliseAsync), reconciliationId);

        if (Math.Abs(workspace.Difference) > FinaliseTolerance)
            throw new ReconciliationException(
                $"Cannot finalise: the difference is {workspace.Difference:C}, not $0.00.",
                nameof(BankReconciliation), nameof(FinaliseAsync), reconciliationId);

        await _reconciliationRepo.FinaliseAsync(reconciliationId, ct);

        await _audit.LogAsync(nameof(BankReconciliation), reconciliationId, AuditAction.Update,
            oldValue: nameof(ReconciliationStatus.Draft),
            newValue: $"Finalised reconciliation for '{workspace.Account.Name}' to {workspace.Reconciliation.StatementDate:yyyy-MM-dd}",
            ct: ct);
    }

    public async Task DeleteDraftAsync(Guid reconciliationId, CancellationToken ct = default)
    {
        var reconciliation = await _reconciliationRepo.GetByIdAsync(reconciliationId, ct)
            ?? throw new EntityNotFoundException(nameof(BankReconciliation), reconciliationId, nameof(DeleteDraftAsync));

        await _reconciliationRepo.SoftDeleteDraftAsync(reconciliationId, "system", ct);

        await _audit.LogAsync(nameof(BankReconciliation), reconciliationId, AuditAction.Delete,
            oldValue: $"Draft reconciliation to {reconciliation.StatementDate:yyyy-MM-dd}",
            newValue: null, ct: ct);
    }
}
