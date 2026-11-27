using NSubstitute;
using NSubstitute.ExceptionExtensions;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Exceptions;
using StageFright.Core.Modules.Finance;
using StageFright.Core.Modules.Localization.Resources;
using StageFright.Core.Tests.Fixtures;

namespace StageFright.Core.Tests.Modules.Finance;

/// <summary>
/// Unit tests for <see cref="BankingService"/> (spec 033, research §4, §5, §8 to §11): the overview,
/// cash available, deposits, withdrawals, reversals, history and deposit detail.
/// </summary>
public class BankingServiceTests : TestBase
{
    private readonly IAccountRepository _accountRepo = Substitute.For<IAccountRepository>();
    private readonly IGLRepository _glRepo = Substitute.For<IGLRepository>();
    private readonly IJournalEntryRepository _journalRepo = Substitute.For<IJournalEntryRepository>();
    private readonly IReceivedItemRepository _itemRepo = Substitute.For<IReceivedItemRepository>();
    private readonly IBankDepositRepository _depositRepo = Substitute.For<IBankDepositRepository>();
    private readonly IBankReconciliationRepository _reconciliationRepo = Substitute.For<IBankReconciliationRepository>();
    private readonly IClosedPeriodGuard _closedPeriodGuard = Substitute.For<IClosedPeriodGuard>();
    private readonly IAuditTrailService _audit = Substitute.For<IAuditTrailService>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    private static readonly Guid BankId = Guid.NewGuid();
    private static readonly Guid SavingsId = Guid.NewGuid();
    private static readonly Guid ExpenseId = Guid.NewGuid();
    private static readonly DateTime DepositDate = new(2026, 6, 10, 0, 0, 0, DateTimeKind.Utc);

    private readonly List<IReadOnlyList<Transaction>> _posted = [];
    private readonly BankingService _sut;

    public BankingServiceTests()
    {
        _unitOfWork
            .ExecuteInTransactionAsync(Arg.Any<Func<CancellationToken, Task>>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.ArgAt<Func<CancellationToken, Task>>(0)(ci.ArgAt<CancellationToken>(1)));
        _journalRepo.AddAsync(Arg.Any<JournalEntry>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.ArgAt<JournalEntry>(0));
        _depositRepo.AddAsync(Arg.Any<BankDeposit>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.ArgAt<BankDeposit>(0));
        _journalRepo.GetByTypesAsync(Arg.Any<IReadOnlyCollection<JournalEntryType>>(), Arg.Any<CancellationToken>())
            .Returns(new List<JournalEntry>());
        _depositRepo.GetAllAsync(Arg.Any<CancellationToken>()).Returns(new List<BankDeposit>());
        _glRepo.When(r => r.AddBalancedSetAsync(Arg.Any<IReadOnlyList<Transaction>>(), Arg.Any<CancellationToken>()))
            .Do(ci => _posted.Add(ci.ArgAt<IReadOnlyList<Transaction>>(0)));

        _accountRepo.GetAllAsync(Arg.Any<CancellationToken>()).Returns(new List<Account>
        {
            MakeAccount(SavingsId, "Savings", "1120", isBank: true, sortOrder: 2),
            MakeAccount(SystemAccounts.CashId, "Petty Cash", SystemAccounts.CashNumber, isBank: true, isSystem: true),
            MakeAccount(SystemAccounts.UndepositedFundsId, "Undeposited Funds", SystemAccounts.UndepositedFundsNumber, isSystem: true),
            MakeAccount(BankId, "Operating", "1110", isBank: true, sortOrder: 1),
            MakeAccount(ExpenseId, "Hall Hire", "6000", type: AccountType.Expense)
        });

        SetCashBalances(atDate: 500m, current: 500m);

        _sut = new BankingService(_accountRepo, _glRepo, _journalRepo, _itemRepo, _depositRepo,
            _reconciliationRepo, _closedPeriodGuard, _audit, _unitOfWork, RealLocalizer.Instance);
    }

    // --- GetCashAvailableAsync ---

    [Theory]
    [InlineData(300, 500, 300)]
    [InlineData(500, 200, 200)]
    [InlineData(-20, 100, -20)]
    public async Task Should_ReturnLowerOfDateAndCurrentBalance_When_CashAvailableRequested(int atDate, int current, int expected)
    {
        SetCashBalances(atDate, current);

        var result = await _sut.GetCashAvailableAsync(DepositDate, Ct);

        Assert.Equal(expected, result);
    }

    [Fact]
    public async Task Should_QueryEndOfDepositDay_When_CashAvailableRequested()
    {
        await _sut.GetCashAvailableAsync(DepositDate.AddHours(9), Ct);

        await _glRepo.Received(1).GetAccountBalanceAsync(
            SystemAccounts.CashId, DepositDate.AddDays(1).AddTicks(-1), Arg.Any<CancellationToken>());
        await _glRepo.Received(1).GetAccountBalanceAsync(
            SystemAccounts.CashId, DateTime.MaxValue, Arg.Any<CancellationToken>());
    }

    // --- GetOverviewAsync ---

