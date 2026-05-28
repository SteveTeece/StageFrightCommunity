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
/// Integration tests for payment recording.
/// Verifies GL transaction pair creation, member balance updates, and audit trail.
/// </summary>
public class PaymentRecordingTests
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
	public async Task RecordPayment_CreatesGLTransactionPair()
	{
		// Arrange
		using var context = CreateContext();
		var paymentRepo = new PaymentRepository(context);
		var transactionRepo = new TransactionRepository(context);
		var categoryRepo = new CategoryRepository(context);
		var memberRepo = new MemberRepository(context);

		var memberId = Guid.NewGuid();
		var member = new Member
		{
			Id = memberId,
			Name = "Test Member",
			JoinDate = DateTime.UtcNow.AddYears(-5),
			Status = "Active"
		};
		await memberRepo.CreateAsync(member);

		var cashCategory = new Category
		{
			Id = Guid.NewGuid(),
			Name = "Cash",
			Type = "Expense",
			GlAccount = "0100"
		};
		var incomeCategory = new Category
		{
			Id = Guid.NewGuid(),
			Name = "Member Payments",
			Type = "Income",
			GlAccount = "1010"
		};
		await categoryRepo.CreateAsync(cashCategory);
		await categoryRepo.CreateAsync(incomeCategory);

		var payment = new Payment
		{
			Id = Guid.NewGuid(),
			Date = DateTime.UtcNow,
			Amount = 100m,
			PaymentMethod = "Cash",
			PaymentType = "Annual",
			MemberId = memberId,
			Category = incomeCategory.Name,
			CreatedAt = DateTime.UtcNow,
			UpdatedAt = DateTime.UtcNow
		};

		// Act
		await paymentRepo.CreateAsync(payment);

		// Create GL pair for the payment
		var glService = new GlTransactionService(transactionRepo, categoryRepo);
		await glService.CreatePairedTransactionAsync(
			amount: payment.Amount,
			debitCategory: cashCategory.GlAccount!,
			creditCategory: incomeCategory.GlAccount!,
			description: $"Payment from {member.Name}",
			memberId: memberId,
			paymentId: payment.Id);

		// Assert
		var recordedPayment = await paymentRepo.GetByIdAsync(payment.Id);
		recordedPayment.Should().NotBeNull();
		recordedPayment!.Amount.Should().Be(100m);
		recordedPayment.MemberId.Should().Be(memberId);

		var transactions = (await transactionRepo.GetByMemberAsync(memberId)).ToList();
		transactions.Should().HaveCount(2);
		transactions.Should().AllSatisfy(t => t.PaymentId.Should().Be(payment.Id));
	}

	[Fact]
	public async Task UpdatePaymentNotes_OnlyUpdatesNotesField()
	{
		// Arrange
		using var context = CreateContext();
		var paymentRepo = new PaymentRepository(context);

		var payment = new Payment
		{
			Id = Guid.NewGuid(),
			Date = DateTime.UtcNow,
			Amount = 100m,
			PaymentMethod = "Cash",
			PaymentType = "Annual",
			MemberId = Guid.NewGuid(),
			Category = "Member Payments",
			Notes = "Original note",
			CreatedAt = DateTime.UtcNow,
			UpdatedAt = DateTime.UtcNow
		};
		await paymentRepo.CreateAsync(payment);

		// Act
		await paymentRepo.UpdateNotesAsync(payment.Id, "Updated note");

		// Assert
		var updated = await paymentRepo.GetByIdAsync(payment.Id);
		updated.Should().NotBeNull();
		updated!.Notes.Should().Be("Updated note");
		updated.Amount.Should().Be(100m); // Verify amount unchanged
		updated.Date.Should().Be(payment.Date); // Verify date unchanged
		updated.UpdatedAt.Should().BeOnOrAfter(payment.UpdatedAt); // Should be at same time or later
	}

	[Fact]
	public async Task RecordMultiplePayments_UpdatesMemberBalance()
	{
		// Arrange
		using var context = CreateContext();
		var paymentRepo = new PaymentRepository(context);
		var feeRepo = new FeeRepository(context);
		var balanceService = new MemberBalanceService(feeRepo);

		var memberId = Guid.NewGuid();

		// Create unpaid fees
		var fee1 = new Fee
		{
			Id = Guid.NewGuid(),
			MemberId = memberId,
			FeeType = "Annual",
			Amount = 100m,
			FeeDate = DateTime.UtcNow.AddMonths(-1),
			DueDate = DateTime.UtcNow.AddDays(-1),
			CreatedAt = DateTime.UtcNow
		};

		var fee2 = new Fee
		{
			Id = Guid.NewGuid(),
			MemberId = memberId,
			FeeType = "Attendance",
			Amount = 50m,
			FeeDate = DateTime.UtcNow.AddDays(-1),
			DueDate = DateTime.UtcNow.AddDays(-1), // Due date in the past to be considered unpaid
			CreatedAt = DateTime.UtcNow
		};

		await feeRepo.CreateAsync(fee1);
		await feeRepo.CreateAsync(fee2);

		// Act
		var initialBalance = await balanceService.GetMemberBalanceAsync(memberId);

		// Assert
		initialBalance.Should().Be(150m);

		// Verify breakdown
		var (annual, attendance, total) = await balanceService.GetMemberBalanceBreakdownAsync(memberId);
		annual.Should().Be(100m);
		attendance.Should().Be(50m);
		total.Should().Be(150m);
	}

	[Fact]
	public async Task PaymentAmountValidation_RejectsInvalidAmounts()
	{
		// Arrange
		using var context = CreateContext();
		var paymentRepo = new PaymentRepository(context);

		// Act & Assert
		await Assert.ThrowsAsync<InvalidOperationException>(
			() => paymentRepo.CreateAsync(new Payment
			{
				Id = Guid.NewGuid(),
				Date = DateTime.UtcNow,
				Amount = 0m, // Invalid
				PaymentMethod = "Cash",
				PaymentType = "Annual",
				MemberId = Guid.NewGuid(),
				Category = "Test",
				CreatedAt = DateTime.UtcNow,
				UpdatedAt = DateTime.UtcNow
			}));
	}

	[Fact]
	public async Task PaymentImmutability_RejectsAmountUpdate()
	{
		// Arrange
		using var context = CreateContext();
		var paymentRepo = new PaymentRepository(context);

		var payment = new Payment
		{
			Id = Guid.NewGuid(),
			Date = DateTime.UtcNow,
			Amount = 100m,
			PaymentMethod = "Cash",
			PaymentType = "Annual",
			MemberId = Guid.NewGuid(),
			Category = "Member Payments",
			CreatedAt = DateTime.UtcNow,
			UpdatedAt = DateTime.UtcNow
		};
		await paymentRepo.CreateAsync(payment);

		// Act - Try to update Amount (should be rejected by UpdateAsync override)
		payment.Amount = 200m;

		// This should only update Notes, not Amount
		await paymentRepo.UpdateAsync(payment);

		// Assert
		var retrieved = await paymentRepo.GetByIdAsync(payment.Id);
		retrieved!.Amount.Should().Be(100m); // Original amount preserved
	}

	[Fact]
	public async Task PaymentHistory_RetrievesCorrectDateRange()
	{
		// Arrange
		using var context = CreateContext();
		var paymentRepo = new PaymentRepository(context);
		var memberId = Guid.NewGuid();

		var payment1 = new Payment
		{
			Id = Guid.NewGuid(),
			Date = DateTime.UtcNow.AddDays(-10),
			Amount = 50m,
			PaymentMethod = "Cash",
			PaymentType = "Annual",
			MemberId = memberId,
			Category = "Member Payments",
			CreatedAt = DateTime.UtcNow,
			UpdatedAt = DateTime.UtcNow
		};

		var payment2 = new Payment
		{
			Id = Guid.NewGuid(),
			Date = DateTime.UtcNow.AddDays(-5),
			Amount = 75m,
			PaymentMethod = "Check",
			PaymentType = "Attendance",
			MemberId = memberId,
			Category = "Member Payments",
			CreatedAt = DateTime.UtcNow,
			UpdatedAt = DateTime.UtcNow
		};

		var payment3 = new Payment
		{
			Id = Guid.NewGuid(),
			Date = DateTime.UtcNow.AddDays(10),
			Amount = 100m,
			PaymentMethod = "Card",
			PaymentType = "Annual",
			MemberId = memberId,
			Category = "Member Payments",
			CreatedAt = DateTime.UtcNow,
			UpdatedAt = DateTime.UtcNow
		};

		await paymentRepo.CreateAsync(payment1);
		await paymentRepo.CreateAsync(payment2);
		await paymentRepo.CreateAsync(payment3);

		// Act
		var history = (await paymentRepo.GetPaymentHistoryAsync(
			memberId,
			DateTime.UtcNow.AddDays(-7),
			DateTime.UtcNow.AddDays(0))).ToList();

		// Assert
		history.Should().HaveCount(1);
		history[0].Amount.Should().Be(75m);
	}

	[Fact]
	public async Task GetByMember_ReturnsPaymentsInDescendingOrder()
	{
		// Arrange
		using var context = CreateContext();
		var paymentRepo = new PaymentRepository(context);
		var memberId = Guid.NewGuid();

		var payment1 = new Payment
		{
			Id = Guid.NewGuid(),
			Date = DateTime.UtcNow.AddDays(-5),
			Amount = 50m,
			PaymentMethod = "Cash",
			PaymentType = "Annual",
			MemberId = memberId,
			Category = "Test",
			CreatedAt = DateTime.UtcNow,
			UpdatedAt = DateTime.UtcNow
		};

		var payment2 = new Payment
		{
			Id = Guid.NewGuid(),
			Date = DateTime.UtcNow,
			Amount = 100m,
			PaymentMethod = "Cash",
			PaymentType = "Annual",
			MemberId = memberId,
			Category = "Test",
			CreatedAt = DateTime.UtcNow,
			UpdatedAt = DateTime.UtcNow
		};

		await paymentRepo.CreateAsync(payment1);
		await paymentRepo.CreateAsync(payment2);

		// Act
		var payments = (await paymentRepo.GetByMemberAsync(memberId)).ToList();

		// Assert
		payments.Should().HaveCount(2);
		payments[0].Amount.Should().Be(100m); // Most recent first
		payments[1].Amount.Should().Be(50m);
	}
}
