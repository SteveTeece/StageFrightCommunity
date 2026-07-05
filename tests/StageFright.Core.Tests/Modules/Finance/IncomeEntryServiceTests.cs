using NSubstitute;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Exceptions;
using StageFright.Core.Modules.Finance;
using StageFright.Core.Tests.Fixtures;

namespace StageFright.Core.Tests.Modules.Finance;

/// <summary>
/// Unit tests for IncomeEntryService — non-member income recording with GL pair creation,
/// account validation, amount validation, and audit logging.
/// </summary>
public class IncomeEntryServiceTests : TestBase
{
    private readonly IAccountRepository _accountRepo = Substitute.For<IAccountRepository>();
    private readonly IGLRepository _glRepo = Substitute.For<IGLRepository>();
    private readonly IAuditTrailService _audit = Substitute.For<IAuditTrailService>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    private static readonly Guid IncomeAccountId = Guid.NewGuid();
    private static readonly Guid ExpenseAccountId = Guid.NewGuid();
    private static readonly Guid SystemAccountId = new("00000000-0000-0000-0000-000000000001");

    private readonly IncomeEntryService _sut;

    public IncomeEntryServiceTests()
    {
        _unitOfWork
            .ExecuteInTransactionAsync(Arg.Any<Func<CancellationToken, Task>>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.ArgAt<Func<CancellationToken, Task>>(0)(ci.ArgAt<CancellationToken>(1)));

        _accountRepo.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Account>
            {
                MakeIncomeAccount(IncomeAccountId, "Raffle Income", "4000"),
                MakeExpenseAccount(ExpenseAccountId, "Hall Hire", "6000"),
                MakeSystemAccount(SystemAccountId, "Cash", "1100")
            });

        _sut = new IncomeEntryService(_accountRepo, _glRepo, _audit, _unitOfWork);
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
        var request = MakeRequest(100m, SystemAccountId);

        await Assert.ThrowsAsync<ValidationException>(
            () => _sut.RecordIncomeAsync(request, Ct));
    }

    // --- RecordIncomeAsync: GL pair ---

    [Fact]
    public async Task RecordIncomeAsync_CreatesGLPair_DebitCashCreditIncome()
    {
        var request = MakeRequest(250m);

        await _sut.RecordIncomeAsync(request, Ct);

        await _glRepo.Received(1).AddPairAsync(
            Arg.Is<Transaction>(t => t.DebitAmount == 250m && t.GLAccount == "1100"),
            Arg.Is<Transaction>(t => t.CreditAmount == 250m && t.AccountId == IncomeAccountId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecordIncomeAsync_GLPair_HasNoMemberOrFeeOrPaymentLinks()
    {
        var request = MakeRequest(100m);

        await _sut.RecordIncomeAsync(request, Ct);

        await _glRepo.Received(1).AddPairAsync(
            Arg.Is<Transaction>(t => t.MemberId == null && t.FeeId == null && t.PaymentId == null),
            Arg.Is<Transaction>(t => t.MemberId == null && t.FeeId == null && t.PaymentId == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecordIncomeAsync_GLPair_UsesRequestDate()
    {
        var date = new DateTime(2026, 3, 15, 0, 0, 0, DateTimeKind.Utc);
        var request = new RecordIncomeRequest
        {
            Date = date, Amount = 100m, AccountId = IncomeAccountId
        };

        await _sut.RecordIncomeAsync(request, Ct);

        await _glRepo.Received(1).AddPairAsync(
            Arg.Is<Transaction>(t => t.Date == date),
            Arg.Is<Transaction>(t => t.Date == date),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecordIncomeAsync_GLDebit_UsesDescriptionWhenProvided()
    {
        var request = new RecordIncomeRequest
        {
            Date = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            Amount = 100m, AccountId = IncomeAccountId,
            Description = "Christmas Raffle"
        };

        await _sut.RecordIncomeAsync(request, Ct);

        await _glRepo.Received(1).AddPairAsync(
            Arg.Is<Transaction>(t => t.Description == "Christmas Raffle"),
            Arg.Any<Transaction>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecordIncomeAsync_GLDebit_DefaultsDescriptionToAccount_WhenNotProvided()
    {
        var request = new RecordIncomeRequest
        {
            Date = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            Amount = 100m, AccountId = IncomeAccountId, Description = null
        };

        await _sut.RecordIncomeAsync(request, Ct);

        await _glRepo.Received(1).AddPairAsync(
            Arg.Is<Transaction>(t => t.Description!.Contains("Raffle Income")),
            Arg.Any<Transaction>(),
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

    private static RecordIncomeRequest MakeRequest(decimal amount, Guid? accountId = null) =>
        new()
        {
            Date = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            Amount = amount,
            AccountId = accountId ?? IncomeAccountId,
            Description = null
        };

    private static Account MakeIncomeAccount(Guid id, string name, string gl) => new()
    {
        Id = id, Name = name, Type = AccountType.Income,
        AccountNumber = gl, IsSystem = false, SortOrder = 0,
        CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
    };

    private static Account MakeExpenseAccount(Guid id, string name, string gl) => new()
    {
        Id = id, Name = name, Type = AccountType.Expense,
        AccountNumber = gl, IsSystem = false, SortOrder = 0,
        CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
    };

    private static Account MakeSystemAccount(Guid id, string name, string gl) => new()
    {
        Id = id, Name = name, Type = AccountType.Income,
        AccountNumber = gl, IsSystem = true, SortOrder = 0,
        CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
    };
}
