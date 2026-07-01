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
/// category validation, amount validation, and audit logging.
/// </summary>
public class IncomeEntryServiceTests : TestBase
{
    private readonly ICategoryRepository _categoryRepo = Substitute.For<ICategoryRepository>();
    private readonly IGLRepository _glRepo = Substitute.For<IGLRepository>();
    private readonly IAuditTrailService _audit = Substitute.For<IAuditTrailService>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    private static readonly Guid IncomeCategoryId = Guid.NewGuid();
    private static readonly Guid ExpenseCategoryId = Guid.NewGuid();
    private static readonly Guid SystemCategoryId = new("00000000-0000-0000-0000-000000000001");

    private readonly IncomeEntryService _sut;

    public IncomeEntryServiceTests()
    {
        _unitOfWork
            .ExecuteInTransactionAsync(Arg.Any<Func<CancellationToken, Task>>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.ArgAt<Func<CancellationToken, Task>>(0)(ci.ArgAt<CancellationToken>(1)));

        _categoryRepo.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Category>
            {
                MakeIncomeCategory(IncomeCategoryId, "Raffle Income", "1000"),
                MakeExpenseCategory(ExpenseCategoryId, "Hall Hire", "2000"),
                MakeSystemCategory(SystemCategoryId, "Cash", "0100")
            });

        _sut = new IncomeEntryService(_categoryRepo, _glRepo, _audit, _unitOfWork);
    }

    // --- GetIncomeCategoriesAsync ---

    [Fact]
    public async Task GetIncomeCategoriesAsync_ReturnsOnlyNonSystemIncomeCategories()
    {
        var result = await _sut.GetIncomeCategoriesAsync(Ct);

        Assert.Single(result);
        Assert.Equal(IncomeCategoryId, result[0].Id);
    }

    [Fact]
    public async Task GetIncomeCategoriesAsync_ExcludesExpenseCategories()
    {
        var result = await _sut.GetIncomeCategoriesAsync(Ct);

        Assert.DoesNotContain(result, c => c.Type == CategoryType.Expense);
    }

    [Fact]
    public async Task GetIncomeCategoriesAsync_ExcludesSystemCategories()
    {
        var result = await _sut.GetIncomeCategoriesAsync(Ct);

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
    public async Task RecordIncomeAsync_ThrowsEntityNotFound_WhenCategoryDoesNotExist()
    {
        var request = MakeRequest(100m, Guid.NewGuid());

        await Assert.ThrowsAsync<EntityNotFoundException>(
            () => _sut.RecordIncomeAsync(request, Ct));
    }

    [Fact]
    public async Task RecordIncomeAsync_ThrowsValidation_WhenCategoryIsExpenseType()
    {
        var request = MakeRequest(100m, ExpenseCategoryId);

        await Assert.ThrowsAsync<ValidationException>(
            () => _sut.RecordIncomeAsync(request, Ct));
    }

    [Fact]
    public async Task RecordIncomeAsync_ThrowsValidation_WhenCategoryIsSystemCategory()
    {
        var request = MakeRequest(100m, SystemCategoryId);

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
            Arg.Is<Transaction>(t => t.DebitAmount == 250m && t.GLAccount == "0100"),
            Arg.Is<Transaction>(t => t.CreditAmount == 250m && t.CategoryId == IncomeCategoryId),
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
            Date = date, Amount = 100m, CategoryId = IncomeCategoryId
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
            Amount = 100m, CategoryId = IncomeCategoryId,
            Description = "Christmas Raffle"
        };

        await _sut.RecordIncomeAsync(request, Ct);

        await _glRepo.Received(1).AddPairAsync(
            Arg.Is<Transaction>(t => t.Description == "Christmas Raffle"),
            Arg.Any<Transaction>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecordIncomeAsync_GLDebit_DefaultsDescriptionToCategory_WhenNotProvided()
    {
        var request = new RecordIncomeRequest
        {
            Date = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            Amount = 100m, CategoryId = IncomeCategoryId, Description = null
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

    private static RecordIncomeRequest MakeRequest(decimal amount, Guid? categoryId = null) =>
        new()
        {
            Date = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            Amount = amount,
            CategoryId = categoryId ?? IncomeCategoryId,
            Description = null
        };

    private static Category MakeIncomeCategory(Guid id, string name, string gl) => new()
    {
        Id = id, Name = name, Type = CategoryType.Income,
        GLAccount = gl, IsSystem = false, SortOrder = 0,
        CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
    };

    private static Category MakeExpenseCategory(Guid id, string name, string gl) => new()
    {
        Id = id, Name = name, Type = CategoryType.Expense,
        GLAccount = gl, IsSystem = false, SortOrder = 0,
        CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
    };

    private static Category MakeSystemCategory(Guid id, string name, string gl) => new()
    {
        Id = id, Name = name, Type = CategoryType.Income,
        GLAccount = gl, IsSystem = true, SortOrder = 0,
        CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
    };
}
