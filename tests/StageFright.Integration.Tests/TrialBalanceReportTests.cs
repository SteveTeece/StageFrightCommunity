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
using System.Threading.Tasks;

namespace StageFright.Integration.Tests;

/// <summary>
/// Integration tests for Trial Balance report provider.
/// Verifies GL balance validation, account organization, and error messaging for imbalanced GL.
/// </summary>
public class TrialBalanceReportTests : IAsyncLifetime
{
	private readonly StageFrightContext _context;
	private TrialBalanceReportProvider _provider;
	private readonly Mock<ILogger<TrialBalanceReportProvider>> _mockLogger;

	public TrialBalanceReportTests()
	{
		var options = new DbContextOptionsBuilder<StageFrightContext>()
			.UseInMemoryDatabase($"TrialBalanceTest_{Guid.NewGuid()}")
			.Options;

		_context = new StageFrightContext(options);
		_mockLogger = new Mock<ILogger<TrialBalanceReportProvider>>();
	}

	public async Task InitializeAsync()
	{
		await _context.Database.EnsureCreatedAsync();
		_provider = new TrialBalanceReportProvider(
			new TransactionRepository(_context),
			new GlBalanceValidationService(new TransactionRepository(_context)),
			_mockLogger.Object);
	}

	public async Task DisposeAsync()
	{
		await _context.Database.EnsureDeletedAsync();
		_context.Dispose();
	}

	[Fact]
	public async Task GenerateAsync_WithBalancedGL_ReturnsReportWithEqualTotals()
	{
		// Arrange - Create balanced pair (debit = credit)
		var assetTx = new Transaction
		{
			Id = Guid.NewGuid(),
			Date = DateTime.Now,
			Category = "0100",  // Asset account
			DebitAmount = 500m,
			CreatedAt = DateTime.UtcNow
		};

		var incomeTx = new Transaction
		{
			Id = Guid.NewGuid(),
			Date = DateTime.Now,
			Category = "1000",  // Income account
			CreditAmount = 500m,
			CreatedAt = DateTime.UtcNow
		};

		_context.Transactions.Add(assetTx);
		_context.Transactions.Add(incomeTx);
		await _context.SaveChangesAsync();

		// Act
		var report = await _provider.GenerateAsync();

		// Assert
		report.ReportTitle.Should().Contain("Trial Balance");
		report.ColumnHeaders.Should().Equal("GL Account", "Debits", "Credits");
		report.Rows.Should().HaveCountGreaterThan(0);
		report.Summaries.Should().ContainKey("Balance Status");
		report.Summaries["Balance Status"].Should().Contain("BALANCED");
	}

	[Fact]
	public async Task GenerateAsync_WithUnbalancedGL_ThrowsWithDetailedErrorMessage()
	{
		// Arrange - Create unbalanced transaction (only debit, no credit pair)
		var unbalancedTx = new Transaction
		{
			Id = Guid.NewGuid(),
			Date = DateTime.Now,
			Category = "0100",
			DebitAmount = 500m,
			CreditAmount = null,
			CreatedAt = DateTime.UtcNow
		};

		_context.Transactions.Add(unbalancedTx);
		await _context.SaveChangesAsync();

		// Act & Assert
		var ex = await Assert.ThrowsAsync<InvalidOperationException>(
			() => _provider.GenerateAsync());

		ex.Message.Should()
			.Contain("GL Balance Verification Failed")
			.And.Contain("Total Debits")
			.And.Contain("Total Credits")
			.And.Contain("Please review and correct GL entries");
	}

	[Fact]
	public async Task GenerateAsync_WithMultipleAccounts_OrganizesByAccountType()
	{
		// Arrange - Create balanced GL transactions
		var assetTx = new Transaction
		{
			Id = Guid.NewGuid(),
			Date = DateTime.Now,
			Category = "0100",  // Asset
			DebitAmount = 1000m,
			CreatedAt = DateTime.UtcNow
		};

		var incomeTx = new Transaction
		{
			Id = Guid.NewGuid(),
			Date = DateTime.Now,
			Category = "1000",  // Income
			CreditAmount = 1000m,  // Offset the asset debit
			CreatedAt = DateTime.UtcNow.AddSeconds(1)
		};

		var expenseTx = new Transaction
		{
			Id = Guid.NewGuid(),
			Date = DateTime.Now,
			Category = "2000",  // Expense
			DebitAmount = 500m,
			CreatedAt = DateTime.UtcNow.AddSeconds(2)
		};

		var assetTx2 = new Transaction
		{
			Id = Guid.NewGuid(),
			Date = DateTime.Now,
			Category = "0100",  // Asset
			CreditAmount = 500m,  // Offset the expense debit
			CreatedAt = DateTime.UtcNow.AddSeconds(3)
		};

		_context.Transactions.Add(assetTx);
		_context.Transactions.Add(incomeTx);
		_context.Transactions.Add(expenseTx);
		_context.Transactions.Add(assetTx2);
		await _context.SaveChangesAsync();

		// Act
		var report = await _provider.GenerateAsync();

		// Assert
		var reportRows = string.Join("\n", report.Rows.Select(r => string.Join("|", r)));
		reportRows.Should().Contain("ASSETS")
			.And.Contain("INCOME")
			.And.Contain("EXPENSES");
	}
}