    [Fact]
    public async Task Should_ResolvePayerAndSource_When_OverviewListsAwaitingItems()
    {
        var member = MemberItem(40m, "Jane", "Smith");
        var organisation = IncomeItem(300m, "Grant", organisationName: "Valley Council");
        var described = IncomeItem(25m, "Raffle cheque");
        var opening = OpeningItem(60m);
        _itemRepo.GetAwaitingAsync(Arg.Any<CancellationToken>()).Returns(new List<ReceivedItem> { member, organisation, described, opening });

        var result = await _sut.GetOverviewAsync(Ct);

        Assert.Collection(result.AwaitingItems,
            i => { Assert.Equal(ReceivedItemSource.MemberPayment, i.Source); Assert.Equal("Jane Smith", i.PayerName); Assert.Equal("chq-1", i.Reference); },
            i => { Assert.Equal(ReceivedItemSource.IncomeEntry, i.Source); Assert.Equal("Valley Council", i.PayerName); },
            i => { Assert.Equal(ReceivedItemSource.IncomeEntry, i.Source); Assert.Equal("Raffle cheque", i.PayerName); },
            i => { Assert.Equal(ReceivedItemSource.OpeningBalance, i.Source); Assert.Null(i.PayerName); Assert.Equal(60m, i.Amount); });
        Assert.Equal(425m, result.UndepositedTotal);
    }

    [Fact]
    public async Task Should_ReturnBalancesAndDepositBanks_When_OverviewRequested()
    {
        SetCashBalances(atDate: 80m, current: 120m);
        _itemRepo.GetAwaitingAsync(Arg.Any<CancellationToken>()).Returns(new List<ReceivedItem>());

        var result = await _sut.GetOverviewAsync(Ct);

        Assert.Empty(result.AwaitingItems);
        Assert.Equal(0m, result.UndepositedTotal);
        Assert.Equal(120m, result.PettyCashBalance);
        Assert.Equal(80m, result.CashAvailableToday);
        Assert.Equal(new[] { BankId, SavingsId }, result.DepositBankAccounts.Select(a => a.Id));
    }

    // --- RecordDepositAsync: refusals ---

    [Fact]
    public async Task Should_RefuseAndPostNothing_When_DepositCashNegative()
    {
        var ex = await Assert.ThrowsAsync<ValidationException>(
            () => _sut.RecordDepositAsync(DepositRequest(cash: -1m), Ct));

        Assert.Equal(Text("Validation_Deposit_CashNegative"), ex.Message);
        await AssertNothingPostedAsync();
    }

    [Fact]
    public async Task Should_RefuseAndPostNothing_When_DepositHasNoItemsAndNoCash()
    {
        var ex = await Assert.ThrowsAsync<ValidationException>(
            () => _sut.RecordDepositAsync(DepositRequest(cash: 0m), Ct));

        Assert.Equal(Text("Validation_Deposit_Empty"), ex.Message);
        await AssertNothingPostedAsync();
    }

    public static TheoryData<Guid> InvalidBanks() => new() { Guid.NewGuid(), ExpenseId, SystemAccounts.UndepositedFundsId, SystemAccounts.CashId };

    [Theory]
    [MemberData(nameof(InvalidBanks))]
    public async Task Should_RefuseAndPostNothing_When_DepositBankAccountInvalid(Guid bankId)
    {
        var request = DepositRequest(cash: 10m);
        request.BankAccountId = bankId;

        var ex = await Assert.ThrowsAsync<ValidationException>(() => _sut.RecordDepositAsync(request, Ct));

        Assert.Equal(Text("Validation_Deposit_BankAccountInvalid"), ex.Message);
        await AssertNothingPostedAsync();
    }

    [Fact]
    public async Task Should_RefuseAndPostNothing_When_SelectedItemNoLongerAwaiting()
    {
        var awaiting = MemberItem(40m);
        var gone = Guid.NewGuid();
        StubAwaitingByIds(awaiting);

        var ex = await Assert.ThrowsAsync<ValidationException>(
            () => _sut.RecordDepositAsync(DepositRequest(0m, awaiting.Id, gone), Ct));

        Assert.Equal(Text("Validation_Deposit_ItemNotAwaiting"), ex.Message);
        await _journalRepo.DidNotReceiveWithAnyArgs().AddAsync(default!, TestContext.Current.CancellationToken);
        Assert.Empty(_posted);
    }

    [Fact]
    public async Task Should_RefuseAndPostNothing_When_DepositDateBeforeItemReceived()
    {
        var item = MemberItem(40m, date: DepositDate.AddDays(1));
        StubAwaitingByIds(item);

        var ex = await Assert.ThrowsAsync<ValidationException>(
            () => _sut.RecordDepositAsync(DepositRequest(0m, item.Id), Ct));

        Assert.Equal(Text("Validation_Deposit_DateBeforeItem"), ex.Message);
        Assert.Empty(_posted);
    }

