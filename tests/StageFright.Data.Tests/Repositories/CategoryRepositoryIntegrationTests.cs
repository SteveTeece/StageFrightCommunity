using Microsoft.EntityFrameworkCore;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Exceptions;
using StageFright.Data.Repositories;
using StageFright.Data.Tests.Infrastructure;

namespace StageFright.Data.Tests.Repositories;

/// <summary>
/// Integration tests for CategoryRepository — IsReferencedByTransactionsAsync,
/// GetNextGLAccountAsync sequential ordering, archive guard, soft-delete filter.
/// </summary>
public class CategoryRepositoryIntegrationTests : IDisposable
{
    private readonly DbContextFactory _factory = new();

    // System category GUIDs (seeded by StageFrightDbContext)
    private static readonly Guid CashCategoryId = new("00000000-0000-0000-0000-000000000001");
    private static readonly Guid MemberReceivableCategoryId = new("00000000-0000-0000-0000-000000000002");

    // --- IsReferencedByTransactionsAsync ---

    [Fact]
    public async Task IsReferencedByTransactions_ReturnsTrue_WhenTransactionExists()
    {
        using var db = _factory.CreateContext();
        var repo = new CategoryRepository(db);

        var category = await AddIncomeCategory(db, "Membership Fees");
        await AddTransaction(db, category.Id);

        var result = await repo.IsReferencedByTransactionsAsync(category.Id);

        Assert.True(result);
    }

    [Fact]
    public async Task IsReferencedByTransactions_ReturnsFalse_WhenNoTransactions()
    {
        using var db = _factory.CreateContext();
        var repo = new CategoryRepository(db);

        var category = await AddIncomeCategory(db, "Donations");

        var result = await repo.IsReferencedByTransactionsAsync(category.Id);

        Assert.False(result);
    }

    // --- GetNextGLAccountAsync ---

    [Fact]
    public async Task GetNextGLAccount_FirstIncome_Returns1000()
    {
        using var db = _factory.CreateContext();
        var repo = new CategoryRepository(db);

        var result = await repo.GetNextGLAccountAsync(CategoryType.Income);

        Assert.Equal("1000", result);
    }

    [Fact]
    public async Task GetNextGLAccount_AfterOneIncome_Returns1001()
    {
        using var db = _factory.CreateContext();
        var repo = new CategoryRepository(db);

        await AddIncomeCategory(db, "First Income");

        var result = await repo.GetNextGLAccountAsync(CategoryType.Income);

        Assert.Equal("1001", result);
    }

    [Fact]
    public async Task GetNextGLAccount_FirstExpense_Returns2000()
    {
        using var db = _factory.CreateContext();
        var repo = new CategoryRepository(db);

        var result = await repo.GetNextGLAccountAsync(CategoryType.Expense);

        Assert.Equal("2000", result);
    }

    [Fact]
    public async Task GetNextGLAccount_AfterOneExpense_Returns2001()
    {
        using var db = _factory.CreateContext();
        var repo = new CategoryRepository(db);

        await AddExpenseCategory(db, "First Expense");

        var result = await repo.GetNextGLAccountAsync(CategoryType.Expense);

        Assert.Equal("2001", result);
    }

    [Fact]
    public async Task GetNextGLAccount_SequentialOrdering_ByCreatedAt()
    {
        using var db = _factory.CreateContext();
        var repo = new CategoryRepository(db);

        // Add two income categories with different creation times
        await AddIncomeCategory(db, "First");
        await AddIncomeCategory(db, "Second");

        // Third should be at index 2 → "1002"
        var result = await repo.GetNextGLAccountAsync(CategoryType.Income);

        Assert.Equal("1002", result);
    }

    [Fact]
    public async Task GetNextGLAccount_IncomeAndExpense_Independent()
    {
        using var db = _factory.CreateContext();
        var repo = new CategoryRepository(db);

        await AddIncomeCategory(db, "Income 1");
        await AddIncomeCategory(db, "Income 2");

        // Expense counter is independent — first expense is still 2000
        var result = await repo.GetNextGLAccountAsync(CategoryType.Expense);

        Assert.Equal("2000", result);
    }

    [Fact]
    public async Task GetNextGLAccount_SystemCategories_ExcludedFromCount()
    {
        using var db = _factory.CreateContext();
        var repo = new CategoryRepository(db);

        // System categories (IsSystem=true) are seeded; they must not affect user category numbering
        var result = await repo.GetNextGLAccountAsync(CategoryType.Income);

        Assert.Equal("1000", result);
    }

