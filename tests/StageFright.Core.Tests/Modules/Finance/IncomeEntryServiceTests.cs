using NSubstitute;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Exceptions;
using StageFright.Core.Modules.Finance;
using StageFright.Core.Tests.Fixtures;

namespace StageFright.Core.Tests.Modules.Finance;

/// <summary>
/// Unit tests for IncomeEntryService — non-member income recording with a journal
/// entry + balanced GL set, deposit-account selection (default Cash 1100), account
/// validation, amount validation, and audit logging.
/// </summary>
public class IncomeEntryServiceTests : TestBase
{
    private readonly IAccountRepository _accountRepo = Substitute.For<IAccountRepository>();
    private readonly IGLRepository _glRepo = Substitute.For<IGLRepository>();
    private readonly IJournalEntryRepository _journalRepo = Substitute.For<IJournalEntryRepository>();
    private readonly ISettingsRepository _settingsRepo = Substitute.For<ISettingsRepository>();
    private readonly IAuditTrailService _audit = Substitute.For<IAuditTrailService>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    private static readonly Guid IncomeAccountId = Guid.NewGuid();
    private static readonly Guid ExpenseAccountId = Guid.NewGuid();
    private static readonly Guid SystemIncomeAccountId = Guid.NewGuid();
    private static readonly Guid BankAccountId = Guid.NewGuid();
    private static readonly Guid CashAccountId = SystemAccounts.CashId;

    private readonly IncomeEntryService _sut;

    public IncomeEntryServiceTests()
    {
        _unitOfWork
            .ExecuteInTransactionAsync(Arg.Any<Func<CancellationToken, Task>>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.ArgAt<Func<CancellationToken, Task>>(0)(ci.ArgAt<CancellationToken>(1)));

        _journalRepo.AddAsync(Arg.Any<JournalEntry>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.ArgAt<JournalEntry>(0));

        _accountRepo.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Account>
            {
                MakeAccount(IncomeAccountId, "Raffle Income", AccountType.Income, "4000"),
                MakeAccount(ExpenseAccountId, "Hall Hire", AccountType.Expense, "6000"),
                MakeAccount(SystemIncomeAccountId, "System Income", AccountType.Income, "4999", isSystem: true),
                MakeAccount(CashAccountId, "Cash on Hand", AccountType.Asset, "1100", isSystem: true, isBank: true),
                MakeAccount(BankAccountId, "Operating Account", AccountType.Asset, "1110", isBank: true)
            });

