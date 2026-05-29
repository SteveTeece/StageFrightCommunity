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
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace StageFright.Integration.Tests;

/// <summary>
/// Integration tests for Account Register report provider.
/// Verifies chronological ordering, running balance accuracy, and filtering.
/// </summary>
public class AccountRegisterReportTests : IAsyncLifetime
{
	private readonly StageFrightContext _context;
	private AccountRegisterReportProvider _provider;
	private readonly Mock<ILogger<AccountRegisterReportProvider>> _mockLogger;

	public AccountRegisterReportTests()
	{
		var options = new DbContextOptionsBuilder<StageFrightContext>()
			.UseInMemoryDatabase($"AccountRegisterTest_{Guid.NewGuid()}")
			.Options;

		_context = new StageFrightContext(options);
		_mockLogger = new Mock<ILogger<AccountRegisterReportProvider>>();
	}

	public async Task InitializeAsync()
	{
		await _context.Database.EnsureCreatedAsync();
		_provider = new AccountRegisterReportProvider(
			new TransactionRepository(_context),
			_mockLogger.Object);
	}

	public async Task DisposeAsync()
	{
		await _context.Database.EnsureDeletedAsync();
		_context.Dispose();
	}

	[Fact]
	public async Task GenerateAsync_WithMultipleTransactions_ReturnsChronologicalOrder()
	{
		// Arrange
		var tx1 = new Transaction
		{
			Id = Guid.NewGuid(),
			Date = new DateTime(2026, 1, 15),
			Category = "Membership",
			CreditAmount = 100m,
			Description = "Jan Fees",
			CreatedAt = DateTime.UtcNow
		};

		var tx2 = new Transaction
		{
			Id = Guid.NewGuid(),
			Date = new DateTime(2026, 1, 20),
			Category = "Membership",
			DebitAmount = 50m,
			Description = "Refund",
			CreatedAt = DateTime.UtcNow.AddSeconds(1)
		};

		var tx3 = new Transaction
		{
			Id = Guid.NewGuid(),
			Date = new DateTime(2026, 2, 10),
			Category = "Membership",
			CreditAmount = 150m,
			Description = "Feb Fees",
			CreatedAt = DateTime.UtcNow.AddSeconds(2)
		};

		_context.Transactions.Add(tx1);
		_context.Transactions.Add(tx2);
		_context.Transactions.Add(tx3);
		await _context.SaveChangesAsync();

		// Act
		var report = await _provider.GenerateAsync();

		// Assert
		report.ReportTitle.Should().Contain("Account Register");
		report.ColumnHeaders.Should().Equal("Date", "GL Account", "Description", "Debit", "Credit", "Running Balance");
		report.Rows.Should().HaveCount(3);

		// Verify chronological order
		report.Rows[0][0].Should().Be("2026-01-15");
		report.Rows[1][0].Should().Be("2026-01-20");
		report.Rows[2][0].Should().Be("2026-02-10");
	}

	[Fact]
	public async Task GenerateAsync_CalculatesRunningBalanceCorrectly()
	{
		// Arrange
		var tx1 = new Transaction
		{
			Id = Guid.NewGuid(),
			Date = new DateTime(2026, 1, 1),
			Category = "Account",
			DebitAmount = 100m,
			CreatedAt = DateTime.UtcNow
		};

		var tx2 = new Transaction
		{
			Id = Guid.NewGuid(),
			Date = new DateTime(2026, 1, 2),
			Category = "Account",
			CreditAmount = 30m,
			CreatedAt = DateTime.UtcNow.AddSeconds(1)
		};

		var tx3 = new Transaction
		{
			Id = Guid.NewGuid(),
			Date = new DateTime(2026, 1, 3),
			Category = "Account",
			DebitAmount = 20m,
			CreatedAt = DateTime.UtcNow.AddSeconds(2)
		};

		_context.Transactions.Add(tx1);
		_context.Transactions.Add(tx2);
		_context.Transactions.Add(tx3);
		await _context.SaveChangesAsync();

		// Act
		var report = await _provider.GenerateAsync();

		// Assert - Running balance should be: 100, then 70 (100-30), then 90 (70+20)
		report.Rows.Should().HaveCount(3);
		report.Rows[0][5].Should().Contain("100");  // First balance
		report.Rows[1][5].Should().Contain("70");   // 100 - 30
		report.Rows[2][5].Should().Contain("90");   // 70 + 20
	}

	[Fact]
	public async Task GenerateAsync_WithCategoryFilter_ReturnsOnlyFilteredCategory()
	{
		// Arrange
		var tx1 = new Transaction
		{
			Id = Guid.NewGuid(),
			Date = DateTime.Now,
			Category = "Membership",
			CreditAmount = 100m,
			CreatedAt = DateTime.UtcNow
		};

		var tx2 = new Transaction
		{
			Id = Guid.NewGuid(),
			Date = DateTime.Now,
			Category = "Donations",
			CreditAmount = 50m,
			CreatedAt = DateTime.UtcNow.AddSeconds(1)
		};

		_context.Transactions.Add(tx1);
		_context.Transactions.Add(tx2);
		await _context.SaveChangesAsync();

		var filter = new ReportFilter { CategoryFilter = "Membership" };

		// Act
		var report = await _provider.GenerateAsync(filter);

		// Assert
		report.Rows.Should().HaveCount(1);
		report.Rows[0][1].Should().Be("Membership");
		report.Summaries["Transaction Count"].Should().Be("1");
	}
}
