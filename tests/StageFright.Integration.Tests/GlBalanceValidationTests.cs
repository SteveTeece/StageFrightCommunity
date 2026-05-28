using Microsoft.EntityFrameworkCore;
using StageFright.Core.Entities;
using StageFright.Core.Services;
using StageFright.Data.Context;
using StageFright.Data.Repositories;
using StageFright.Data.Services;
using Xunit;
using FluentAssertions;

namespace StageFright.Integration.Tests;

/// <summary>
/// Critical integration tests for GL balance validation failure scenarios.
/// Verifies that report generation fails with clear error messages when GL is out of balance.
/// </summary>
public class GlBalanceValidationTests
{
	private DbContextOptions<StageFrightContext> CreateInMemoryOptions()
	{
		return new DbContextOptionsBuilder<StageFrightContext>()
			.UseInMemoryDatabase(Guid.NewGuid().ToString())
			.EnableSensitiveDataLogging()
			.ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
			.Options;
	}

	private StageFrightContext CreateContext()
	{
		return new StageFrightContext(CreateInMemoryOptions());
	}

	[Fact]
	public async Task ValidateGLBalance_FailsWhenImbalanced()
	{
		// Arrange
		using var context = CreateContext();
		var transactionRepo = new TransactionRepository(context);
		var balanceService = new GlBalanceValidationService(transactionRepo);

		// Create imbalanced transactions manually (normally prevented by GlTransactionService)
		var debitOnly = new Transaction
		{
			Id = Guid.NewGuid(),
			Date = DateTime.UtcNow,
			Category = "Test",
			DebitAmount = 500m,
			CreditAmount = null,
			CreatedAt = DateTime.UtcNow,
			ModifiedAt = DateTime.UtcNow
		};

		await transactionRepo.CreateAsync(debitOnly);

		// Act
		var isBalanced = await balanceService.ValidateGLBalanceAsync();

		// Assert
		isBalanced.Should().BeFalse();
	}

	[Fact]
	public async Task GetGLBalanceErrorMessage_ReturnsFormattedMessage()
	{
		// Arrange
		using var context = CreateContext();
		var transactionRepo = new TransactionRepository(context);
		var balanceService = new GlBalanceValidationService(transactionRepo);

		// Create imbalanced GL
		var debit = new Transaction
		{
			Id = Guid.NewGuid(),
			Date = DateTime.UtcNow,
			Category = "Test",
			DebitAmount = 500m,
			CreditAmount = null,
			CreatedAt = DateTime.UtcNow,
			ModifiedAt = DateTime.UtcNow
		};

		var credit = new Transaction
		{
			Id = Guid.NewGuid(),
			Date = DateTime.UtcNow,
			Category = "Test",
			DebitAmount = null,
			CreditAmount = 300m,
			CreatedAt = DateTime.UtcNow,
			ModifiedAt = DateTime.UtcNow
		};

		await transactionRepo.CreateAsync(debit);
		await transactionRepo.CreateAsync(credit);

		// Act
		var errorMessage = await balanceService.GetGLBalanceErrorMessageAsync();

		// Assert
		errorMessage.Should().Contain("GL Balance Verification Failed");
		errorMessage.Should().Contain("Total Debits");
		errorMessage.Should().Contain("Total Credits");
		errorMessage.Should().Contain("$500.00");
		errorMessage.Should().Contain("$300.00");
		errorMessage.Should().Contain("$200.00"); // Difference
	}