    // --- Archive guard ---

    [Fact]
    public async Task ArchiveAsync_SystemCategory_ThrowsValidationException()
    {
        using var db = _factory.CreateContext();
        var repo = new CategoryRepository(db);

        await Assert.ThrowsAsync<ValidationException>(() =>
            repo.ArchiveAsync(CashCategoryId, "system"));
    }

    [Fact]
    public async Task ArchiveAsync_ReferencedCategory_ThrowsValidationException()
    {
        using var db = _factory.CreateContext();
        var repo = new CategoryRepository(db);

        var category = await AddIncomeCategory(db, "Referenced");
        await AddTransaction(db, category.Id);

        await Assert.ThrowsAsync<ValidationException>(() =>
            repo.ArchiveAsync(category.Id, "system"));
    }

    [Fact]
    public async Task ArchiveAsync_UnreferencedCategory_SoftDeletes()
    {
        using var db = _factory.CreateContext();
        var repo = new CategoryRepository(db);

        var category = await AddIncomeCategory(db, "To Archive");

        await repo.ArchiveAsync(category.Id, "system");

        var allActive = await repo.GetAllAsync();
        Assert.DoesNotContain(allActive, c => c.Id == category.Id);

        var archived = await repo.GetArchivedAsync();
        Assert.Contains(archived, c => c.Id == category.Id);
    }

    // --- Soft-delete filter ---

    [Fact]
    public async Task GetAllAsync_ExcludesSoftDeleted()
    {
        using var db = _factory.CreateContext();
        var repo = new CategoryRepository(db);

        var c1 = await AddIncomeCategory(db, "Active");
        var c2 = await AddIncomeCategory(db, "ToArchive");
        await repo.ArchiveAsync(c2.Id, "system");

        var all = await repo.GetAllAsync();
        Assert.Contains(all, c => c.Id == c1.Id);
        Assert.DoesNotContain(all, c => c.Id == c2.Id);
    }

    // --- ReorderAsync ---

    [Fact]
    public async Task ReorderAsync_UpdatesSortOrder()
    {
        using var db = _factory.CreateContext();
        var repo = new CategoryRepository(db);

        var c1 = await AddIncomeCategory(db, "Cat A");
        var c2 = await AddIncomeCategory(db, "Cat B");

        await repo.ReorderAsync(new[] { (c1.Id, 5), (c2.Id, 3) });

        using var db2 = _factory.CreateContext();
        var updated1 = await db2.Categories.FindAsync(c1.Id);
        var updated2 = await db2.Categories.FindAsync(c2.Id);

        Assert.Equal(5, updated1!.SortOrder);
        Assert.Equal(3, updated2!.SortOrder);
    }

    // --- Helpers ---

    private static async Task<Category> AddIncomeCategory(StageFright.Data.StageFrightDbContext db, string name)
    {
        var now = DateTime.UtcNow;
        var category = new Category
        {
            Id = Guid.NewGuid(),
            Name = name,
            Type = CategoryType.Income,
            GLAccount = "TEMP",
            IsSystem = false,
            SortOrder = 0,
            CreatedAt = now,
            UpdatedAt = now
        };
        db.Categories.Add(category);
        await db.SaveChangesAsync();
        return category;
    }

    private static async Task<Category> AddExpenseCategory(StageFright.Data.StageFrightDbContext db, string name)
    {
        var now = DateTime.UtcNow;
        var category = new Category
        {
            Id = Guid.NewGuid(),
            Name = name,
            Type = CategoryType.Expense,
            GLAccount = "TEMP",
            IsSystem = false,
            SortOrder = 0,
            CreatedAt = now,
            UpdatedAt = now
        };
        db.Categories.Add(category);
        await db.SaveChangesAsync();
        return category;
    }

    private static async Task AddTransaction(StageFright.Data.StageFrightDbContext db, Guid categoryId)
    {
        var now = DateTime.UtcNow;
        db.Transactions.Add(new Transaction
        {
            Id = Guid.NewGuid(),
            Date = now,
            CategoryId = categoryId,
            DebitAmount = 50m,
            CreditAmount = 0m,
            GLAccount = "0101",
            Description = "Test transaction",
            CreatedAt = now
        });
        await db.SaveChangesAsync();
    }

    public void Dispose() => _factory.Dispose();
}
