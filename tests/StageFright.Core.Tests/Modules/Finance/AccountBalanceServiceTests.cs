using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Modules.Finance;
using StageFright.Core.Tests.Fixtures;

namespace StageFright.Core.Tests.Modules.Finance;

/// <summary>
/// Unit tests for AccountBalanceService: GL-derived per-account balances for the
/// Chart of Accounts, with credit-normal sign flip and per-account failure isolation.
/// </summary>
public class AccountBalanceServiceTests : TestBase
{
    private readonly IAccountRepository _accountRepo = Substitute.For<IAccountRepository>();
    private readonly IGLRepository _glRepo = Substitute.For<IGLRepository>();

    private static readonly Guid BankAccountId = Guid.NewGuid();
    private static readonly Guid IncomeAccountId = Guid.NewGuid();
    private static readonly Guid LiabilityAccountId = Guid.NewGuid();
    private static readonly Guid ExpenseAccountId = Guid.NewGuid();
    private static readonly Guid ZeroActivityAccountId = Guid.NewGuid();
    private static readonly Guid FailingAccountId = Guid.NewGuid();
    private static readonly Guid ArchivedAccountId = Guid.NewGuid();

    private readonly AccountBalanceService _sut;

    public AccountBalanceServiceTests()
    {
        _sut = new AccountBalanceService(_accountRepo, _glRepo, NullLogger<AccountBalanceService>.Instance);
    }

    [Fact]
    public async Task GetActiveAccountBalancesAsync_ReturnsRawNetDebit_ForDebitNormalAsset()
    {
        _accountRepo.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Account> { MakeAccount(BankAccountId, "Operating Account", AccountType.Asset, "1110") });
        _glRepo.GetAccountBalanceAsync(BankAccountId, Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(150m);

        var result = await _sut.GetActiveAccountBalancesAsync(Ct);

        var row = Assert.Single(result);
        Assert.Equal(150m, row.Balance);
        Assert.False(row.HasError);
    }

    [Fact]
    public async Task GetActiveAccountBalancesAsync_ReturnsRawNetDebit_ForDebitNormalExpense()
    {
        _accountRepo.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Account> { MakeAccount(ExpenseAccountId, "Venue Hire", AccountType.Expense, "6000") });
        _glRepo.GetAccountBalanceAsync(ExpenseAccountId, Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(75m);

        var result = await _sut.GetActiveAccountBalancesAsync(Ct);

        var row = Assert.Single(result);
        Assert.Equal(75m, row.Balance);
    }

    [Fact]
    public async Task GetActiveAccountBalancesAsync_FlipsSign_ForCreditNormalIncome()
    {
        _accountRepo.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Account> { MakeAccount(IncomeAccountId, "Membership Fees", AccountType.Income, "4000") });
        // Net debit is negative (more credits than debits) for a healthy income account.
        _glRepo.GetAccountBalanceAsync(IncomeAccountId, Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(-200m);

        var result = await _sut.GetActiveAccountBalancesAsync(Ct);

        var row = Assert.Single(result);
        Assert.Equal(200m, row.Balance);
    }

    [Fact]
    public async Task GetActiveAccountBalancesAsync_FlipsSign_ForCreditNormalLiability()
    {
        _accountRepo.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Account> { MakeAccount(LiabilityAccountId, "GST Collected", AccountType.Liability, "2310") });
        _glRepo.GetAccountBalanceAsync(LiabilityAccountId, Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(-40m);

        var result = await _sut.GetActiveAccountBalancesAsync(Ct);

        var row = Assert.Single(result);
        Assert.Equal(40m, row.Balance);
    }

    [Fact]
    public async Task GetActiveAccountBalancesAsync_ReturnsZero_ForAccountWithNoActivity()
    {
        _accountRepo.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Account> { MakeAccount(ZeroActivityAccountId, "Brand New Account", AccountType.Income, "4001") });
        _glRepo.GetAccountBalanceAsync(ZeroActivityAccountId, Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(0m);

        var result = await _sut.GetActiveAccountBalancesAsync(Ct);

        var row = Assert.Single(result);
        Assert.Equal(0m, row.Balance);
        Assert.False(row.HasError);
    }

    [Fact]
    public async Task GetActiveAccountBalancesAsync_IsolatesFailure_ToTheOneAccount()
    {
        _accountRepo.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Account>
            {
                MakeAccount(FailingAccountId, "Corrupt Account", AccountType.Asset, "1120"),
                MakeAccount(BankAccountId, "Operating Account", AccountType.Asset, "1110")
            });
        _glRepo.GetAccountBalanceAsync(FailingAccountId, Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns<Task<decimal>>(_ => throw new InvalidOperationException("GL data error"));
        _glRepo.GetAccountBalanceAsync(BankAccountId, Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(100m);

        var result = await _sut.GetActiveAccountBalancesAsync(Ct);

        Assert.Equal(2, result.Count);

        var failingRow = Assert.Single(result, r => r.AccountId == FailingAccountId);
        Assert.True(failingRow.HasError);
        Assert.Null(failingRow.Balance);

        var healthyRow = Assert.Single(result, r => r.AccountId == BankAccountId);
        Assert.False(healthyRow.HasError);
        Assert.Equal(100m, healthyRow.Balance);
    }

    [Fact]
    public async Task GetActiveAccountBalancesAsync_OrdersByAccountNumber()
    {
        _accountRepo.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Account>
            {
                MakeAccount(IncomeAccountId, "Membership Fees", AccountType.Income, "4000"),
                MakeAccount(BankAccountId, "Operating Account", AccountType.Asset, "1110")
            });
        _glRepo.GetAccountBalanceAsync(Arg.Any<Guid>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(0m);

        var result = await _sut.GetActiveAccountBalancesAsync(Ct);

        Assert.Equal("1110", result[0].AccountNumber);
        Assert.Equal("4000", result[1].AccountNumber);
    }

    [Fact]
    public async Task GetArchivedAccountBalancesAsync_ReturnsBalance_ForArchivedAccount()
    {
        var archived = MakeAccount(ArchivedAccountId, "Old Fund", AccountType.Income, "4009");
        archived.IsDeleted = true;
        _accountRepo.GetArchivedAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Account> { archived });
        _glRepo.GetAccountBalanceAsync(ArchivedAccountId, Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(-25m);

        var result = await _sut.GetArchivedAccountBalancesAsync(Ct);

        var row = Assert.Single(result);
        Assert.Equal(25m, row.Balance);
        Assert.False(row.HasError);
    }

    // --- Helpers ---

    private static Account MakeAccount(Guid id, string name, AccountType type, string accountNumber) => new()
    {
        Id = id,
        Name = name,
        Type = type,
        AccountNumber = accountNumber,
        IsSystem = false,
        IsBankAccount = type == AccountType.Asset && accountNumber == "1110",
        CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
    };
}
