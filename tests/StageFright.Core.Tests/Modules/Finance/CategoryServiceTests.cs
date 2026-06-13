using NSubstitute;
using NSubstitute.ExceptionExtensions;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Exceptions;
using StageFright.Core.Modules.Finance;
using StageFright.Core.Modules.Settings;
using StageFright.Core.Tests.Fixtures;

namespace StageFright.Core.Tests.Modules.Finance;

/// <summary>
/// Unit tests for CategoryService — creation with GL assignment, archive blocking (referenced or system),
/// restore, reorder, and audit entries.
/// </summary>
public class CategoryServiceTests : TestBase
{
    private readonly ICategoryRepository _repo = Substitute.For<ICategoryRepository>();
    private readonly IAuditTrailService _audit = Substitute.For<IAuditTrailService>();
    private readonly GLAccountAssignmentService _glAssignment;
    private readonly CategoryService _sut;

    private static readonly Guid IncomeCategoryId = Guid.NewGuid();
    private static readonly Guid ExpenseCategoryId = Guid.NewGuid();
    private static readonly Guid SystemCategoryId = new("00000000-0000-0000-0000-000000000001");

    public CategoryServiceTests()
    {
        var categoryRepoForGl = Substitute.For<ICategoryRepository>();
        categoryRepoForGl.GetNextGLAccountAsync(CategoryType.Income, Arg.Any<CancellationToken>())
            .Returns("1000");
        categoryRepoForGl.GetNextGLAccountAsync(CategoryType.Expense, Arg.Any<CancellationToken>())
            .Returns("2000");
        _glAssignment = new GLAccountAssignmentService(categoryRepoForGl);

        _repo.AddAsync(Arg.Any<Category>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.ArgAt<Category>(0));

        _sut = new CategoryService(_repo, _glAssignment, _audit);
    }

    // --- CreateAsync ---

    [Fact]
    public async Task CreateAsync_Income_AssignsNextGLAccount()
    {
        var result = await _sut.CreateAsync("Membership Fees", CategoryType.Income, Ct);

        Assert.Equal("1000", result.GLAccount);
        Assert.Equal(CategoryType.Income, result.Type);
    }

    [Fact]
    public async Task CreateAsync_Expense_AssignsExpenseGLAccount()
    {
        var result = await _sut.CreateAsync("Hall Rental", CategoryType.Expense, Ct);

        Assert.Equal("2000", result.GLAccount);
        Assert.Equal(CategoryType.Expense, result.Type);
    }

    [Fact]
    public async Task CreateAsync_SetsIsSystemFalse()
    {
        var result = await _sut.CreateAsync("Donations", CategoryType.Income, Ct);

        Assert.False(result.IsSystem);
    }

    [Fact]
    public async Task CreateAsync_WritesAuditEntry()
    {
        await _sut.CreateAsync("Raffles", CategoryType.Income, Ct);

        await _audit.Received(1).LogAsync(
            Arg.Any<string>(), Arg.Any<Guid>(), Arg.Is(AuditAction.Create),
            Arg.Any<string?>(), Arg.Any<string?>(),
            Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_EmptyName_ThrowsValidationException()
    {
        await Assert.ThrowsAsync<ValidationException>(() =>
            _sut.CreateAsync("", CategoryType.Income, Ct));
    }

    [Fact]
    public async Task CreateAsync_WhitespaceName_ThrowsValidationException()
    {
        await Assert.ThrowsAsync<ValidationException>(() =>
            _sut.CreateAsync("   ", CategoryType.Income, Ct));
    }

    // --- ArchiveAsync ---

    [Fact]
    public async Task ArchiveAsync_ReferencedByTransaction_ThrowsValidationException()
    {
        var category = MakeCategory(IncomeCategoryId, isSystem: false);
        _repo.GetByIdAsync(IncomeCategoryId, Arg.Any<CancellationToken>()).Returns(category);
        _repo.IsReferencedByTransactionsAsync(IncomeCategoryId, Arg.Any<CancellationToken>()).Returns(true);

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            _sut.ArchiveAsync(IncomeCategoryId, Ct));

        Assert.Contains("referenced by one or more transactions", ex.Message);
    }

    [Fact]
    public async Task ArchiveAsync_UnreferencedCategory_Succeeds()
    {
        var category = MakeCategory(IncomeCategoryId, isSystem: false);
        _repo.GetByIdAsync(IncomeCategoryId, Arg.Any<CancellationToken>()).Returns(category);
        _repo.IsReferencedByTransactionsAsync(IncomeCategoryId, Arg.Any<CancellationToken>()).Returns(false);

        await _sut.ArchiveAsync(IncomeCategoryId, Ct);

        await _repo.Received(1).ArchiveAsync(IncomeCategoryId, "system", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ArchiveAsync_SystemCategory_ThrowsValidationException()
    {
        var sysCategory = MakeCategory(SystemCategoryId, isSystem: true);
        _repo.GetByIdAsync(SystemCategoryId, Arg.Any<CancellationToken>()).Returns(sysCategory);

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            _sut.ArchiveAsync(SystemCategoryId, Ct));

        Assert.Contains("System categories cannot be archived", ex.Message);
    }

    [Fact]
    public async Task ArchiveAsync_WritesAuditEntry_OnSuccess()
    {
        var category = MakeCategory(IncomeCategoryId, isSystem: false);
        _repo.GetByIdAsync(IncomeCategoryId, Arg.Any<CancellationToken>()).Returns(category);
        _repo.IsReferencedByTransactionsAsync(IncomeCategoryId, Arg.Any<CancellationToken>()).Returns(false);

        await _sut.ArchiveAsync(IncomeCategoryId, Ct);

        await _audit.Received(1).LogAsync(
            nameof(Category), IncomeCategoryId, AuditAction.Delete,
            Arg.Any<string?>(), null, "system", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ArchiveAsync_NotFound_ThrowsEntityNotFoundException()
    {
        _repo.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((Category?)null);

        await Assert.ThrowsAsync<EntityNotFoundException>(() =>
            _sut.ArchiveAsync(Guid.NewGuid(), Ct));
    }

    // --- RestoreAsync ---

    [Fact]
    public async Task RestoreAsync_CallsRepository_RestoreAsync()
    {
        await _sut.RestoreAsync(IncomeCategoryId, Ct);

        await _repo.Received(1).RestoreAsync(IncomeCategoryId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RestoreAsync_WritesAuditEntry()
    {
        await _sut.RestoreAsync(IncomeCategoryId, Ct);

        await _audit.Received(1).LogAsync(
            nameof(Category), IncomeCategoryId, AuditAction.Restore,
            null, null, "system", Arg.Any<CancellationToken>());
    }

    // --- ReorderAsync ---

    [Fact]
    public async Task ReorderAsync_DelegatesToRepository()
    {
        var order = new[] { (IncomeCategoryId, 0), (ExpenseCategoryId, 1) };

        await _sut.ReorderAsync(order, Ct);

        await _repo.Received(1).ReorderAsync(order, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReorderAsync_EmptyList_DoesNotThrow()
    {
        var exception = await Record.ExceptionAsync(() =>
            _sut.ReorderAsync(Array.Empty<(Guid, int)>(), Ct));

        Assert.Null(exception);
    }

    // --- Helpers ---

    private static Category MakeCategory(Guid id, bool isSystem) => new()
    {
        Id = id,
        Name = isSystem ? "Cash" : "Test Category",
        Type = CategoryType.Income,
        GLAccount = isSystem ? "0100" : "1000",
        IsSystem = isSystem,
        SortOrder = 0,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };
}