        _sut = new IncomeEntryService(_accountRepo, _glRepo, _journalRepo, _settingsRepo, _audit, _unitOfWork);
    }

    // --- GetIncomeAccountsAsync ---

    [Fact]
    public async Task GetIncomeAccountsAsync_ReturnsOnlyNonSystemIncomeAccounts()
    {
        var result = await _sut.GetIncomeAccountsAsync(Ct);

        Assert.Single(result);
        Assert.Equal(IncomeAccountId, result[0].Id);
    }

    [Fact]
    public async Task GetIncomeAccountsAsync_ExcludesExpenseAccounts()
    {
        var result = await _sut.GetIncomeAccountsAsync(Ct);

        Assert.DoesNotContain(result, c => c.Type == AccountType.Expense);
    }

    [Fact]
    public async Task GetIncomeAccountsAsync_ExcludesSystemAccounts()
    {
        var result = await _sut.GetIncomeAccountsAsync(Ct);

        Assert.DoesNotContain(result, c => c.IsSystem);
    }

    // --- RecordIncomeAsync: validation ---

    [Fact]
    public async Task RecordIncomeAsync_ThrowsValidation_WhenAmountIsZero()
    {
        var request = MakeRequest(0m);

        await Assert.ThrowsAsync<ValidationException>(
            () => _sut.RecordIncomeAsync(request, Ct));
    }

    [Fact]
    public async Task RecordIncomeAsync_ThrowsValidation_WhenAmountIsNegative()
    {
        var request = MakeRequest(-5m);

        await Assert.ThrowsAsync<ValidationException>(
            () => _sut.RecordIncomeAsync(request, Ct));
    }

    [Fact]
    public async Task RecordIncomeAsync_ThrowsEntityNotFound_WhenAccountDoesNotExist()
    {
        var request = MakeRequest(100m, Guid.NewGuid());

        await Assert.ThrowsAsync<EntityNotFoundException>(
            () => _sut.RecordIncomeAsync(request, Ct));
    }

    [Fact]
    public async Task RecordIncomeAsync_ThrowsValidation_WhenAccountIsExpenseType()
    {
        var request = MakeRequest(100m, ExpenseAccountId);

        await Assert.ThrowsAsync<ValidationException>(
            () => _sut.RecordIncomeAsync(request, Ct));
    }

    [Fact]
    public async Task RecordIncomeAsync_ThrowsValidation_WhenAccountIsSystemAccount()
    {
        var request = MakeRequest(100m, SystemIncomeAccountId);

        await Assert.ThrowsAsync<ValidationException>(
            () => _sut.RecordIncomeAsync(request, Ct));
    }

    [Fact]
    public async Task RecordIncomeAsync_ThrowsEntityNotFound_WhenDepositAccountDoesNotExist()
    {
        var request = MakeRequest(100m, depositAccountId: Guid.NewGuid());

        await Assert.ThrowsAsync<EntityNotFoundException>(
            () => _sut.RecordIncomeAsync(request, Ct));
    }

    [Fact]
    public async Task RecordIncomeAsync_ThrowsValidation_WhenDepositAccountIsNotABankAccount()
    {
        var request = MakeRequest(100m, depositAccountId: ExpenseAccountId);

        await Assert.ThrowsAsync<ValidationException>(
            () => _sut.RecordIncomeAsync(request, Ct));
    }

    // --- RecordIncomeAsync: GL posting ---

    [Fact]
    public async Task RecordIncomeAsync_PostsBalancedSet_DebitCashCreditIncome()
    {
        var request = MakeRequest(250m);

        await _sut.RecordIncomeAsync(request, Ct);

        await _glRepo.Received(1).AddBalancedSetAsync(
            Arg.Is<IReadOnlyList<Transaction>>(lines =>
                lines!.Count == 2
                && lines.Any(t => t.DebitAmount == 250m && t.AccountId == CashAccountId && t.GLAccount == "1100")
                && lines.Any(t => t.CreditAmount == 250m && t.AccountId == IncomeAccountId)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecordIncomeAsync_DebitsChosenDepositAccount_WhenDepositAccountProvided()
    {
        var request = MakeRequest(250m, depositAccountId: BankAccountId);

        await _sut.RecordIncomeAsync(request, Ct);

        await _glRepo.Received(1).AddBalancedSetAsync(
            Arg.Is<IReadOnlyList<Transaction>>(lines =>
                lines!.Any(t => t.DebitAmount == 250m && t.AccountId == BankAccountId && t.GLAccount == "1110")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecordIncomeAsync_CreatesIncomeJournalEntry_AndLinksLines()
    {
        var request = MakeRequest(100m);

        await _sut.RecordIncomeAsync(request, Ct);

        await _journalRepo.Received(1).AddAsync(
            Arg.Is<JournalEntry>(j => j!.Type == JournalEntryType.Income && j.Date == request.Date),
            Arg.Any<CancellationToken>());

        await _glRepo.Received(1).AddBalancedSetAsync(
            Arg.Is<IReadOnlyList<Transaction>>(lines => lines!.All(t => t.JournalEntryId != null)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecordIncomeAsync_GLSet_HasNoMemberOrFeeOrPaymentLinks()
    {
        var request = MakeRequest(100m);

        await _sut.RecordIncomeAsync(request, Ct);

        await _glRepo.Received(1).AddBalancedSetAsync(
            Arg.Is<IReadOnlyList<Transaction>>(lines =>
                lines!.All(t => t.MemberId == null && t.FeeId == null && t.PaymentId == null)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecordIncomeAsync_GLSet_UsesRequestDate()
    {
        var date = new DateTime(2026, 3, 15, 0, 0, 0, DateTimeKind.Utc);
        var request = new RecordIncomeRequest
        {
            Date = date, Amount = 100m, AccountId = IncomeAccountId
        };

        await _sut.RecordIncomeAsync(request, Ct);

        await _glRepo.Received(1).AddBalancedSetAsync(
            Arg.Is<IReadOnlyList<Transaction>>(lines => lines!.All(t => t.Date == date)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecordIncomeAsync_GLSet_UsesDescriptionWhenProvided()
    {
        var request = new RecordIncomeRequest
        {
            Date = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            Amount = 100m, AccountId = IncomeAccountId,
            Description = "Christmas Raffle"
        };

        await _sut.RecordIncomeAsync(request, Ct);

        await _glRepo.Received(1).AddBalancedSetAsync(
            Arg.Is<IReadOnlyList<Transaction>>(lines => lines!.All(t => t.Description == "Christmas Raffle")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecordIncomeAsync_GLSet_DefaultsDescriptionToAccount_WhenNotProvided()
    {
        var request = new RecordIncomeRequest
        {
            Date = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            Amount = 100m, AccountId = IncomeAccountId, Description = null
        };

        await _sut.RecordIncomeAsync(request, Ct);

        await _glRepo.Received(1).AddBalancedSetAsync(
            Arg.Is<IReadOnlyList<Transaction>>(lines => lines!.All(t => t.Description!.Contains("Raffle Income"))),
            Arg.Any<CancellationToken>());
    }

    // --- RecordIncomeAsync: GST ---

    [Fact]
    public async Task RecordIncomeAsync_NotRegistered_PostsTwoLines_WithNullTaxCode_EvenWhenTaxCodeRequested()
    {
        // Toggle-off byte-identical regression: unregistered orgs must always get the
        // pre-GST 2-line posting with TaxCode == null, regardless of what the caller asks for.
        _settingsRepo.GetAsync(Arg.Any<CancellationToken>()).Returns((Settings?)null);
        var request = MakeRequest(110m);
        request.TaxCode = TaxCode.Taxable;

        await _sut.RecordIncomeAsync(request, Ct);

        await _glRepo.Received(1).AddBalancedSetAsync(
            Arg.Is<IReadOnlyList<Transaction>>(lines =>
                lines!.Count == 2
                && lines.All(t => t.TaxCode == null)
                && lines.Any(t => t.DebitAmount == 110m)
                && lines.Any(t => t.CreditAmount == 110m)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecordIncomeAsync_RegisteredAndTaxable_PostsThreeLines_SplittingTaxToClearing()
    {
        _settingsRepo.GetAsync(Arg.Any<CancellationToken>())
            .Returns(MakeSettings(isTaxApplicable: true));
        var request = MakeRequest(110m);
        request.TaxCode = TaxCode.Taxable;

        await _sut.RecordIncomeAsync(request, Ct);

        await _glRepo.Received(1).AddBalancedSetAsync(
            Arg.Is<IReadOnlyList<Transaction>>(lines =>
                lines!.Count == 3
                && lines.Any(t => t.DebitAmount == 110m && t.AccountId == CashAccountId)
                && lines.Any(t => t.CreditAmount == 100m && t.AccountId == IncomeAccountId)
                && lines.Any(t => t.CreditAmount == 10m && t.AccountId == SystemAccounts.TaxCollectedId)
                && lines.All(t => t.TaxCode == TaxCode.Taxable)),
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(TaxCode.TaxExempt)]
    [InlineData(TaxCode.Excluded)]
    public async Task RecordIncomeAsync_RegisteredButNotTaxable_PostsTwoLines_NoTaxSplit(TaxCode code)
    {
        _settingsRepo.GetAsync(Arg.Any<CancellationToken>())
            .Returns(MakeSettings(isTaxApplicable: true));
        var request = MakeRequest(110m);
        request.TaxCode = code;

        await _sut.RecordIncomeAsync(request, Ct);

        await _glRepo.Received(1).AddBalancedSetAsync(
            Arg.Is<IReadOnlyList<Transaction>>(lines =>
                lines!.Count == 2
                && lines.Any(t => t.DebitAmount == 110m)
                && lines.Any(t => t.CreditAmount == 110m)
                && lines.All(t => t.TaxCode == code)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecordIncomeAsync_RegisteredNoTaxCodeProvided_DefaultsToTaxFree()
    {
        _settingsRepo.GetAsync(Arg.Any<CancellationToken>())
            .Returns(MakeSettings(isTaxApplicable: true));
        var request = MakeRequest(110m); // TaxCode left null

        await _sut.RecordIncomeAsync(request, Ct);

        await _glRepo.Received(1).AddBalancedSetAsync(
            Arg.Is<IReadOnlyList<Transaction>>(lines =>
                lines!.Count == 2 && lines.All(t => t.TaxCode == TaxCode.TaxExempt)),
            Arg.Any<CancellationToken>());
    }

    // --- RecordIncomeAsync: unit of work + audit ---

    [Fact]
    public async Task RecordIncomeAsync_RunsInsideUnitOfWork()
    {
        await _sut.RecordIncomeAsync(MakeRequest(100m), Ct);

        await _unitOfWork.Received(1).ExecuteInTransactionAsync(
            Arg.Any<Func<CancellationToken, Task>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecordIncomeAsync_WritesAuditEntry()
    {
        await _sut.RecordIncomeAsync(MakeRequest(100m), Ct);

        await _audit.Received(1).LogAsync(
            Arg.Any<string>(), Arg.Any<Guid>(), AuditAction.Create,
            Arg.Any<string?>(), Arg.Any<string?>(),
            ct: Arg.Any<CancellationToken>());
    }

    // --- Helpers ---

    private static RecordIncomeRequest MakeRequest(
        decimal amount, Guid? accountId = null, Guid? depositAccountId = null) =>
        new()
        {
            Date = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            Amount = amount,
            AccountId = accountId ?? IncomeAccountId,
            DepositAccountId = depositAccountId,
            Description = null
        };

    private static Account MakeAccount(
        Guid id, string name, AccountType type, string number,
        bool isSystem = false, bool isBank = false) => new()
    {
        Id = id, Name = name, Type = type,
        AccountNumber = number, IsSystem = isSystem, IsBankAccount = isBank, SortOrder = 0,
        CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
    };

    private static Settings MakeSettings(bool isTaxApplicable) => new()
    {
        Id = Guid.NewGuid(), OrganizationName = "Test Choir",
        AnnualFee = 50m, AttendanceFee = 10m,
        MembershipRenewalMonth = 1, MaxAgeRangeYears = 150,
        MinimumMemberAge = 0, SchemaVersion = "1.1.0",
        IsTaxApplicable = isTaxApplicable, TaxRate = isTaxApplicable ? 10m : null,
        CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
    };
}
