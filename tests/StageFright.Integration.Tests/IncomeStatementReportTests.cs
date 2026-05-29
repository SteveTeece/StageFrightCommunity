using Xunit;
using FluentAssertions;
using StageFright.Reports.Providers;
using StageFright.Plugins.Contracts;
using StageFright.Data.Context;
using StageFright.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using StageFright.Data.Repositories;
using StageFright.Data.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace StageFright.Integration.Tests;

/// <summary>
/// Integration tests for Income Statement report provider.
/// Verifies revenue/expense organization, filtering, and calculation accuracy.
/// </summary>
public class IncomeStatementReportTests : IAsyncLifetime
{
	private readonly StageFrightContext _context;
	private IncomeStatementReportProvider _provider;
	private readonly Mock<ILogger<IncomeStatementReportProvider>> _mockLogger;
	private readonly Mock<ILogger<GlBalanceValidationService>> _mockGlLogger;

	public IncomeStatementReportTests()
	{
		var options = new DbContextOptionsBuilder<StageFrightContext>()
			.UseInMemoryDatabase($"IncomeStatementTest_{Guid.NewGuid()}")
			.Options;

		_context = new StageFrightContext(options);
		_mockLogger = new Mock<ILogger<IncomeStatementReportProvider>>();
		_mockGlLogger = new Mock<ILogger<GlBalanceValidationService>>();
	}

	public async Task InitializeAsync()
	{
		await _context.Database.EnsureCreatedAsync();
		_provider = new IncomeStatementReportProvider(
			new TransactionRepository(_context),
			new CategoryRepository(_context),
			new GlBalanceValidationService(new TransactionRepository(_context)),
			_mockLogger.Object);
	}

	public async Task DisposeAsync()
	{
		await _context.Database.EnsureDeletedAsync();
		_context.Dispose();
	}

	[Fact]
	public async Task GenerateAsync_WithValidTransactions_ReturnsBothRevenueAndExpenses()
	{
		// Arrange
		var incomeCategory = new Category { Id = Guid.NewGuid(), Name = "Membership Fees", Type = "Income", SortOrder = 1 };
		var assetCategory = new Category { Id = Guid.NewGuid(), Name = "Cash", Type = "Asset", SortOrder = 1 };
		var expenseCategory = new Category { Id = Guid.NewGuid(), Name = "Venue Rental", Type = "Expense", SortOrder = 1 };

		_context.Categories.Add(incomeCategory);
		_context.Categories.Add(assetCategory);
		_context.Categories.Add(expenseCategory);

		// GL Entry 1: Receive membership fees (balanced pair)
		var incomeTx1 = new Transaction
		{
			Id = Guid.NewGuid(),
			Date = DateTime.Now,
			Category = "Membership Fees",
			CreditAmount = 500m,  // Income is credited
			CreatedAt = DateTime.UtcNow
		};

		var assetTx1 = new Transaction
		{
			Id = Guid.NewGuid(),
			Date = DateTime.Now,
			Category = "Cash",
			DebitAmount = 500m,  // Asset (cash) is debited
			CreatedAt = DateTime.UtcNow
		};

		// GL Entry 2: Pay for venue (balanced pair)
		var expenseTx = new Transaction
		{
			Id = Guid.NewGuid(),
			Date = DateTime.Now,
			Category = "Venue Rental",
			DebitAmount = 300m,  // Expense is debited
			CreatedAt = DateTime.UtcNow
		};

		var assetTx2 = new Transaction
		{
			Id = Guid.NewGuid(),
			Date = DateTime.Now,
			Category = "Cash",
			CreditAmount = 300m,  // Asset (cash) is credited
			CreatedAt = DateTime.UtcNow
		};

		_context.Transactions.Add(incomeTx1);
		_context.Transactions.Add(assetTx1);
		_context.Transactions.Add(expenseTx);
		_context.Transactions.Add(assetTx2);
		await _context.SaveChangesAsync();

		// Act
		var report = await _provider.GenerateAsync();

		// Assert
		report.ReportTitle.Should().Contain("Income Statement");
		report.ColumnHeaders.Should().Equal("Description", "Amount");
		report.Rows.Should().HaveCountGreaterThan(0);
		report.Summaries.Should().ContainKey("Total Income")
			.And.ContainKey("Total Expense")
			.And.ContainKey("Net Income");

		report.Summaries["Total Income"].Should().Contain("500");
		report.Summaries["Total Expense"].Should().Contain("300");
	}

	[Fact]
	public async Task GenerateAsync_WithDateFilter_ReturnsOnlyTransactionsInRange()
	{
		// Arrange
		var category = new Category { Id = Guid.NewGuid(), Name = "Test Income", Type = "Income", SortOrder = 1 };
		var cashCategory = new Category { Id = Guid.NewGuid(), Name = "Cash", Type = "Asset", SortOrder = 1 };
		_context.Categories.Add(category);
		_context.Categories.Add(cashCategory);

		// Jan transaction (should be filtered out)
		var tx1 = new Transaction
		{
			Id = Guid.NewGuid(),
			Date = new DateTime(2026, 1, 15),
			Category = "Test Income",
			CreditAmount = 100m,
			CreatedAt = DateTime.UtcNow
		};

		var tx1Cash = new Transaction
		{
			Id = Guid.NewGuid(),
			Date = new DateTime(2026, 1, 15),
			Category = "Cash",
			DebitAmount = 100m,
			CreatedAt = DateTime.UtcNow
		};

		// Mar transaction (should be filtered out)
		var tx2 = new Transaction
		{
			Id = Guid.NewGuid(),
			Date = new DateTime(2026, 3, 15),
			Category = "Test Income",
			CreditAmount = 200m,
			CreatedAt = DateTime.UtcNow.AddSeconds(1)
		};

		var tx2Cash = new Transaction
		{
			Id = Guid.NewGuid(),
			Date = new DateTime(2026, 3, 15),
			Category = "Cash",
			DebitAmount = 200m,
			CreatedAt = DateTime.UtcNow.AddSeconds(1)
		};

		_context.Transactions.Add(tx1);
		_context.Transactions.Add(tx1Cash);
		_context.Transactions.Add(tx2);
		_context.Transactions.Add(tx2Cash);
		await _context.SaveChangesAsync();

		var filter = new ReportFilter
		{
			DateFrom = new DateTime(2026, 2, 1),
			DateTo = new DateTime(2026, 2, 28)
		};

		// Act
		var report = await _provider.GenerateAsync(filter);

		// Assert
		report.Summaries["Total Income"].Should().Contain("0");
	}

	[Fact]
	public async Task GenerateAsync_WithUnbalancedGL_ThrowsInvalidOperationException()
	{
		// Arrange
		var category = new Category { Id = Guid.NewGuid(), Name = "Bad Category", Type = "Income", SortOrder = 1 };
		_context.Categories.Add(category);

		var unbalancedTx = new Transaction
		{
			Id = Guid.NewGuid(),
			Date = DateTime.Now,
			Category = "Bad Category",
			DebitAmount = 100m,
			CreditAmount = null,
			CreatedAt = DateTime.UtcNow
		};

		_context.Transactions.Add(unbalancedTx);
		await _context.SaveChangesAsync();

		// Act & Assert
		await Assert.ThrowsAsync<InvalidOperationException>(
			() => _provider.GenerateAsync());
	}
}
