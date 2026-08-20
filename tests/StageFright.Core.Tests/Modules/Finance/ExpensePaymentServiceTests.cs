using NSubstitute;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Exceptions;
using StageFright.Core.Modules.Finance;
using StageFright.Core.Tests.Fixtures;

namespace StageFright.Core.Tests.Modules.Finance;

/// <summary>
/// Unit tests for ExpensePaymentService — expense recording with a journal entry +
/// balanced GL set (DR Expense / CR Bank), account validation, amount validation,
/// and audit logging.
/// </summary>
public class ExpensePaymentServiceTests : TestBase
{
    private readonly IAccountRepository _accountRepo = Substitute.For<IAccountRepository>();
    private readonly IGLRepository _glRepo = Substitute.For<IGLRepository>();
    private readonly IJournalEntryRepository _journalRepo = Substitute.For<IJournalEntryRepository>();
    private readonly ISettingsRepository _settingsRepo = Substitute.For<ISettingsRepository>();
    private readonly IAuditTrailService _audit = Substitute.For<IAuditTrailService>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    private static readonly Guid ExpenseAccountId = Guid.NewGuid();
    private static readonly Guid IncomeAccountId = Guid.NewGuid();
    private static readonly Guid BankAccountId = Guid.NewGuid();
    private static readonly Guid NonBankAssetAccountId = Guid.NewGuid();
    private static readonly Guid SystemExpenseAccountId = SystemAccounts.BadDebtId;

    private readonly ExpensePaymentService _sut;

    public ExpensePaymentServiceTests()
    {
        _unitOfWork
            .ExecuteInTransactionAsync(Arg.Any<Func<CancellationToken, Task>>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.ArgAt<Func<CancellationToken, Task>>(0)(ci.ArgAt<CancellationToken>(1)));

        _journalRepo.AddAsync(Arg.Any<JournalEntry>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.ArgAt<JournalEntry>(0));

        _accountRepo.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Account>
            {
                MakeAccount(ExpenseAccountId, "Hall Hire", AccountType.Expense, "6000"),
                MakeAccount(IncomeAccountId, "Raffle Income", AccountType.Income, "4000"),
                MakeAccount(SystemExpenseAccountId, "Bad Debt Expense", AccountType.Expense, "6999", isSystem: true),
                MakeAccount(BankAccountId, "Operating Account", AccountType.Asset, "1110", isBank: true),
                MakeAccount(NonBankAssetAccountId, "Equipment", AccountType.Asset, "1300")
            });