	[Fact]
	public async Task GetGLBalanceDetails_CalculatesCorrectTotals()
	{
		// Arrange
		using var context = CreateContext();
		var transactionRepo = new TransactionRepository(context);
		var balanceService = new GlBalanceValidationService(transactionRepo);

		// Create specific imbalance: debits exceed credits
		var debits = new[]
		{
			new Transaction
			{
				Id = Guid.NewGuid(),
				Date = DateTime.UtcNow,
				Category = "Income",
				DebitAmount = 250m,
				CreditAmount = null,
				CreatedAt = DateTime.UtcNow,
				ModifiedAt = DateTime.UtcNow
			},
			new Transaction
			{
				Id = Guid.NewGuid(),
				Date = DateTime.UtcNow,
				Category = "Expense",
				DebitAmount = 350m,
				CreditAmount = null,
				CreatedAt = DateTime.UtcNow,
				ModifiedAt = DateTime.UtcNow
			}
		};

		var credits = new[]
		{
			new Transaction
			{
				Id = Guid.NewGuid(),
				Date = DateTime.UtcNow,
				Category = "Test",
				DebitAmount = null,
				CreditAmount = 400m,
				CreatedAt = DateTime.UtcNow,
				ModifiedAt = DateTime.UtcNow
			}
		};

		foreach (var t in debits)
			await transactionRepo.CreateAsync(t);
		foreach (var t in credits)
			await transactionRepo.CreateAsync(t);

		// Act
		var (totalDebits, totalCredits, isBalanced, difference) = await balanceService.GetGLBalanceDetailsAsync();

		// Assert
		totalDebits.Should().Be(600m); // 250 + 350
		totalCredits.Should().Be(400m);
		isBalanced.Should().BeFalse();
		difference.Should().Be(200m); // |600 - 400|
	}

	[Fact]
	public async Task ReportGeneration_FailsWithClearMessageOnImbalance()
	{
		// Arrange
		using var context = CreateContext();
		var transactionRepo = new TransactionRepository(context);
		var balanceService = new GlBalanceValidationService(transactionRepo);

		// Create imbalanced GL state
		var unbalancedDebit = new Transaction
		{
			Id = Guid.NewGuid(),
			Date = DateTime.UtcNow,
			Category = "Income",
			DebitAmount = 1000m,
			CreditAmount = null,
			CreatedAt = DateTime.UtcNow,
			ModifiedAt = DateTime.UtcNow
		};

		await transactionRepo.CreateAsync(unbalancedDebit);

		// Simulate report generation that checks GL balance
		// Act
		var isBalanced = await balanceService.ValidateGLBalanceAsync();

		// Assert - Report should fail
		isBalanced.Should().BeFalse();

		// Get user-facing error message
		var errorMessage = await balanceService.GetGLBalanceErrorMessageAsync();
		errorMessage.Should().Contain("GL Balance Verification Failed");
		errorMessage.Should().Contain("review GL entries");
	}

	[Fact]
	public async Task GLBalance_AllowsSmallRoundingErrors()
	{
		// Arrange
		// GL should be balanced if difference is within 0.01 (penny precision)

		using var context = CreateContext();
		var transactionRepo = new TransactionRepository(context);
		var balanceService = new GlBalanceValidationService(transactionRepo);

		// Create nearly-balanced GL (off by 0.01)
		var debit = new Transaction
		{
			Id = Guid.NewGuid(),
			Date = DateTime.UtcNow,
			Category = "Test",
			DebitAmount = 100.01m,
			CreditAmount = null,
			CreatedAt = DateTime.UtcNow,
			ModifiedAt = DateTime.UtcNow
		};

		var credit = new Transaction
		{
			Id = Guid.NewGuid(),
			Date = DateTime.UtcNow,
			Category = "Test",
			DebitAmount = null,
			CreditAmount = 100m,
			CreatedAt = DateTime.UtcNow,
			ModifiedAt = DateTime.UtcNow
		};

		await transactionRepo.CreateAsync(debit);
		await transactionRepo.CreateAsync(credit);

		// Act
		var isBalanced = await balanceService.ValidateGLBalanceAsync();

		// Assert - Should be considered balanced (within 0.01 tolerance)
		isBalanced.Should().BeTrue();
	}

