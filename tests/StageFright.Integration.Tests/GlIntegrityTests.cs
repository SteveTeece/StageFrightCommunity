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
/// Integration tests for GL (General Ledger) integrity.
/// Verifies paired transactions, balance validation, and FIFO allocation.
/// </summary>
public class GlIntegrityTests
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
	public async Task CreatePairedTransaction_CreatesDebitAndCreditEntries()
	{
		// Arrange
		using var context = CreateContext();
		var transactionRepo = new TransactionRepository(context);
		var categoryRepo = new CategoryRepository(context);
		var service = new GlTransactionService(transactionRepo, categoryRepo);

		// Create test categories
		var incomeCat = new Category { Id = Guid.NewGuid(), Name = "Member Payments", Type = "Income", GlAccount = "1010" };
		var assetCat = new Category { Id = Guid.NewGuid(), Name = "Cash", Type = "Expense", GlAccount = "0100" };
		await categoryRepo.CreateAsync(incomeCat);
		await categoryRepo.CreateAsync(assetCat);

		// Act
		await service.CreatePairedTransactionAsync(
			amount: 100m,
			debitCategory: assetCat.GlAccount!,
			creditCategory: incomeCat.GlAccount!,
			description: "Test payment");

		// Assert
		var allTransactions = await transactionRepo.GetByDateRangeAsync(DateTime.MinValue, DateTime.MaxValue);
		var transactions = allTransactions.ToList();

		transactions.Should().HaveCount(2);

		var debitTrans = transactions.FirstOrDefault(t => t.DebitAmount.HasValue);
		var creditTrans = transactions.FirstOrDefault(t => t.CreditAmount.HasValue);

		debitTrans.Should().NotBeNull();
		creditTrans.Should().NotBeNull();
		debitTrans!.DebitAmount.Should().Be(100m);
		creditTrans!.CreditAmount.Should().Be(100m);
		debitTrans.Date.Should().Be(DateTime.UtcNow.Date);
		creditTrans.Date.Should().Be(DateTime.UtcNow.Date);
	}

	[Fact]
	public async Task CreatePairedTransaction_WithMemberId_AssociatesTransactions()
	{
		// Arrange
		using var context = CreateContext();
		var transactionRepo = new TransactionRepository(context);
		var categoryRepo = new CategoryRepository(context);
		var service = new GlTransactionService(transactionRepo, categoryRepo);

		var memberId = Guid.NewGuid();
		var incomeCat = new Category { Id = Guid.NewGuid(), Name = "Income", Type = "Income", GlAccount = "1010" };
		var assetCat = new Category { Id = Guid.NewGuid(), Name = "Asset", Type = "Expense", GlAccount = "0100" };
		await categoryRepo.CreateAsync(incomeCat);
		await categoryRepo.CreateAsync(assetCat);

		// Act
		await service.CreatePairedTransactionAsync(
			amount: 50m,
			debitCategory: assetCat.GlAccount!,
			creditCategory: incomeCat.GlAccount!,
			memberId: memberId);

		// Assert
		var byMember = (await transactionRepo.GetByMemberAsync(memberId)).ToList();
		byMember.Should().HaveCount(2);
		byMember.Should().AllSatisfy(t => t.MemberId.Should().Be(memberId));
	}

	[Fact]
	public async Task ValidateGLBalance_WithBalancedTransactions_ReturnsTrue()
	{
		// Arrange
		using var context = CreateContext();
		var transactionRepo = new TransactionRepository(context);
		var balanceService = new GlBalanceValidationService(transactionRepo);
		var categoryRepo = new CategoryRepository(context);
		var glService = new GlTransactionService(transactionRepo, categoryRepo);

		var income = new Category { Id = Guid.NewGuid(), Name = "Income", Type = "Income", GlAccount = "1010" };
		var asset = new Category { Id = Guid.NewGuid(), Name = "Asset", Type = "Expense", GlAccount = "0100" };
		await categoryRepo.CreateAsync(income);
		await categoryRepo.CreateAsync(asset);

		// Create paired transactions
		await glService.CreatePairedTransactionAsync(100m, asset.GlAccount!, income.GlAccount!);
		await glService.CreatePairedTransactionAsync(50m, asset.GlAccount!, income.GlAccount!);

		// Act
		var isBalanced = await balanceService.ValidateGLBalanceAsync();

		// Assert
		isBalanced.Should().BeTrue();
	}

	[Fact]
	public async Task ValidateGLBalance_WithImbalancedTransactions_ReturnsFalse()
	{
		// Arrange
		using var context = CreateContext();
		var transactionRepo = new TransactionRepository(context);
		var balanceService = new GlBalanceValidationService(transactionRepo);

		// Manually create imbalanced transaction (normally prevented by service)
		var unbalancedDebit = new Transaction
		{
			Id = Guid.NewGuid(),
			Date = DateTime.UtcNow,
			Category = "Test",
			DebitAmount = 100m,
			CreditAmount = null,
			CreatedAt = DateTime.UtcNow,
			ModifiedAt = DateTime.UtcNow
		};

		await transactionRepo.CreateAsync(unbalancedDebit);

		// Act
		var isBalanced = await balanceService.ValidateGLBalanceAsync();

		// Assert
		isBalanced.Should().BeFalse();
	}

	[Fact]
	public async Task GetGLBalanceDetails_ReturnsCorrectTotals()
	{
		// Arrange
		using var context = CreateContext();
		var transactionRepo = new TransactionRepository(context);
		var balanceService = new GlBalanceValidationService(transactionRepo);
		var categoryRepo = new CategoryRepository(context);
		var glService = new GlTransactionService(transactionRepo, categoryRepo);

		var income = new Category { Id = Guid.NewGuid(), Name = "Income", Type = "Income", GlAccount = "1010" };
		var asset = new Category { Id = Guid.NewGuid(), Name = "Asset", Type = "Expense", GlAccount = "0100" };
		await categoryRepo.CreateAsync(income);
		await categoryRepo.CreateAsync(asset);

		// Create paired transactions
		await glService.CreatePairedTransactionAsync(150m, asset.GlAccount!, income.GlAccount!);

		// Act
		var (debits, credits, isBalanced, diff) = await balanceService.GetGLBalanceDetailsAsync();

		// Assert
		debits.Should().Be(150m);
		credits.Should().Be(150m);
		isBalanced.Should().BeTrue();
		diff.Should().Be(0m);
	}

	[Fact]
	public async Task TransactionRepository_EnforcesImmutability()
	{
		// Arrange
		using var context = CreateContext();
		var transactionRepo = new TransactionRepository(context);
		var transaction = new Transaction
		{
			Id = Guid.NewGuid(),
			Date = DateTime.UtcNow,
			Category = "Test",
			DebitAmount = 100m,
			CreatedAt = DateTime.UtcNow,
			ModifiedAt = DateTime.UtcNow
		};
		await transactionRepo.CreateAsync(transaction);

		// Act & Assert
		var ex = await Assert.ThrowsAsync<InvalidOperationException>(
			() => transactionRepo.UpdateAsync(transaction));

		ex.Message.Should().Contain("immutable");
	}

	[Fact]
	public async Task CreatePairedTransaction_WithInvalidAmount_ThrowsException()
	{
		// Arrange
		using var context = CreateContext();
		var transactionRepo = new TransactionRepository(context);
		var categoryRepo = new CategoryRepository(context);
		var service = new GlTransactionService(transactionRepo, categoryRepo);

		// Act & Assert
		await Assert.ThrowsAsync<ArgumentException>(
			() => service.CreatePairedTransactionAsync(-50m, "cat1", "cat2"));

		await Assert.ThrowsAsync<ArgumentException>(
			() => service.CreatePairedTransactionAsync(0m, "cat1", "cat2"));
	}

	[Fact]
	public async Task CreatePairedTransaction_WithSameCategories_ThrowsException()
	{
		// Arrange
		using var context = CreateContext();
		var transactionRepo = new TransactionRepository(context);
		var categoryRepo = new CategoryRepository(context);
		var service = new GlTransactionService(transactionRepo, categoryRepo);

		// Act & Assert
		var ex = await Assert.ThrowsAsync<ArgumentException>(
			() => service.CreatePairedTransactionAsync(100m, "category1", "category1"));

		ex.Message.Should().Contain("different");
	}
}
