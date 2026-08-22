using NSubstitute;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Exceptions;
using StageFright.Core.Modules.Finance;
using StageFright.Core.Tests.Fixtures;

namespace StageFright.Core.Tests.Modules.Finance;

/// <summary>
/// Unit tests for OpeningBalanceService — normal-side posting, Opening Balance
/// Equity plug, account eligibility (only the Opening Balance Equity plug account
/// itself is excluded — Member Receivable and the tax clearing accounts are
/// eligible), rerun detection, and audit logging.
/// </summary>
public class OpeningBalanceServiceTests : TestBase
{
    private readonly IAccountRepository _accountRepo = Substitute.For<IAccountRepository>();
    private readonly IGLRepository _glRepo = Substitute.For<IGLRepository>();
    private readonly IJournalEntryRepository _journalRepo = Substitute.For<IJournalEntryRepository>();
    private readonly IAuditTrailService _audit = Substitute.For<IAuditTrailService>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    private static readonly Guid CashAccountId = SystemAccounts.CashId;
    private static readonly Guid LoanAccountId = Guid.NewGuid();
    private static readonly Guid IncomeAccountId = Guid.NewGuid();

    private readonly OpeningBalanceService _sut;

    public OpeningBalanceServiceTests()
    {
        _unitOfWork
            .ExecuteInTransactionAsync(Arg.Any<Func<CancellationToken, Task>>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.ArgAt<Func<CancellationToken, Task>>(0)(ci.ArgAt<CancellationToken>(1)));

        _journalRepo.AddAsync(Arg.Any<JournalEntry>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.ArgAt<JournalEntry>(0));

        _accountRepo.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Account>
            {
                MakeAccount(CashAccountId, "Cash on Hand", AccountType.Asset, "1100", isSystem: true, isBank: true),
                MakeAccount(SystemAccounts.MemberReceivableId, "Member Receivable", AccountType.Asset, "1200", isSystem: true),
                MakeAccount(SystemAccounts.TaxCollectedId, "GST Collected", AccountType.Liability, "2310", isSystem: true),
                MakeAccount(SystemAccounts.TaxPaidId, "GST Paid", AccountType.Liability, "2320", isSystem: true),
                MakeAccount(SystemAccounts.OpeningBalanceEquityId, "Opening Balance Equity", AccountType.Equity, "3100", isSystem: true),
                MakeAccount(LoanAccountId, "Committee Loan", AccountType.Liability, "2000"),
                MakeAccount(IncomeAccountId, "Raffle Income", AccountType.Income, "4000")
            });