	[Fact]
	public async Task TrialBalanceReport_FailsWhenGLOutOfBalance()
	{
		// Arrange
		// Test that Trial Balance report generation would fail with proper error

		using var context = CreateContext();
		var transactionRepo = new TransactionRepository(context);
		var balanceService = new GlBalanceValidationService(transactionRepo);

		// Create GL imbalance
		var debits = Enumerable.Range(0, 5)
			.Select(i => new Transaction
			{
				Id = Guid.NewGuid(),
				Date = DateTime.UtcNow.AddDays(-i),
				Category = $"Category{i}",
				DebitAmount = 100m + i,
				CreditAmount = null,
				CreatedAt = DateTime.UtcNow,
				ModifiedAt = DateTime.UtcNow
			})
			.ToList();

		var credits = Enumerable.Range(0, 3)
			.Select(i => new Transaction
			{
				Id = Guid.NewGuid(),
				Date = DateTime.UtcNow.AddDays(-i),
				Category = $"Category{i}",
				DebitAmount = null,
				CreditAmount = 150m + i,
				CreatedAt = DateTime.UtcNow,
				ModifiedAt = DateTime.UtcNow
			})
			.ToList();

		foreach (var t in debits.Concat(credits))
			await transactionRepo.CreateAsync(t);

		// Act
		var isBalanced = await balanceService.ValidateGLBalanceAsync();
		var errorMsg = await balanceService.GetGLBalanceErrorMessageAsync();

		// Assert
		isBalanced.Should().BeFalse();
		errorMsg.Should().StartWith("GL Balance Verification Failed:");
		errorMsg.Should().Contain("Total Debits");
		errorMsg.Should().Contain("Total Credits");
	}

	[Fact]
	public async Task IncomeStatement_FailsWithGLOutOfBalance()
	{
		// Arrange
		// Income Statement report should fail when GL is out of balance

		using var context = CreateContext();
		var transactionRepo = new TransactionRepository(context);
		var balanceService = new GlBalanceValidationService(transactionRepo);

		// Simulate unbalanced GL that would occur from data entry errors
		var largeDebit = new Transaction
		{
			Id = Guid.NewGuid(),
			Date = DateTime.UtcNow,
			Category = "Revenue",
			DebitAmount = 5000m,
			CreditAmount = null,
			CreatedAt = DateTime.UtcNow,
			ModifiedAt = DateTime.UtcNow
		};

		var smallCredit = new Transaction
		{
			Id = Guid.NewGuid(),
			Date = DateTime.UtcNow,
			Category = "Revenue",
			DebitAmount = null,
			CreditAmount = 3000m,
			CreatedAt = DateTime.UtcNow,
			ModifiedAt = DateTime.UtcNow
		};

		await transactionRepo.CreateAsync(largeDebit);
		await transactionRepo.CreateAsync(smallCredit);

		// Act
		var isBalanced = await balanceService.ValidateGLBalanceAsync();

		// Assert - Report generation should fail
		isBalanced.Should().BeFalse();

		var detailsResult = await balanceService.GetGLBalanceDetailsAsync();
		var debits = detailsResult.TotalDebits;
		var credits = detailsResult.TotalCredits;
		var balanced = detailsResult.IsBalanced;
		var diff = detailsResult.Difference;
		debits.Should().Be(5000m);
		credits.Should().Be(3000m);
		diff.Should().Be(2000m);
	}

	[Fact]
	public async Task GetGLBalanceErrorMessage_BalancedGL_ReturnsSuccessMessage()
	{
		// Arrange
		using var context = CreateContext();
		var transactionRepo = new TransactionRepository(context);
		var balanceService = new GlBalanceValidationService(transactionRepo);

		// Create balanced GL
		var debit = new Transaction
		{
			Id = Guid.NewGuid(),
			Date = DateTime.UtcNow,
			Category = "Test",
			DebitAmount = 500m,
			CreditAmount = null,
			CreatedAt = DateTime.UtcNow,
			ModifiedAt = DateTime.UtcNow
		};

		var credit = new Transaction
		{
			Id = Guid.NewGuid(),
			Date = DateTime.UtcNow,
			Category = "Test",
			DebitAmount = null,
			CreditAmount = 500m,
			CreatedAt = DateTime.UtcNow,
			ModifiedAt = DateTime.UtcNow
		};

		await transactionRepo.CreateAsync(debit);
		await transactionRepo.CreateAsync(credit);

		// Act
		var errorMessage = await balanceService.GetGLBalanceErrorMessageAsync();

		// Assert
		errorMessage.Should().Contain("balanced");
	}
}
