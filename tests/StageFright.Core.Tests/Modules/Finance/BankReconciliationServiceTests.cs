using NSubstitute;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Exceptions;
using StageFright.Core.Modules.Finance;
using StageFright.Core.Tests.Fixtures;

namespace StageFright.Core.Tests.Modules.Finance;

/// <summary>
/// Unit tests for BankReconciliationService — draft start validation (bank account,
/// single draft, statement-date ordering), workspace difference calculation,
/// toggle-clear rules, finalise gating at |diff| ≤ 0.005, and audit logging.
/// Spec 028 FR-015: finalisation still requires the reconciliation to balance (no
/// tolerance band beyond the half-cent), and a finalised reconciliation is immutable.
/// </summary>
public class BankReconciliationServiceTests : TestBase
{
    private readonly IBankReconciliationRepository _recRepo = Substitute.For<IBankReconciliationRepository>();
    private readonly IAccountRepository _accountRepo = Substitute.For<IAccountRepository>();
    private readonly IGLRepository _glRepo = Substitute.For<IGLRepository>();
    private readonly IAuditTrailService _audit = Substitute.For<IAuditTrailService>();

    private static readonly Guid BankAccountId = Guid.NewGuid();
    private static readonly Guid NonBankAccountId = Guid.NewGuid();
    private static readonly DateTime StatementDate = new(2026, 6, 30, 0, 0, 0, DateTimeKind.Utc);

    private readonly BankReconciliationService _sut;