        _sut = new ExpensePaymentService(_accountRepo, _glRepo, _journalRepo, _settingsRepo, _audit, _unitOfWork);
    }

    // --- GetExpenseAccountsAsync ---

    [Fact]
    public async Task Should_ReturnOnlyNonSystemExpenseAccounts_When_GettingExpenseAccounts()
    {
        var result = await _sut.GetExpenseAccountsAsync(Ct);

        Assert.Single(result);
        Assert.Equal(ExpenseAccountId, result[0].Id);
    }

    // --- Validation ---

    [Fact]
    public async Task Should_ThrowValidation_When_AmountIsZero()
    {
        await Assert.ThrowsAsync<ValidationException>(
            () => _sut.RecordExpenseAsync(MakeRequest(0m), Ct));
    }

    [Fact]
    public async Task Should_ThrowValidation_When_AmountIsNegative()
    {
        await Assert.ThrowsAsync<ValidationException>(
            () => _sut.RecordExpenseAsync(MakeRequest(-10m), Ct));
    }

    [Fact]
    public async Task Should_ThrowEntityNotFound_When_BankAccountDoesNotExist()
    {
        await Assert.ThrowsAsync<EntityNotFoundException>(
            () => _sut.RecordExpenseAsync(MakeRequest(100m, bankAccountId: Guid.NewGuid()), Ct));
    }

    [Fact]
    public async Task Should_ThrowValidation_When_PaymentAccountIsNotABankAccount()
    {
        await Assert.ThrowsAsync<ValidationException>(
            () => _sut.RecordExpenseAsync(MakeRequest(100m, bankAccountId: NonBankAssetAccountId), Ct));
    }

    [Fact]
    public async Task Should_ThrowEntityNotFound_When_ExpenseAccountDoesNotExist()
    {
        await Assert.ThrowsAsync<EntityNotFoundException>(
            () => _sut.RecordExpenseAsync(MakeRequest(100m, expenseAccountId: Guid.NewGuid()), Ct));
    }

    [Fact]
    public async Task Should_ThrowValidation_When_ExpenseAccountIsNotExpenseType()
    {
        await Assert.ThrowsAsync<ValidationException>(
            () => _sut.RecordExpenseAsync(MakeRequest(100m, expenseAccountId: IncomeAccountId), Ct));
    }

    [Fact]
    public async Task Should_ThrowValidation_When_ExpenseAccountIsSystemAccount()
    {
        await Assert.ThrowsAsync<ValidationException>(
            () => _sut.RecordExpenseAsync(MakeRequest(100m, expenseAccountId: SystemExpenseAccountId), Ct));
    }

    // --- Posting ---

    [Fact]
    public async Task Should_PostDebitExpenseCreditBank_When_ExpenseRecorded()
    {
        await _sut.RecordExpenseAsync(MakeRequest(80m), Ct);

        await _glRepo.Received(1).AddBalancedSetAsync(
            Arg.Is<IReadOnlyList<Transaction>>(lines =>
                lines.Count == 2
                && lines.Any(t => t.DebitAmount == 80m && t.AccountId == ExpenseAccountId && t.GLAccount == "6000")
                && lines.Any(t => t.CreditAmount == 80m && t.AccountId == BankAccountId && t.GLAccount == "1110")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_CreateExpensePaymentJournalEntry_When_ExpenseRecorded()
    {
        var request = MakeRequest(80m);

        await _sut.RecordExpenseAsync(request, Ct);

        await _journalRepo.Received(1).AddAsync(
            Arg.Is<JournalEntry>(j => j.Type == JournalEntryType.ExpensePayment && j.Date == request.Date),
            Arg.Any<CancellationToken>());

        await _glRepo.Received(1).AddBalancedSetAsync(
            Arg.Is<IReadOnlyList<Transaction>>(lines => lines.All(t => t.JournalEntryId != null)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_IncludePayeeInDescription_When_PayeeProvided()
    {
        var request = MakeRequest(80m);
        request.Payee = "Scout Hall Trust";

        await _sut.RecordExpenseAsync(request, Ct);

        await _glRepo.Received(1).AddBalancedSetAsync(
            Arg.Is<IReadOnlyList<Transaction>>(lines =>
                lines.All(t => t.Description!.Contains("Scout Hall Trust"))),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_DefaultDescriptionToExpenseAccount_When_NoDescriptionProvided()
    {
        await _sut.RecordExpenseAsync(MakeRequest(80m), Ct);

        await _glRepo.Received(1).AddBalancedSetAsync(
            Arg.Is<IReadOnlyList<Transaction>>(lines =>
                lines.All(t => t.Description!.Contains("Hall Hire"))),
            Arg.Any<CancellationToken>());
    }

    // --- GST ---

    [Fact]
    public async Task Should_PostTwoLinesWithNullTaxCode_When_NotRegistered_EvenWhenTaxCodeRequested()
    {
        // Toggle-off byte-identical regression.
        _settingsRepo.GetAsync(Arg.Any<CancellationToken>()).Returns((Settings?)null);
        var request = MakeRequest(110m);
        request.TaxCode = TaxCode.Taxable;

        await _sut.RecordExpenseAsync(request, Ct);

        await _glRepo.Received(1).AddBalancedSetAsync(
            Arg.Is<IReadOnlyList<Transaction>>(lines =>
                lines.Count == 2
                && lines.All(t => t.TaxCode == null)
                && lines.Any(t => t.DebitAmount == 110m)
                && lines.Any(t => t.CreditAmount == 110m)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_PostThreeLinesSplittingTaxToClearing_When_RegisteredAndTaxable()
    {
        _settingsRepo.GetAsync(Arg.Any<CancellationToken>())
            .Returns(MakeSettings(isTaxApplicable: true));
        var request = MakeRequest(110m);
        request.TaxCode = TaxCode.Taxable;

        await _sut.RecordExpenseAsync(request, Ct);

        await _glRepo.Received(1).AddBalancedSetAsync(
            Arg.Is<IReadOnlyList<Transaction>>(lines =>
                lines.Count == 3
                && lines.Any(t => t.DebitAmount == 100m && t.AccountId == ExpenseAccountId)
                && lines.Any(t => t.DebitAmount == 10m && t.AccountId == SystemAccounts.TaxPaidId)
                && lines.Any(t => t.CreditAmount == 110m && t.AccountId == BankAccountId)
                && lines.All(t => t.TaxCode == TaxCode.Taxable)),
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(TaxCode.TaxExempt)]
    [InlineData(TaxCode.Excluded)]
    public async Task Should_PostTwoLines_When_RegisteredButNotTaxable(TaxCode code)
    {
        _settingsRepo.GetAsync(Arg.Any<CancellationToken>())
            .Returns(MakeSettings(isTaxApplicable: true));
        var request = MakeRequest(110m);
        request.TaxCode = code;

        await _sut.RecordExpenseAsync(request, Ct);

        await _glRepo.Received(1).AddBalancedSetAsync(
            Arg.Is<IReadOnlyList<Transaction>>(lines =>
                lines.Count == 2
                && lines.Any(t => t.DebitAmount == 110m)
                && lines.Any(t => t.CreditAmount == 110m)
                && lines.All(t => t.TaxCode == code)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_DefaultToTaxFree_When_RegisteredAndNoTaxCodeProvided()
    {
        _settingsRepo.GetAsync(Arg.Any<CancellationToken>())
            .Returns(MakeSettings(isTaxApplicable: true));
        var request = MakeRequest(110m); // TaxCode left null

        await _sut.RecordExpenseAsync(request, Ct);

        await _glRepo.Received(1).AddBalancedSetAsync(
            Arg.Is<IReadOnlyList<Transaction>>(lines =>
                lines.Count == 2 && lines.All(t => t.TaxCode == TaxCode.TaxExempt)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_RunInsideUnitOfWork_When_ExpenseRecorded()
    {
        await _sut.RecordExpenseAsync(MakeRequest(80m), Ct);

        await _unitOfWork.Received(1).ExecuteInTransactionAsync(
            Arg.Any<Func<CancellationToken, Task>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_WriteAuditEntry_When_ExpenseRecorded()
    {
        await _sut.RecordExpenseAsync(MakeRequest(80m), Ct);

        await _audit.Received(1).LogAsync(
            Arg.Any<string>(), Arg.Any<Guid>(), AuditAction.Create,
            Arg.Any<string?>(), Arg.Any<string?>(),
            ct: Arg.Any<CancellationToken>());
    }

    // --- Helpers ---

    private static RecordExpenseRequest MakeRequest(
        decimal amount, Guid? bankAccountId = null, Guid? expenseAccountId = null) =>
        new()
        {
            Date = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc),
            Amount = amount,
            BankAccountId = bankAccountId ?? BankAccountId,
            ExpenseAccountId = expenseAccountId ?? ExpenseAccountId
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
