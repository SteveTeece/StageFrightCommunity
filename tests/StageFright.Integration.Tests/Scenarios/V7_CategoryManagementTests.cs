using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Exceptions;
using StageFright.Core.Modules.AuditTrail;
using StageFright.Core.Modules.Finance;
using StageFright.Core.Modules.Settings;
using StageFright.Data;
using StageFright.Data.Repositories;

namespace StageFright.Integration.Tests.Scenarios;

/// <summary>
/// Acceptance tests for V7: category management.
/// Verifies GL sequential assignment, archive blocking when referenced,
/// archive of unreferenced categories, restore, and reorder persistence.
/// Uses a real SQLite in-memory database with full EF migrations.
/// </summary>
public sealed class V7_CategoryManagementTests : IAsyncLifetime
{
    private StageFrightDbContext _db = null!;

    public async Task InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<StageFrightDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        _db = new StageFrightDbContext(options);
        await _db.Database.OpenConnectionAsync();
        await _db.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await _db.Database.CloseConnectionAsync();
        await _db.DisposeAsync();
    }

    // --- GL sequential assignment ---

    [Fact]
    public async Task CreateIncome_FirstCategory_Gets1000()
    {
        var svc = BuildCategoryService();

        var category = await svc.CreateAsync("Membership Fees", CategoryType.Income);

        Assert.Equal("1000", category.GLAccount);
    }

    [Fact]
    public async Task CreateIncome_SecondCategory_Gets1001()
    {
        var svc = BuildCategoryService();

        await svc.CreateAsync("Membership Fees", CategoryType.Income);
        var second = await svc.CreateAsync("Concert Tickets", CategoryType.Income);

        Assert.Equal("1001", second.GLAccount);
    }

    [Fact]
    public async Task CreateExpense_FirstCategory_Gets2000()
    {
        var svc = BuildCategoryService();

        var category = await svc.CreateAsync("Hall Rental", CategoryType.Expense);

        Assert.Equal("2000", category.GLAccount);
    }

    [Fact]
    public async Task CreateExpense_SecondCategory_Gets2001()
    {
        var svc = BuildCategoryService();

        await svc.CreateAsync("Hall Rental", CategoryType.Expense);
        var second = await svc.CreateAsync("Printing", CategoryType.Expense);

        Assert.Equal("2001", second.GLAccount);
    }

    [Fact]
    public async Task CreateIncome_AndExpense_Independent_Sequences()
    {
        var svc = BuildCategoryService();

        await svc.CreateAsync("Income A", CategoryType.Income);
        await svc.CreateAsync("Income B", CategoryType.Income);
        var expense = await svc.CreateAsync("Expense A", CategoryType.Expense);

        Assert.Equal("2000", expense.GLAccount);
    }

    // --- Archive blocked ---

    [Fact]
    public async Task Archive_CategoryReferencedByTransaction_ThrowsValidationException()
    {
        var svc = BuildCategoryService();

        var category = await svc.CreateAsync("Membership", CategoryType.Income);
        await AddTransaction(category.Id, category.GLAccount);

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            svc.ArchiveAsync(category.Id));

        Assert.Contains("referenced by one or more transactions", ex.Message);
    }

    [Fact]
    public async Task Archive_CategoryReferencedByTransaction_DoesNotSoftDelete()
    {
        var svc = BuildCategoryService();

        var category = await svc.CreateAsync("Membership", CategoryType.Income);
        await AddTransaction(category.Id, category.GLAccount);

        await Assert.ThrowsAsync<ValidationException>(() => svc.ArchiveAsync(category.Id));

        var inDb = await _db.Categories.FindAsync(category.Id);
        Assert.False(inDb!.IsDeleted);
    }

    [Fact]
    public async Task Archive_SystemCategory_ThrowsValidationException()
    {
        var svc = BuildCategoryService();
        var cashId = new Guid("00000000-0000-0000-0000-000000000001");

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            svc.ArchiveAsync(cashId));

        Assert.Contains("System categories cannot be archived", ex.Message);
    }

    // --- Archive unblocked ---

    [Fact]
    public async Task Archive_UnreferencedCategory_SoftDeletes()
    {
        var svc = BuildCategoryService();

        var category = await svc.CreateAsync("Old Category", CategoryType.Income);

        await svc.ArchiveAsync(category.Id);

        var all = await svc.GetAllAsync();
        Assert.DoesNotContain(all, c => c.Id == category.Id);

        var archived = await svc.GetArchivedAsync();
        Assert.Contains(archived, c => c.Id == category.Id);
    }

    [Fact]
    public async Task Archive_CreatesAuditEntry()
    {
        var svc = BuildCategoryService();
        var category = await svc.CreateAsync("Old Category", CategoryType.Income);

        await svc.ArchiveAsync(category.Id);

        var audit = await _db.AuditTrailEntries.FirstOrDefaultAsync(a =>
            a.EntityType == nameof(Category) &&
            a.EntityId == category.Id &&
            a.Action == AuditAction.Delete);

        Assert.NotNull(audit);
    }

    // --- Restore ---

    [Fact]
    public async Task Restore_ArchivedCategory_MakesActiveAgain()
    {
        var svc = BuildCategoryService();

        var category = await svc.CreateAsync("Dormant Category", CategoryType.Income);
        await svc.ArchiveAsync(category.Id);

        await svc.RestoreAsync(category.Id);

        var all = await svc.GetAllAsync();
        Assert.Contains(all, c => c.Id == category.Id);
    }

    // --- Reorder ---

    [Fact]
    public async Task Reorder_PersistsSortOrder()
    {
        var svc = BuildCategoryService();

        var first = await svc.CreateAsync("Category A", CategoryType.Income);
        var second = await svc.CreateAsync("Category B", CategoryType.Income);

        await svc.ReorderAsync(new[] { (first.Id, 5), (second.Id, 3) });

        var updatedFirst = await _db.Categories.FindAsync(first.Id);
        var updatedSecond = await _db.Categories.FindAsync(second.Id);

        Assert.Equal(5, updatedFirst!.SortOrder);
        Assert.Equal(3, updatedSecond!.SortOrder);
    }

    // --- Helpers ---

    private CategoryService BuildCategoryService()
    {
        var categoryRepo = new CategoryRepository(_db);
        var auditRepo = new AuditTrailRepository(_db);
        var auditSvc = new AuditTrailService(auditRepo, NullLogger<AuditTrailService>.Instance);
        var glAssignment = new GLAccountAssignmentService(categoryRepo);
        return new CategoryService(categoryRepo, glAssignment, auditSvc);
    }

    private async Task AddTransaction(Guid categoryId, string glAccount)
    {
        var now = DateTime.UtcNow;
        _db.Transactions.Add(new Transaction
        {
            Id = Guid.NewGuid(),
            Date = now,
            CategoryId = categoryId,
            DebitAmount = 50m,
            CreditAmount = 0m,
            GLAccount = glAccount,
            Description = "Test transaction",
            CreatedAt = now
        });
        await _db.SaveChangesAsync();
    }
}