        _sut = new OpeningBalanceService(_accountRepo, _glRepo, _journalRepo, _audit, _unitOfWork);
    }

    // --- GetOpeningBalanceAccountsAsync ---

    [Fact]
    public async Task Should_ExcludeOnlyPlugAccount_When_GettingEligibleAccounts()
    {
        var result = await _sut.GetOpeningBalanceAccountsAsync(Ct);

        Assert.Equal(6, result.Count);
        Assert.Contains(result, a => a.Id == SystemAccounts.MemberReceivableId);
        Assert.Contains(result, a => a.Id == SystemAccounts.TaxCollectedId);
        Assert.Contains(result, a => a.Id == SystemAccounts.TaxPaidId);
        Assert.DoesNotContain(result, a => a.Id == SystemAccounts.OpeningBalanceEquityId);
    }

    // --- HasExistingOpeningBalancesAsync ---

    [Fact]
    public async Task Should_ReportExistingOpeningBalances_When_OpeningBalanceJournalExists()
    {
        _journalRepo.AnyOfTypeAsync(JournalEntryType.OpeningBalance, Arg.Any<CancellationToken>())
            .Returns(true);

        Assert.True(await _sut.HasExistingOpeningBalancesAsync(Ct));
    }

    // --- ComputePlug ---

    [Fact]
    public async Task Should_ComputePositivePlug_When_DebitsExceedCredits()
    {
        var accounts = await _sut.GetOpeningBalanceAccountsAsync(Ct);
        var entries = new List<OpeningBalanceEntry>
        {
            new() { AccountId = CashAccountId, Amount = 5000m },
            new() { AccountId = LoanAccountId, Amount = 1000m }
        };

        // 5000 debit (asset) − 1000 credit (liability) = 4000 credited to equity.
        Assert.Equal(4000m, _sut.ComputePlug(entries, accounts));
    }

    [Fact]
    public async Task Should_ComputeNegativePlug_When_CreditsExceedDebits()
    {
        var accounts = await _sut.GetOpeningBalanceAccountsAsync(Ct);
        var entries = new List<OpeningBalanceEntry>
        {
            new() { AccountId = LoanAccountId, Amount = 3000m }
        };

        Assert.Equal(-3000m, _sut.ComputePlug(entries, accounts));
    }

    [Fact]
    public async Task Should_IgnoreZeroAndUnknownEntries_When_ComputingPlug()
    {
        var accounts = await _sut.GetOpeningBalanceAccountsAsync(Ct);
        var entries = new List<OpeningBalanceEntry>
        {
            new() { AccountId = CashAccountId, Amount = 0m },
            new() { AccountId = Guid.NewGuid(), Amount = 500m }
        };

        Assert.Equal(0m, _sut.ComputePlug(entries, accounts));
    }

    // --- Validation ---

    [Fact]
    public async Task Should_ThrowValidation_When_AllEntriesAreZero()
    {
        var request = MakeRequest(new OpeningBalanceEntry { AccountId = CashAccountId, Amount = 0m });

        await Assert.ThrowsAsync<ValidationException>(
            () => _sut.RecordOpeningBalancesAsync(request, Ct));
    }

    [Fact]
    public async Task Should_ThrowValidation_When_AccountAppearsTwice()
    {
        var request = MakeRequest(
            new OpeningBalanceEntry { AccountId = CashAccountId, Amount = 100m },
            new OpeningBalanceEntry { AccountId = CashAccountId, Amount = 200m });

        await Assert.ThrowsAsync<ValidationException>(
            () => _sut.RecordOpeningBalancesAsync(request, Ct));
    }

    [Fact]
    public async Task Should_ThrowValidation_When_EntryTargetsExcludedAccount()
    {
        var request = MakeRequest(
            new OpeningBalanceEntry { AccountId = SystemAccounts.OpeningBalanceEquityId, Amount = 100m });

        await Assert.ThrowsAsync<ValidationException>(
            () => _sut.RecordOpeningBalancesAsync(request, Ct));
    }

    [Fact]
    public async Task Should_AcceptEntry_When_TargetingMemberReceivableOrTaxAccount()
    {
        var request = MakeRequest(
            new OpeningBalanceEntry { AccountId = SystemAccounts.MemberReceivableId, Amount = 100m },
            new OpeningBalanceEntry { AccountId = SystemAccounts.TaxCollectedId, Amount = 50m },
            new OpeningBalanceEntry { AccountId = SystemAccounts.TaxPaidId, Amount = 25m });

        await _sut.RecordOpeningBalancesAsync(request, Ct);

        await _glRepo.Received(1).AddBalancedSetAsync(
            Arg.Is<IReadOnlyList<Transaction>>(lines =>
                lines!.Any(t => t.DebitAmount == 100m && t.AccountId == SystemAccounts.MemberReceivableId)
                && lines!.Any(t => t.CreditAmount == 50m && t.AccountId == SystemAccounts.TaxCollectedId)
                && lines!.Any(t => t.CreditAmount == 25m && t.AccountId == SystemAccounts.TaxPaidId)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_ThrowValidation_When_EntryTargetsUnknownAccount()
    {
        var request = MakeRequest(
            new OpeningBalanceEntry { AccountId = Guid.NewGuid(), Amount = 100m });

        await Assert.ThrowsAsync<ValidationException>(
            () => _sut.RecordOpeningBalancesAsync(request, Ct));
    }

    // --- Posting ---

    [Fact]
    public async Task Should_PostNormalSideLinesAndCreditPlug_When_DebitsExceedCredits()
    {
        var request = MakeRequest(
            new OpeningBalanceEntry { AccountId = CashAccountId, Amount = 5000m },
            new OpeningBalanceEntry { AccountId = LoanAccountId, Amount = 1000m });

        await _sut.RecordOpeningBalancesAsync(request, Ct);

        await _glRepo.Received(1).AddBalancedSetAsync(
            Arg.Is<IReadOnlyList<Transaction>>(lines =>
                lines!.Count == 3
                && lines.Any(t => t.DebitAmount == 5000m && t.AccountId == CashAccountId)
                && lines.Any(t => t.CreditAmount == 1000m && t.AccountId == LoanAccountId)
                && lines.Any(t => t.CreditAmount == 4000m && t.AccountId == SystemAccounts.OpeningBalanceEquityId && t.GLAccount == "3100")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_DebitPlug_When_CreditsExceedDebits()
    {
        var request = MakeRequest(
            new OpeningBalanceEntry { AccountId = LoanAccountId, Amount = 3000m });

        await _sut.RecordOpeningBalancesAsync(request, Ct);

        await _glRepo.Received(1).AddBalancedSetAsync(
            Arg.Is<IReadOnlyList<Transaction>>(lines =>
                lines!.Count == 2
                && lines.Any(t => t.CreditAmount == 3000m && t.AccountId == LoanAccountId)
                && lines.Any(t => t.DebitAmount == 3000m && t.AccountId == SystemAccounts.OpeningBalanceEquityId)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_PostOppositeSide_When_EnteredBalanceIsNegative()
    {
        // An overdrawn bank account: negative asset posts as a credit.
        var request = MakeRequest(
            new OpeningBalanceEntry { AccountId = CashAccountId, Amount = -200m });

        await _sut.RecordOpeningBalancesAsync(request, Ct);

        await _glRepo.Received(1).AddBalancedSetAsync(
            Arg.Is<IReadOnlyList<Transaction>>(lines =>
                lines!.Any(t => t.CreditAmount == 200m && t.AccountId == CashAccountId)
                && lines!.Any(t => t.DebitAmount == 200m && t.AccountId == SystemAccounts.OpeningBalanceEquityId)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_SkipZeroEntries_When_PostingOpeningBalances()
    {
        var request = MakeRequest(
            new OpeningBalanceEntry { AccountId = CashAccountId, Amount = 100m },
            new OpeningBalanceEntry { AccountId = LoanAccountId, Amount = 0m });

        await _sut.RecordOpeningBalancesAsync(request, Ct);

        await _glRepo.Received(1).AddBalancedSetAsync(
            Arg.Is<IReadOnlyList<Transaction>>(lines =>
                lines!.Count == 2 && lines.All(t => t.AccountId != LoanAccountId)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_CreateOpeningBalanceJournalEntry_When_Posted()
    {
        var request = MakeRequest(
            new OpeningBalanceEntry { AccountId = CashAccountId, Amount = 100m });

        await _sut.RecordOpeningBalancesAsync(request, Ct);

        await _journalRepo.Received(1).AddAsync(
            Arg.Is<JournalEntry>(j => j!.Type == JournalEntryType.OpeningBalance && j.Date == request.AsAtDate),
            Arg.Any<CancellationToken>());

        await _glRepo.Received(1).AddBalancedSetAsync(
            Arg.Is<IReadOnlyList<Transaction>>(lines => lines!.All(t => t.JournalEntryId != null)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_RunInsideUnitOfWork_When_Posted()
    {
        await _sut.RecordOpeningBalancesAsync(
            MakeRequest(new OpeningBalanceEntry { AccountId = CashAccountId, Amount = 100m }), Ct);

        await _unitOfWork.Received(1).ExecuteInTransactionAsync(
            Arg.Any<Func<CancellationToken, Task>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_WriteAuditEntry_When_Posted()
    {
        await _sut.RecordOpeningBalancesAsync(
            MakeRequest(new OpeningBalanceEntry { AccountId = CashAccountId, Amount = 100m }), Ct);

        await _audit.Received(1).LogAsync(
            Arg.Any<string>(), Arg.Any<Guid>(), AuditAction.Create,
            Arg.Any<string?>(), Arg.Any<string?>(),
            ct: Arg.Any<CancellationToken>());
    }

    // --- Helpers ---

    private static RecordOpeningBalancesRequest MakeRequest(params OpeningBalanceEntry[] entries) =>
        new()
        {
            AsAtDate = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
            Entries = entries.ToList()
        };

    private static Account MakeAccount(
        Guid id, string name, AccountType type, string number,
        bool isSystem = false, bool isBank = false) => new()
    {
        Id = id, Name = name, Type = type,
        AccountNumber = number, IsSystem = isSystem, IsBankAccount = isBank, SortOrder = 0,
        CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
    };
}