    public BankReconciliationServiceTests()
    {
        _accountRepo.GetByIdAsync(BankAccountId, Arg.Any<CancellationToken>())
            .Returns(MakeBankAccount());
        _accountRepo.GetByIdAsync(NonBankAccountId, Arg.Any<CancellationToken>())
            .Returns(new Account
            {
                Id = NonBankAccountId, Name = "Equipment", Type = AccountType.Asset,
                AccountNumber = "1300", IsBankAccount = false
            });

        _recRepo.CreateDraftAsync(Arg.Any<Guid>(), Arg.Any<DateTime>(), Arg.Any<decimal>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(ci => new BankReconciliation
            {
                Id = Guid.NewGuid(),
                AccountId = ci.ArgAt<Guid>(0),
                StatementDate = ci.ArgAt<DateTime>(1),
                StatementClosingBalance = ci.ArgAt<decimal>(2),
                Status = ReconciliationStatus.Draft
            });

        _glRepo.GetUnreconciledByAccountAsync(Arg.Any<Guid>(), Arg.Any<DateTime?>(), Arg.Any<CancellationToken>())
            .Returns(new List<Transaction>());

        _sut = new BankReconciliationService(_recRepo, _accountRepo, _glRepo, _audit, RealLocalizer.Instance);
    }

    // --- StartDraftAsync ---

    [Fact]
    public async Task Should_ThrowEntityNotFound_When_StartingDraftForUnknownAccount()
    {
        await Assert.ThrowsAsync<EntityNotFoundException>(
            () => _sut.StartDraftAsync(MakeStartRequest(Guid.NewGuid()), Ct));
    }

    [Fact]
    public async Task Should_ThrowValidation_When_StartingDraftForNonBankAccount()
    {
        await Assert.ThrowsAsync<ValidationException>(
            () => _sut.StartDraftAsync(MakeStartRequest(NonBankAccountId), Ct));
    }

    [Fact]
    public async Task Should_ThrowReconciliation_When_AccountAlreadyHasDraft()
    {
        _recRepo.GetDraftForAccountAsync(BankAccountId, Arg.Any<CancellationToken>())
            .Returns(MakeReconciliation(ReconciliationStatus.Draft));

        await Assert.ThrowsAsync<ReconciliationException>(
            () => _sut.StartDraftAsync(MakeStartRequest(BankAccountId), Ct));
    }

    [Fact]
    public async Task Should_ThrowValidation_When_StatementDateNotAfterLastFinalised()
    {
        _recRepo.GetLastFinalisedForAccountAsync(BankAccountId, Arg.Any<CancellationToken>())
            .Returns(MakeReconciliation(ReconciliationStatus.Finalised, statementDate: StatementDate));

        await Assert.ThrowsAsync<ValidationException>(
            () => _sut.StartDraftAsync(MakeStartRequest(BankAccountId, statementDate: StatementDate), Ct));
    }

    [Fact]
    public async Task Should_CreateDraftAndAudit_When_RequestIsValid()
    {
        var draft = await _sut.StartDraftAsync(MakeStartRequest(BankAccountId, closingBalance: 250m), Ct);

        Assert.Equal(BankAccountId, draft.AccountId);
        Assert.Equal(250m, draft.StatementClosingBalance);
        await _recRepo.Received(1).CreateDraftAsync(BankAccountId, StatementDate, 250m, null, Arg.Any<CancellationToken>());
        await _audit.Received(1).LogAsync(nameof(BankReconciliation), draft.Id, AuditAction.Create,
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_AllowNewDraft_When_StatementDateAfterLastFinalised()
    {
        _recRepo.GetLastFinalisedForAccountAsync(BankAccountId, Arg.Any<CancellationToken>())
            .Returns(MakeReconciliation(ReconciliationStatus.Finalised, statementDate: StatementDate));

        var draft = await _sut.StartDraftAsync(
            MakeStartRequest(BankAccountId, statementDate: StatementDate.AddMonths(1)), Ct);

        Assert.Equal(ReconciliationStatus.Draft, draft.Status);
    }

    // --- GetWorkspaceAsync ---

    [Fact]
    public async Task Should_ThrowEntityNotFound_When_WorkspaceReconciliationMissing()
    {
        await Assert.ThrowsAsync<EntityNotFoundException>(() => _sut.GetWorkspaceAsync(Guid.NewGuid(), Ct));
    }

    [Fact]
    public async Task Should_ComputeClearedTotalAndDifference_When_LinesAreCleared()
    {
        // Opening 100; cleared: +200 deposit, −50 payment → cleared total 150.
        // Statement closing 250 → difference 250 − (100 + 150) = 0.
        var rec = MakeReconciliation(ReconciliationStatus.Draft, openingBalance: 100m, closingBalance: 250m);
        AddClearedLine(rec, debit: 200m);
        AddClearedLine(rec, credit: 50m);
        _recRepo.GetByIdAsync(rec.Id, Arg.Any<CancellationToken>()).Returns(rec);

        var workspace = await _sut.GetWorkspaceAsync(rec.Id, Ct);

        Assert.Equal(150m, workspace.ClearedTotal);
        Assert.Equal(0m, workspace.Difference);
        Assert.Equal(2, workspace.Transactions.Count);
        Assert.All(workspace.Transactions, t => Assert.True(t.IsCleared));
    }

    [Fact]
    public async Task Should_MergeUnclearedCandidates_When_BuildingWorkspace()
    {
        var rec = MakeReconciliation(ReconciliationStatus.Draft, closingBalance: 75m);
        AddClearedLine(rec, debit: 75m);
        _recRepo.GetByIdAsync(rec.Id, Arg.Any<CancellationToken>()).Returns(rec);
        _glRepo.GetUnreconciledByAccountAsync(BankAccountId, rec.StatementDate, Arg.Any<CancellationToken>())
            .Returns(new List<Transaction> { MakeBankTransaction(debit: 30m) });

        var workspace = await _sut.GetWorkspaceAsync(rec.Id, Ct);

        Assert.Equal(2, workspace.Transactions.Count);
        Assert.Single(workspace.Transactions, t => t.IsCleared);
        Assert.Single(workspace.Transactions, t => !t.IsCleared);
        Assert.Equal(0m, workspace.Difference);
    }

    // --- ToggleClearAsync ---

    [Fact]
    public async Task Should_ThrowEntityNotFound_When_TogglingOnUnknownReconciliation()
    {
        await Assert.ThrowsAsync<EntityNotFoundException>(
            () => _sut.ToggleClearAsync(Guid.NewGuid(), Guid.NewGuid(), Ct));
    }

    [Fact]
    public async Task Should_ThrowReconciliation_When_TogglingOnFinalisedReconciliation()
    {
        var rec = MakeReconciliation(ReconciliationStatus.Finalised);
        _recRepo.GetByIdAsync(rec.Id, Arg.Any<CancellationToken>()).Returns(rec);

        await Assert.ThrowsAsync<ReconciliationException>(
            () => _sut.ToggleClearAsync(rec.Id, Guid.NewGuid(), Ct));
    }

    [Fact]
    public async Task Should_RemoveLine_When_TransactionAlreadyCleared()
    {
        var rec = MakeReconciliation(ReconciliationStatus.Draft);
        var line = AddClearedLine(rec, debit: 40m);
        _recRepo.GetByIdAsync(rec.Id, Arg.Any<CancellationToken>()).Returns(rec);

        await _sut.ToggleClearAsync(rec.Id, line.TransactionId, Ct);

        await _recRepo.Received(1).RemoveLineAsync(rec.Id, line.TransactionId, Arg.Any<CancellationToken>());
        await _recRepo.DidNotReceive().AddLineAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_ThrowReconciliation_When_TransactionClearedByAnotherReconciliation()
    {
        var rec = MakeReconciliation(ReconciliationStatus.Draft);
        var txId = Guid.NewGuid();
        _recRepo.GetByIdAsync(rec.Id, Arg.Any<CancellationToken>()).Returns(rec);
        _recRepo.IsTransactionClearedElsewhereAsync(txId, rec.Id, Arg.Any<CancellationToken>()).Returns(true);

        await Assert.ThrowsAsync<ReconciliationException>(() => _sut.ToggleClearAsync(rec.Id, txId, Ct));
    }

    [Fact]
    public async Task Should_ThrowValidation_When_TransactionIsNotACandidate()
    {
        // Not on this account, or dated after the statement date — either way not in candidates.
        var rec = MakeReconciliation(ReconciliationStatus.Draft);
        _recRepo.GetByIdAsync(rec.Id, Arg.Any<CancellationToken>()).Returns(rec);

        await Assert.ThrowsAsync<ValidationException>(
            () => _sut.ToggleClearAsync(rec.Id, Guid.NewGuid(), Ct));
    }

    [Fact]
    public async Task Should_AddLine_When_TransactionIsUnclearedCandidate()
    {
        var rec = MakeReconciliation(ReconciliationStatus.Draft);
        var candidate = MakeBankTransaction(debit: 60m);
        _recRepo.GetByIdAsync(rec.Id, Arg.Any<CancellationToken>()).Returns(rec);
        _glRepo.GetUnreconciledByAccountAsync(BankAccountId, rec.StatementDate, Arg.Any<CancellationToken>())
            .Returns(new List<Transaction> { candidate });

        await _sut.ToggleClearAsync(rec.Id, candidate.Id, Ct);

        await _recRepo.Received(1).AddLineAsync(rec.Id, candidate.Id, Arg.Any<CancellationToken>());
    }

    // --- FinaliseAsync ---

    [Fact]
    public async Task Should_ThrowReconciliation_When_FinalisingAlreadyFinalised()
    {
        var rec = MakeReconciliation(ReconciliationStatus.Finalised, closingBalance: 0m);
        _recRepo.GetByIdAsync(rec.Id, Arg.Any<CancellationToken>()).Returns(rec);

        await Assert.ThrowsAsync<ReconciliationException>(() => _sut.FinaliseAsync(rec.Id, Ct));
    }

    [Fact]
    public async Task Should_ThrowReconciliation_When_FinalisingWithNonZeroDifference()
    {
        var rec = MakeReconciliation(ReconciliationStatus.Draft, closingBalance: 10m);
        _recRepo.GetByIdAsync(rec.Id, Arg.Any<CancellationToken>()).Returns(rec);

        await Assert.ThrowsAsync<ReconciliationException>(() => _sut.FinaliseAsync(rec.Id, Ct));
        await _recRepo.DidNotReceive().FinaliseAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_FinaliseAndAudit_When_DifferenceIsZero()
    {
        var rec = MakeReconciliation(ReconciliationStatus.Draft, closingBalance: 80m);
        AddClearedLine(rec, debit: 80m);
        _recRepo.GetByIdAsync(rec.Id, Arg.Any<CancellationToken>()).Returns(rec);

        await _sut.FinaliseAsync(rec.Id, Ct);

        await _recRepo.Received(1).FinaliseAsync(rec.Id, Arg.Any<CancellationToken>());
        await _audit.Received(1).LogAsync(nameof(BankReconciliation), rec.Id, AuditAction.Update,
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_Finalise_When_DifferenceIsWithinHalfCentTolerance()
    {
        var rec = MakeReconciliation(ReconciliationStatus.Draft, closingBalance: 0.004m);
        _recRepo.GetByIdAsync(rec.Id, Arg.Any<CancellationToken>()).Returns(rec);

        await _sut.FinaliseAsync(rec.Id, Ct);

        await _recRepo.Received(1).FinaliseAsync(rec.Id, Arg.Any<CancellationToken>());
    }

    // FR-015: finalisation still requires the reconciliation to balance — a one-cent
    // difference is over the half-cent tolerance and must be rejected with no state change.
    [Fact]
    public async Task Should_RejectFinalise_When_DifferenceExceedsHalfCent_FR015()
    {
        var rec = MakeReconciliation(ReconciliationStatus.Draft, closingBalance: 0.01m);
        _recRepo.GetByIdAsync(rec.Id, Arg.Any<CancellationToken>()).Returns(rec);

        await Assert.ThrowsAsync<ReconciliationException>(() => _sut.FinaliseAsync(rec.Id, Ct));
        await _recRepo.DidNotReceive().FinaliseAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    // FR-015: a finalised reconciliation is immutable — neither clearing a line nor
    // re-finalising it is permitted.
    [Fact]
    public async Task Should_RejectClearAndFinalise_When_ReconciliationIsFinalised_FR015()
    {
        var rec = MakeReconciliation(ReconciliationStatus.Finalised, closingBalance: 0m);
        _recRepo.GetByIdAsync(rec.Id, Arg.Any<CancellationToken>()).Returns(rec);

        await Assert.ThrowsAsync<ReconciliationException>(() => _sut.ToggleClearAsync(rec.Id, Guid.NewGuid(), Ct));
        await Assert.ThrowsAsync<ReconciliationException>(() => _sut.FinaliseAsync(rec.Id, Ct));
        await _recRepo.DidNotReceive().FinaliseAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    // --- DeleteDraftAsync ---

    [Fact]
    public async Task Should_ThrowEntityNotFound_When_DeletingUnknownReconciliation()
    {
        await Assert.ThrowsAsync<EntityNotFoundException>(() => _sut.DeleteDraftAsync(Guid.NewGuid(), Ct));
    }

    [Fact]
    public async Task Should_SoftDeleteAndAudit_When_DeletingDraft()
    {
        var rec = MakeReconciliation(ReconciliationStatus.Draft);
        _recRepo.GetByIdAsync(rec.Id, Arg.Any<CancellationToken>()).Returns(rec);

        await _sut.DeleteDraftAsync(rec.Id, Ct);

        await _recRepo.Received(1).SoftDeleteDraftAsync(rec.Id, "system", Arg.Any<CancellationToken>());
        await _audit.Received(1).LogAsync(nameof(BankReconciliation), rec.Id, AuditAction.Delete,
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    // --- Delegating lookups ---

    [Fact]
    public async Task Should_DelegateToRepository_When_GettingHistoryAndDraft()
    {
        await _sut.GetHistoryAsync(BankAccountId, Ct);
        await _sut.GetDraftForAccountAsync(BankAccountId, Ct);

        await _recRepo.Received(1).GetByAccountAsync(BankAccountId, Arg.Any<CancellationToken>());
        await _recRepo.Received(1).GetDraftForAccountAsync(BankAccountId, Arg.Any<CancellationToken>());
    }

    // --- Helpers ---

    private static Account MakeBankAccount() => new()
    {
        Id = BankAccountId, Name = "Operating Account", Type = AccountType.Asset,
        AccountNumber = "1110", IsBankAccount = true
    };

    private static StartReconciliationRequest MakeStartRequest(
        Guid accountId, DateTime? statementDate = null, decimal closingBalance = 0m) => new()
    {
        AccountId = accountId,
        StatementDate = statementDate ?? StatementDate,
        StatementClosingBalance = closingBalance
    };

    private static BankReconciliation MakeReconciliation(
        ReconciliationStatus status, decimal openingBalance = 0m, decimal closingBalance = 0m,
        DateTime? statementDate = null) => new()
    {
        Id = Guid.NewGuid(),
        AccountId = BankAccountId,
        Account = MakeBankAccount(),
        StatementDate = statementDate ?? StatementDate,
        OpeningBalance = openingBalance,
        StatementClosingBalance = closingBalance,
        Status = status
    };

    private static Transaction MakeBankTransaction(decimal debit = 0m, decimal credit = 0m) => new()
    {
        Id = Guid.NewGuid(),
        AccountId = BankAccountId,
        Date = StatementDate.AddDays(-7),
        DebitAmount = debit,
        CreditAmount = credit,
        GLAccount = "1110",
        CreatedAt = DateTime.UtcNow
    };

    private static ReconciliationLine AddClearedLine(BankReconciliation rec, decimal debit = 0m, decimal credit = 0m)
    {
        var transaction = MakeBankTransaction(debit, credit);
        var line = new ReconciliationLine
        {
            Id = Guid.NewGuid(),
            ReconciliationId = rec.Id,
            TransactionId = transaction.Id,
            Transaction = transaction,
            CreatedAt = DateTime.UtcNow
        };
        rec.Lines.Add(line);
        return line;
    }
}