    [Fact]
    public async Task Should_AcceptItem_When_ReceivedLaterOnTheDepositDay()
    {
        var item = MemberItem(40m, date: DepositDate.AddHours(15));
        StubAwaitingByIds(item);

        await _sut.RecordDepositAsync(DepositRequest(0m, item.Id), Ct);

        Assert.Single(_posted);
    }

    [Fact]
    public async Task Should_RefuseWithCashAvailable_When_DepositCashExceedsAvailable()
    {
        SetCashBalances(atDate: 40m, current: 90m);

        var ex = await Assert.ThrowsAsync<ValidationException>(
            () => _sut.RecordDepositAsync(DepositRequest(cash: 40.01m), Ct));

        Assert.Contains("40", ex.Message);
        Assert.StartsWith(Text("Validation_Deposit_CashExceedsAvailable").Split('{')[0], ex.Message);
        Assert.Empty(_posted);
    }

    // --- RecordDepositAsync: postings ---

    [Fact]
    public async Task Should_PostBankDebitAndPettyCashCreditOnly_When_CashOnlyDeposit()
    {
        SetCashBalances(atDate: 40m, current: 90m);

        var deposit = await _sut.RecordDepositAsync(DepositRequest(cash: 40m), Ct);

        var lines = Assert.Single(_posted);
        Assert.Equal(2, lines.Count);
        Assert.Contains(lines, t => t.AccountId == BankId && t.GLAccount == "1110" && t.DebitAmount == 40m);
        Assert.Contains(lines, t => t.AccountId == SystemAccounts.CashId && t.GLAccount == SystemAccounts.CashNumber && t.CreditAmount == 40m);
        Assert.Equal(0m, deposit.ChequeTotal);
        Assert.Equal(40m, deposit.CashAmount);
        Assert.Empty(deposit.Lines);
        await _itemRepo.DidNotReceiveWithAnyArgs().GetAwaitingByIdsAsync(default!, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Should_PostBankDebitAndUndepositedCreditOnly_When_ItemsOnlyDeposit()
    {
        var first = MemberItem(40m);
        var second = IncomeItem(110m, "Sponsorship");
        StubAwaitingByIds(first, second);

        var deposit = await _sut.RecordDepositAsync(DepositRequest(0m, first.Id, second.Id), Ct);

        var lines = Assert.Single(_posted);
        Assert.Equal(2, lines.Count);
        Assert.Contains(lines, t => t.AccountId == BankId && t.DebitAmount == 150m);
        Assert.Contains(lines, t => t.AccountId == SystemAccounts.UndepositedFundsId && t.GLAccount == "1105" && t.CreditAmount == 150m);
        Assert.Equal(150m, deposit.ChequeTotal);
        Assert.Equal(new[] { first.Id, second.Id }, deposit.Lines.Select(l => l.ReceivedItemId));
        Assert.All(deposit.Lines, l => Assert.Equal(deposit.Id, l.BankDepositId));
        await _glRepo.DidNotReceiveWithAnyArgs().GetAccountBalanceAsync(default, default, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Should_PostOneBankLineForTotalAndBalance_When_ItemsAndCashDeposited()
    {
        var item = MemberItem(60m);
        StubAwaitingByIds(item);

        var deposit = await _sut.RecordDepositAsync(DepositRequest(25m, item.Id), Ct);

        var lines = Assert.Single(_posted);
        Assert.Equal(3, lines.Count);
        Assert.Single(lines, t => t.AccountId == BankId);
        Assert.Contains(lines, t => t.AccountId == BankId && t.DebitAmount == 85m);
        Assert.Contains(lines, t => t.AccountId == SystemAccounts.UndepositedFundsId && t.CreditAmount == 60m);
        Assert.Contains(lines, t => t.AccountId == SystemAccounts.CashId && t.CreditAmount == 25m);
        Assert.Equal(lines.Sum(t => t.DebitAmount), lines.Sum(t => t.CreditAmount));
        Assert.All(lines, t => Assert.Equal(DepositDate, t.Date));
        Assert.Equal(85m, deposit.Total);
    }

    [Fact]
    public async Task Should_AcceptCash_When_CashEqualsAvailable()
    {
        SetCashBalances(atDate: 25m, current: 25m);

        await _sut.RecordDepositAsync(DepositRequest(cash: 25m), Ct);

        Assert.Single(_posted);
    }

    [Fact]
    public async Task Should_DepositEachItemOnce_When_RequestRepeatsAnItemId()
    {
        var item = MemberItem(60m);
        StubAwaitingByIds(item);

        var deposit = await _sut.RecordDepositAsync(DepositRequest(0m, item.Id, item.Id), Ct);

        Assert.Single(deposit.Lines);
        Assert.Equal(60m, deposit.ChequeTotal);
    }

    [Fact]
    public async Task Should_LinkDepositToBankDepositJournal_When_DepositRecorded()
    {
        JournalEntry? entry = null;
        _journalRepo.AddAsync(Arg.Any<JournalEntry>(), Arg.Any<CancellationToken>()).Returns(ci => entry = ci.ArgAt<JournalEntry>(0));

        var deposit = await _sut.RecordDepositAsync(DepositRequest(cash: 30m), Ct);

        Assert.NotNull(entry);
        Assert.Equal(JournalEntryType.BankDeposit, entry!.Type);
        Assert.Null(entry.ExternalOrganisationId);
        Assert.Equal(DepositDate, entry.Date);
        Assert.Contains("Operating", entry.Description);
        Assert.Equal(entry.Id, deposit.JournalEntryId);
        Assert.Equal(BankId, deposit.BankAccountId);
        Assert.All(_posted[0], t => Assert.Equal(entry.Id, t.JournalEntryId));
        await _depositRepo.Received(1).AddAsync(Arg.Any<BankDeposit>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_UseTrimmedNote_When_DepositDescriptionGiven()
    {
        var request = DepositRequest(cash: 30m);
        request.Description = "  Concert takings  ";

        await _sut.RecordDepositAsync(request, Ct);

        Assert.All(_posted[0], t => Assert.Equal("Concert takings", t.Description));
    }

    [Fact]
    public async Task Should_AuditDepositInsideTransaction_When_DepositRecorded()
    {
        var deposit = await _sut.RecordDepositAsync(DepositRequest(cash: 30m), Ct);

        await _unitOfWork.Received(1).ExecuteInTransactionAsync(Arg.Any<Func<CancellationToken, Task>>(), Arg.Any<CancellationToken>());
        await _audit.Received(1).LogAsync(
            nameof(BankDeposit), deposit.Id, AuditAction.Create,
            Arg.Any<string?>(), Arg.Is<string?>(v => v!.Contains("Operating")),
            ct: Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_SaveNoDeposit_When_GLPostingRefusedForClosedPeriod()
    {
        _glRepo.AddBalancedSetAsync(Arg.Any<IReadOnlyList<Transaction>>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new ClosedPeriodException("closed", "Settings", "EnsureOpen"));

        await Assert.ThrowsAsync<ClosedPeriodException>(() => _sut.RecordDepositAsync(DepositRequest(cash: 30m), Ct));

        await _depositRepo.DidNotReceiveWithAnyArgs().AddAsync(default!, TestContext.Current.CancellationToken);
    }

    // --- RecordWithdrawalAsync ---

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task Should_RefuseAndPostNothing_When_WithdrawalAmountNotPositive(int amount)
    {
        var ex = await Assert.ThrowsAsync<ValidationException>(
            () => _sut.RecordWithdrawalAsync(WithdrawalRequest(amount), Ct));

        Assert.Equal(Text("Validation_Withdrawal_AmountPositive"), ex.Message);
        await AssertNothingPostedAsync();
    }

    [Fact]
    public async Task Should_RefuseAndPostNothing_When_WithdrawalPurposeLongerThan200()
    {
        var ex = await Assert.ThrowsAsync<ValidationException>(
            () => _sut.RecordWithdrawalAsync(WithdrawalRequest(50m, new string('x', 201)), Ct));

        Assert.Equal(Text("Validation_Withdrawal_PurposeTooLong"), ex.Message);
        await AssertNothingPostedAsync();
    }

    [Theory]
    [MemberData(nameof(InvalidBanks))]
    public async Task Should_RefuseAndPostNothing_When_WithdrawalBankAccountInvalid(Guid bankId)
    {
        var request = WithdrawalRequest(50m);
        request.BankAccountId = bankId;

        var ex = await Assert.ThrowsAsync<ValidationException>(() => _sut.RecordWithdrawalAsync(request, Ct));

        Assert.Equal(Text("Validation_Withdrawal_BankAccountInvalid"), ex.Message);
        await AssertNothingPostedAsync();
    }

    [Fact]
    public async Task Should_DebitPettyCashCreditBank_When_WithdrawalRecorded()
    {
        var purpose = new string('x', 200);

        var entry = await _sut.RecordWithdrawalAsync(WithdrawalRequest(150m, $"  {purpose}  "), Ct);

        var lines = Assert.Single(_posted);
        Assert.Equal(2, lines.Count);
        Assert.Contains(lines, t => t.AccountId == SystemAccounts.CashId && t.GLAccount == SystemAccounts.CashNumber && t.DebitAmount == 150m);
        Assert.Contains(lines, t => t.AccountId == BankId && t.GLAccount == "1110" && t.CreditAmount == 150m);
        Assert.All(lines, t => Assert.Equal(entry.Id, t.JournalEntryId));
        Assert.Equal(JournalEntryType.CashWithdrawal, entry.Type);
        Assert.Equal(purpose, entry.Description);
        Assert.Null(entry.ExternalOrganisationId);
    }

    [Fact]
    public async Task Should_UseDefaultDescription_When_WithdrawalPurposeBlank()
    {
        var entry = await _sut.RecordWithdrawalAsync(WithdrawalRequest(150m, "   "), Ct);

        Assert.Contains("Operating", entry.Description);
    }

    [Fact]
    public async Task Should_AuditAgainstJournalEntry_When_WithdrawalRecorded()
    {
        var entry = await _sut.RecordWithdrawalAsync(WithdrawalRequest(150m), Ct);

        await _audit.Received(1).LogAsync(
            nameof(JournalEntry), entry.Id, AuditAction.Create,
            Arg.Any<string?>(), Arg.Any<string?>(), ct: Arg.Any<CancellationToken>());
    }

    // --- ReverseDepositAsync ---

    [Fact]
    public async Task Should_RefuseReversal_When_DepositNotFound()
    {
        var ex = await Assert.ThrowsAsync<ValidationException>(() => _sut.ReverseDepositAsync(Guid.NewGuid(), Ct));

        Assert.Equal(Text("Validation_Deposit_NotFound"), ex.Message);
        Assert.Empty(_posted);
    }

    [Fact]
    public async Task Should_RefuseReversalAndPostNothing_When_DepositAlreadyReversed()
    {
        var deposit = StubDeposit(reversed: true);

        var ex = await Assert.ThrowsAsync<ValidationException>(() => _sut.ReverseDepositAsync(deposit.Id, Ct));

        Assert.Equal(Text("Validation_Deposit_AlreadyReversed"), ex.Message);
        Assert.Empty(_posted);
        await _reconciliationRepo.DidNotReceiveWithAnyArgs().IsTransactionInFinalisedAsync(default, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Should_RefuseReversalAndPostNothing_When_BankLineInFinalisedReconciliation()
    {
        var deposit = StubDeposit();
        var bankLine = deposit.JournalEntry!.Transactions.Single(t => t.AccountId == BankId);
        _reconciliationRepo.IsTransactionInFinalisedAsync(bankLine.Id, Arg.Any<CancellationToken>()).Returns(true);

        var ex = await Assert.ThrowsAsync<ValidationException>(() => _sut.ReverseDepositAsync(deposit.Id, Ct));

        Assert.Equal(Text("Validation_Deposit_ReversalBlockedByFinalisedReconciliation"), ex.Message);
        Assert.Empty(_posted);
        await _reconciliationRepo.DidNotReceiveWithAnyArgs().RemoveLineAsync(default, default, TestContext.Current.CancellationToken);
        await _depositRepo.DidNotReceiveWithAnyArgs().MarkReversedAsync(default, default, default, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Should_RemoveDraftLine_When_BankLineClearedInDraftReconciliation()
    {
        var deposit = StubDeposit();
        var bankLine = deposit.JournalEntry!.Transactions.Single(t => t.AccountId == BankId);
        var draftId = Guid.NewGuid();
        _reconciliationRepo.GetDraftLineReconciliationIdAsync(bankLine.Id, Arg.Any<CancellationToken>()).Returns(draftId);

        await _sut.ReverseDepositAsync(deposit.Id, Ct);

        await _reconciliationRepo.Received(1).RemoveLineAsync(draftId, bankLine.Id, Arg.Any<CancellationToken>());
        await _reconciliationRepo.DidNotReceive().GetDraftLineReconciliationIdAsync(
            Arg.Is<Guid>(id => id != bankLine.Id), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_LeaveReconciliationsAlone_When_BankLineNotCleared()
    {
        var deposit = StubDeposit();

        await _sut.ReverseDepositAsync(deposit.Id, Ct);

        await _reconciliationRepo.DidNotReceiveWithAnyArgs().RemoveLineAsync(default, default, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Should_PostExactOppositeOnOriginalDate_When_DepositReversed()
    {
        var deposit = StubDeposit();
        JournalEntry? reversal = null;
        _journalRepo.AddAsync(Arg.Any<JournalEntry>(), Arg.Any<CancellationToken>()).Returns(ci => reversal = ci.ArgAt<JournalEntry>(0));

        await _sut.ReverseDepositAsync(deposit.Id, Ct);

        Assert.Equal(JournalEntryType.BankDepositReversal, reversal!.Type);
        Assert.Equal(DepositDate, reversal.Date);
        Assert.Null(reversal.ExternalOrganisationId);
        var lines = Assert.Single(_posted);
        Assert.Equal(3, lines.Count);
        Assert.Contains(lines, t => t.AccountId == BankId && t.GLAccount == "1110" && t.CreditAmount == 85m && t.DebitAmount == 0m);
        Assert.Contains(lines, t => t.AccountId == SystemAccounts.UndepositedFundsId && t.DebitAmount == 60m);
        Assert.Contains(lines, t => t.AccountId == SystemAccounts.CashId && t.DebitAmount == 25m);
        Assert.All(lines, t => Assert.Equal(DepositDate, t.Date));
        Assert.All(lines, t => Assert.Equal(reversal.Id, t.JournalEntryId));
        await _depositRepo.Received(1).MarkReversedAsync(deposit.Id, reversal.Id, Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_AuditReversalInsideTransaction_When_DepositReversed()
    {
        var deposit = StubDeposit();

        await _sut.ReverseDepositAsync(deposit.Id, Ct);

        await _unitOfWork.Received(1).ExecuteInTransactionAsync(Arg.Any<Func<CancellationToken, Task>>(), Arg.Any<CancellationToken>());
        await _audit.Received(1).LogAsync(
            nameof(BankDeposit), deposit.Id, AuditAction.Update,
            Arg.Any<string?>(), Arg.Any<string?>(), ct: Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_NotMarkReversed_When_ReversalDateInClosedPeriod()
    {
        var deposit = StubDeposit();
        _glRepo.AddBalancedSetAsync(Arg.Any<IReadOnlyList<Transaction>>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new ClosedPeriodException("closed", "Settings", "EnsureOpen"));

        await Assert.ThrowsAsync<ClosedPeriodException>(() => _sut.ReverseDepositAsync(deposit.Id, Ct));

        await _depositRepo.DidNotReceiveWithAnyArgs().MarkReversedAsync(default, default, default, TestContext.Current.CancellationToken);
        await _audit.DidNotReceiveWithAnyArgs().LogAsync(default!, default, default, ct: TestContext.Current.CancellationToken);
    }

    // --- GetHistoryAsync ---

    [Fact]
    public async Task Should_MergeDepositsLegacyDepositsAndWithdrawalsNewestFirst_When_HistoryRequested()
    {
        var deposit = MakeDeposit(date: DepositDate, reversed: true);
        var legacy = MakeJournal(JournalEntryType.BankDeposit, DepositDate.AddDays(-5), "Old deposit",
            (BankId, "Operating", 70m, 0m), (SystemAccounts.CashId, "Petty Cash", 0m, 70m));
        var withdrawal = MakeJournal(JournalEntryType.CashWithdrawal, DepositDate.AddDays(3), "Concert float",
            (SystemAccounts.CashId, "Petty Cash", 200m, 0m), (SavingsId, "Savings", 0m, 200m));
        _depositRepo.GetAllAsync(Arg.Any<CancellationToken>()).Returns(new List<BankDeposit> { deposit });
        _journalRepo.GetByTypesAsync(Arg.Any<IReadOnlyCollection<JournalEntryType>>(), Arg.Any<CancellationToken>())
            .Returns(new List<JournalEntry> { withdrawal, deposit.JournalEntry!, legacy });

        var history = await _sut.GetHistoryAsync(Ct);

        Assert.Collection(history,
            h =>
            {
                Assert.Equal(withdrawal.Id, h.Id);
                Assert.Equal(BankingHistoryEntryType.Withdrawal, h.Type);
                Assert.Equal("Savings", h.BankAccountName);
                Assert.Equal(200m, h.Total);
                Assert.Equal("Concert float", h.Description);
                Assert.False(h.IsReversed);
                Assert.False(h.IsLegacy);
            },
            h =>
            {
                Assert.Equal(deposit.Id, h.Id);
                Assert.Equal(BankingHistoryEntryType.Deposit, h.Type);
                Assert.Equal("Operating", h.BankAccountName);
                Assert.Equal(85m, h.Total);
                Assert.True(h.IsReversed);
                Assert.False(h.IsLegacy);
                Assert.Equal("Bank deposit — Operating", h.Description);
            },
            h =>
            {
                Assert.Equal(legacy.Id, h.Id);
                Assert.Equal(BankingHistoryEntryType.Deposit, h.Type);
                Assert.Equal("Operating", h.BankAccountName);
                Assert.Equal(70m, h.Total);
                Assert.True(h.IsLegacy);
            });
    }

    [Fact]
    public async Task Should_OrderByCreatedAt_When_HistoryRowsShareADate()
    {
        var earlier = MakeJournal(JournalEntryType.CashWithdrawal, DepositDate, "First",
            (SystemAccounts.CashId, "Petty Cash", 10m, 0m), (BankId, "Operating", 0m, 10m));
        earlier.CreatedAt = new DateTime(2026, 6, 10, 9, 0, 0, DateTimeKind.Utc);
        var later = MakeJournal(JournalEntryType.CashWithdrawal, DepositDate, "Second",
            (SystemAccounts.CashId, "Petty Cash", 20m, 0m), (BankId, "Operating", 0m, 20m));
        later.CreatedAt = new DateTime(2026, 6, 10, 10, 0, 0, DateTimeKind.Utc);
        _journalRepo.GetByTypesAsync(Arg.Any<IReadOnlyCollection<JournalEntryType>>(), Arg.Any<CancellationToken>())
            .Returns(new List<JournalEntry> { earlier, later });

        var history = await _sut.GetHistoryAsync(Ct);

        Assert.Equal(new[] { later.Id, earlier.Id }, history.Select(h => h.Id));
    }

    [Fact]
    public async Task Should_ReturnEmpty_When_NothingBanked()
    {
        Assert.Empty(await _sut.GetHistoryAsync(Ct));
    }

    // --- GetDepositAsync ---

    [Fact]
    public async Task Should_ReturnItemsAndAllowReversal_When_DepositOpenAndUnreconciled()
    {
        var deposit = StubDeposit();

        var detail = await _sut.GetDepositAsync(deposit.Id, Ct);

        Assert.NotNull(detail);
        Assert.Equal(deposit.Id, detail!.Id);
        Assert.Equal("Bank deposit — Operating", detail.Description);
        Assert.Equal("Operating", detail.BankAccountName);
        Assert.Equal(25m, detail.CashAmount);
        Assert.Equal(85m, detail.Total);
        var item = Assert.Single(detail.Items);
        Assert.Equal("Jane Smith", item.PayerName);
        Assert.False(detail.IsReversed);
        Assert.False(detail.IsLegacy);
        Assert.True(detail.CanReverse);
        Assert.Null(detail.CannotReverseReason);
    }

    [Fact]
    public async Task Should_GiveAlreadyReversedReason_When_DepositReversed()
    {
        var deposit = StubDeposit(reversed: true);

        var detail = await _sut.GetDepositAsync(deposit.Id, Ct);

        Assert.True(detail!.IsReversed);
        Assert.Equal(deposit.ReversedAt, detail.ReversedAt);
        Assert.False(detail.CanReverse);
        Assert.Equal(Text("Validation_Deposit_AlreadyReversed"), detail.CannotReverseReason);
    }

    [Fact]
    public async Task Should_GiveReconciledReason_When_BankLineInFinalisedReconciliation()
    {
        var deposit = StubDeposit();
        _reconciliationRepo.IsTransactionInFinalisedAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(true);

        var detail = await _sut.GetDepositAsync(deposit.Id, Ct);

        Assert.False(detail!.CanReverse);
        Assert.Equal(Text("Validation_Deposit_ReversalBlockedByFinalisedReconciliation"), detail.CannotReverseReason);
    }

    [Fact]
    public async Task Should_GiveClosedPeriodReason_When_DepositDateInClosedPeriod()
    {
        var deposit = StubDeposit();
        _closedPeriodGuard.EnsureOpen(DepositDate, Arg.Any<CancellationToken>())
            .ThrowsAsync(new ClosedPeriodException("closed", "Settings", "EnsureOpen"));

        var detail = await _sut.GetDepositAsync(deposit.Id, Ct);

        Assert.False(detail!.CanReverse);
        Assert.Equal(Text("Validation_ClosedPeriod_PostingRejected"), detail.CannotReverseReason);
    }

    [Fact]
    public async Task Should_ReturnLegacyDetail_When_IdIsAnUnlinkedDepositJournal()
    {
        var legacy = MakeJournal(JournalEntryType.BankDeposit, DepositDate, "Old deposit",
            (BankId, "Operating", 70m, 0m), (SystemAccounts.CashId, "Petty Cash", 0m, 70m));
        _journalRepo.GetByTypesAsync(Arg.Any<IReadOnlyCollection<JournalEntryType>>(), Arg.Any<CancellationToken>())
            .Returns(new List<JournalEntry> { legacy });

        var detail = await _sut.GetDepositAsync(legacy.Id, Ct);

        Assert.NotNull(detail);
        Assert.True(detail!.IsLegacy);
        Assert.Equal("Old deposit", detail.Description);
        Assert.Equal("Operating", detail.BankAccountName);
        Assert.Equal(70m, detail.CashAmount);
        Assert.Equal(70m, detail.Total);
        Assert.Empty(detail.Items);
        Assert.False(detail.CanReverse);
    }

    [Fact]
    public async Task Should_ReturnTheDeposit_When_IdIsItsJournalEntry()
    {
        var deposit = StubDeposit();
        _depositRepo.GetAllAsync(Arg.Any<CancellationToken>()).Returns(new List<BankDeposit> { deposit });
        _journalRepo.GetByTypesAsync(Arg.Any<IReadOnlyCollection<JournalEntryType>>(), Arg.Any<CancellationToken>())
            .Returns(new List<JournalEntry> { deposit.JournalEntry! });

        var detail = await _sut.GetDepositAsync(deposit.JournalEntryId, Ct);

        Assert.Equal(deposit.Id, detail!.Id);
        Assert.False(detail.IsLegacy);
    }

    [Fact]
    public async Task Should_ReturnNull_When_DepositUnknown()
    {
        Assert.Null(await _sut.GetDepositAsync(Guid.NewGuid(), Ct));
    }

    // --- Helpers ---

    private static string Text(string key) => RealLocalizer.Instance.Get<ValidationResource>(key);

    private async Task AssertNothingPostedAsync()
    {
        await _unitOfWork.DidNotReceiveWithAnyArgs().ExecuteInTransactionAsync(default!, TestContext.Current.CancellationToken);
        Assert.Empty(_posted);
    }

    private void SetCashBalances(decimal atDate, decimal current)
    {
        _glRepo.GetAccountBalanceAsync(SystemAccounts.CashId, Arg.Is<DateTime>(d => d != DateTime.MaxValue), Arg.Any<CancellationToken>())
            .Returns(atDate);
        _glRepo.GetAccountBalanceAsync(SystemAccounts.CashId, DateTime.MaxValue, Arg.Any<CancellationToken>())
            .Returns(current);
    }

    private void StubAwaitingByIds(params ReceivedItem[] items) =>
        _itemRepo.GetAwaitingByIdsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(items.ToList());

    private static RecordDepositRequest DepositRequest(decimal cash, params Guid[] itemIds) => new()
    {
        Date = DepositDate, BankAccountId = BankId, CashAmount = cash, ReceivedItemIds = itemIds
    };

    private static RecordCashWithdrawalRequest WithdrawalRequest(decimal amount, string? purpose = null) => new()
    {
        Date = DepositDate, BankAccountId = BankId, Amount = amount, Purpose = purpose
    };

    private static ReceivedItem MemberItem(decimal amount, string first = "Jane", string last = "Smith", DateTime? date = null) => new()
    {
        Id = Guid.NewGuid(), Date = date ?? DepositDate.AddDays(-3), Amount = amount, Method = PaymentMethod.Check,
        Reference = "chq-1", PaymentId = Guid.NewGuid(), CreatedAt = DateTime.UtcNow,
        Payment = new Payment { Member = new Member { FirstName = first, LastName = last } }
    };

    private static ReceivedItem IncomeItem(decimal amount, string description, string? organisationName = null)
    {
        var entry = new JournalEntry
        {
            Id = Guid.NewGuid(), Type = JournalEntryType.Income, Description = description,
            ExternalOrganisation = organisationName is null ? null : new ExternalOrganisation { Name = organisationName, IsDeleted = true }
        };
        return new ReceivedItem
        {
            Id = Guid.NewGuid(), Date = DepositDate.AddDays(-2), Amount = amount, Method = PaymentMethod.Check,
            JournalEntryId = entry.Id, JournalEntry = entry, CreatedAt = DateTime.UtcNow
        };
    }

    private static ReceivedItem OpeningItem(decimal amount)
    {
        var entry = new JournalEntry { Id = Guid.NewGuid(), Type = JournalEntryType.OpeningBalance, Description = "Opening balances" };
        return new ReceivedItem
        {
            Id = Guid.NewGuid(), Date = DepositDate.AddDays(-30), Amount = amount, Method = PaymentMethod.Other,
            JournalEntryId = entry.Id, JournalEntry = entry, CreatedAt = DateTime.UtcNow
        };
    }

    /// <summary>A deposit of one 60.00 member cheque plus 25.00 cash into Operating.</summary>
    private static BankDeposit MakeDeposit(DateTime date, bool reversed = false)
    {
        var item = MemberItem(60m);
        var depositId = Guid.NewGuid();
        var journal = MakeJournal(JournalEntryType.BankDeposit, date, "Bank deposit — Operating",
            (BankId, "Operating", 85m, 0m),
            (SystemAccounts.UndepositedFundsId, "Undeposited Funds", 0m, 60m),
            (SystemAccounts.CashId, "Petty Cash", 0m, 25m));
        return new BankDeposit
        {
            Id = depositId, Date = date, BankAccountId = BankId, CashAmount = 25m, ChequeTotal = 60m,
            JournalEntryId = journal.Id, JournalEntry = journal, CreatedAt = DateTime.UtcNow,
            BankAccount = MakeAccount(BankId, "Operating", "1110", isBank: true),
            ReversalJournalEntryId = reversed ? Guid.NewGuid() : null,
            ReversedAt = reversed ? new DateTime(2026, 6, 20, 0, 0, 0, DateTimeKind.Utc) : null,
            Lines = [new BankDepositLine { Id = Guid.NewGuid(), BankDepositId = depositId, ReceivedItemId = item.Id, ReceivedItem = item }]
        };
    }

    private BankDeposit StubDeposit(bool reversed = false)
    {
        var deposit = MakeDeposit(DepositDate, reversed);
        _depositRepo.GetByIdAsync(deposit.Id, Arg.Any<CancellationToken>()).Returns(deposit);
        return deposit;
    }

    private static JournalEntry MakeJournal(
        JournalEntryType type, DateTime date, string description,
        params (Guid AccountId, string Name, decimal Debit, decimal Credit)[] lines)
    {
        var entry = new JournalEntry { Id = Guid.NewGuid(), Type = type, Date = date, Description = description, CreatedAt = DateTime.UtcNow };
        entry.Transactions = lines.Select(l => new Transaction
        {
            Id = Guid.NewGuid(), Date = date, AccountId = l.AccountId, DebitAmount = l.Debit, CreditAmount = l.Credit,
            GLAccount = l.AccountId == BankId ? "1110" : l.AccountId == SavingsId ? "1120"
                : l.AccountId == SystemAccounts.CashId ? SystemAccounts.CashNumber : SystemAccounts.UndepositedFundsNumber,
            JournalEntryId = entry.Id, CreatedAt = DateTime.UtcNow,
            Account = new Account { Id = l.AccountId, Name = l.Name }
        }).ToList();
        return entry;
    }

    private static Account MakeAccount(
        Guid id, string name, string number, AccountType type = AccountType.Asset,
        bool isBank = false, bool isSystem = false, int sortOrder = 0) => new()
    {
        Id = id, Name = name, Type = type, AccountNumber = number, IsBankAccount = isBank, IsSystem = isSystem,
        SortOrder = sortOrder, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
    };
}
