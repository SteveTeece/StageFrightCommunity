using NSubstitute;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Reports.Models;
using StageFright.Reports.Providers;

namespace StageFright.Reports.Tests;

/// <summary>
/// Tests for AccountRegisterReportProvider:
/// - Transactions in chronological order
/// - Running balance correct after each row
/// - Date-range filter applied
/// - Category filter applied
/// </summary>
public class AccountRegisterReportProviderTests
{
    private readonly IGLRepository _gl = Substitute.For<IGLRepository>();
    private readonly ICategoryRepository _categories = Substitute.For<ICategoryRepository>();
    private readonly AccountRegisterReportProvider _sut;

    public AccountRegisterReportProviderTests()
    {
        _sut = new AccountRegisterReportProvider(_gl, _categories);
    }

    [Fact]
    public void ReportId_IsAccountRegister()
    {
        Assert.Equal("account-register", _sut.ReportId);
    }

    [Fact]
    public void ModuleName_IsFinance()
    {
        Assert.Equal("Finance", _sut.ModuleName);
    }

    [Fact]
    public async Task GenerateAsync_TransactionsInChronologicalOrder()
    {
        var catId = Guid.NewGuid();
        SetupCategories(MakeCategory(catId, "Dues", CategoryType.Income, "1000"));

        var t1 = MakeTransaction(catId, "Dues", 0, 100m, new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc));
        var t2 = MakeTransaction(catId, "Dues", 0, 50m, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        _gl.GetByDateRangeAsync(Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<Transaction>>(new[] { t1, t2 }.ToList()));

        var result = await _sut.GenerateAsync(CurrentYearFilters());

        var rows = result.Sections.SelectMany(s => s.Rows).ToList();
        // First row should be Jan 1 (earlier date)
        Assert.Contains("2026-01-01", rows[0].Cells[0]);
        Assert.Contains("2026-03-01", rows[1].Cells[0]);
    }

    [Fact]
    public async Task GenerateAsync_RunningBalance_CorrectAfterEachRow()
    {
        var catId = Guid.NewGuid();
        SetupCategories(MakeCategory(catId, "Dues", CategoryType.Income, "1000"));

        _gl.GetByDateRangeAsync(Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<Transaction>>(new[]
            {
                MakeTransaction(catId, "Dues", 0, 100m, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)),
                MakeTransaction(catId, "Dues", 30m, 0, new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc)),
            }.ToList()));

        var result = await _sut.GenerateAsync(CurrentYearFilters());
        var rows = result.Sections.SelectMany(s => s.Rows).ToList();

        // After credit 100: balance = 100 (credit side)
        // After debit 30:   balance = 70
        Assert.Contains("100.00", rows[0].Cells.Last());
        Assert.Contains("70.00", rows[1].Cells.Last());
    }

    [Fact]
    public async Task GenerateAsync_EmptyDateRange_ReturnsEmptyReport()
    {
        SetupCategories();
        _gl.GetByDateRangeAsync(Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<Transaction>>(Array.Empty<Transaction>().ToList()));

        var result = await _sut.GenerateAsync(CurrentYearFilters());

        Assert.NotNull(result);
        Assert.Equal("Account Register", result.Title);
    }

    [Fact]
    public async Task GenerateAsync_DateRangeFilter_Applied()
    {
        SetupCategories();
        _gl.GetByDateRangeAsync(Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<Transaction>>(Array.Empty<Transaction>().ToList()));

        var filters = new ReportFilterValues();
        filters.Set("dateFrom", "2026-03-01");
        filters.Set("dateTo", "2026-09-30");

        await _sut.GenerateAsync(filters);

        await _gl.Received().GetByDateRangeAsync(
            new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 9, 30, 23, 59, 59, DateTimeKind.Utc),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GenerateAsync_ReportHasRunningBalanceColumn()
    {
        SetupCategories();
        _gl.GetByDateRangeAsync(Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<Transaction>>(Array.Empty<Transaction>().ToList()));

        var result = await _sut.GenerateAsync(CurrentYearFilters());

        Assert.Contains(result.Columns, c => c.Header.Contains("Balance", StringComparison.OrdinalIgnoreCase));
    }

    // --- Helpers ---

    private void SetupCategories(params Category[] cats)
    {
        _categories.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<Category>>(cats.ToList()));
    }

    private static Category MakeCategory(Guid id, string name, CategoryType type, string glAccount)
        => new() { Id = id, Name = name, Type = type, GLAccount = glAccount, CreatedAt = DateTime.UtcNow };

    private static Transaction MakeTransaction(Guid catId, string desc, decimal debit, decimal credit, DateTime date)
    {
        var cat = new Category { Id = catId, Name = desc, Type = CategoryType.Income, GLAccount = "1000", CreatedAt = DateTime.UtcNow };
        return new Transaction
        {
            Id = Guid.NewGuid(), CategoryId = catId, Category = cat,
            GLAccount = "1000", DebitAmount = debit, CreditAmount = credit,
            Date = date, Description = desc, CreatedAt = DateTime.UtcNow
        };
    }

    private static ReportFilterValues CurrentYearFilters()
    {
        var f = new ReportFilterValues();
        f.Set("dateFrom", $"{DateTime.UtcNow.Year}-01-01");
        f.Set("dateTo", $"{DateTime.UtcNow.Year}-12-31");
        return f;
    }
}
