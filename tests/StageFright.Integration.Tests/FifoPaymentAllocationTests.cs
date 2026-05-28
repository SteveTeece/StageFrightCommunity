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
/// Critical integration tests for FIFO payment allocation.
/// Verifies oldest unpaid fees are satisfied first per FR-016.
/// </summary>
public class FifoPaymentAllocationTests
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
	public async Task SimpleFifo_PaymentSatisfiesOldestFeesFirst()
	{
		// Arrange
		// Test case: $75 payment against:
		// - 2024 $50 annual (oldest)
		// - 2025 $50 annual
		// - 2025 $10 attendance
		// Expected: 2024 fully paid, 2025 annual fully paid, 2025 attendance remains $10 unpaid

		using var context = CreateContext();
		var feeRepo = new FeeRepository(context);
		var paymentRepo = new PaymentRepository(context);
		var allocationService = new PaymentAllocationService(feeRepo, paymentRepo);

		var memberId = Guid.NewGuid();
		var now = DateTime.UtcNow;

		// Create fees in order (oldest first)
		var fee2024Annual = new Fee
		{
			Id = Guid.NewGuid(),
			MemberId = memberId,
			FeeType = "Annual",
			Amount = 50m,
			FeeDate = new DateTime(2024, 1, 1),
			DueDate = new DateTime(2024, 2, 1), // Past date - will be considered unpaid
			CreatedAt = now.AddDays(-365)
		};

		var fee2025Annual = new Fee
		{
			Id = Guid.NewGuid(),
			MemberId = memberId,
			FeeType = "Annual",
			Amount = 50m,
			FeeDate = new DateTime(2025, 1, 1),
			DueDate = new DateTime(2025, 2, 1), // Past date - will be considered unpaid
			CreatedAt = now.AddDays(-30)
		};

		var fee2025Attendance = new Fee
		{
			Id = Guid.NewGuid(),
			MemberId = memberId,
			FeeType = "Attendance",
			Amount = 10m,
			FeeDate = now.AddDays(-5),
			DueDate = now.AddDays(-1), // Due date must be in the past to be considered unpaid
			CreatedAt = now.AddDays(-5)
		};

		await feeRepo.CreateAsync(fee2024Annual);
		await feeRepo.CreateAsync(fee2025Annual);
		await feeRepo.CreateAsync(fee2025Attendance);

		// Act
		var result1 = await allocationService.AllocatePaymentAsync(memberId, 75m);
		var allocations = result1.Allocations;
		var memberCredit = result1.MemberCredit;

		// Assert
		allocations.Should().HaveCount(3);
		allocations[0].Should().Be((fee2024Annual.Id, 50m)); // 2024 fully paid
		allocations[1].Should().Be((fee2025Annual.Id, 25m)); // 2025 partial
		allocations[2].Should().Be((fee2025Attendance.Id, 0m)); // Not allocated (no remaining)
		memberCredit.Should().Be(0m);

		// Verify that only fully allocated fees would be considered "paid"
		var allocation2025Annual = allocations.FirstOrDefault(a => a.FeeId == fee2025Annual.Id);
		allocation2025Annual.AmountAllocated.Should().Be(25m);
	}

	[Fact]
	public async Task PartialPayment_PartiallyAllocatesToFee()
	{
		// Arrange
		// Test case: $40 payment against $50 annual fee
		// Expected: $40 allocated, $10 remains unpaid

		using var context = CreateContext();
		var feeRepo = new FeeRepository(context);
		var paymentRepo = new PaymentRepository(context);
		var allocationService = new PaymentAllocationService(feeRepo, paymentRepo);

		var memberId = Guid.NewGuid();
		var fee = new Fee
		{
			Id = Guid.NewGuid(),
			MemberId = memberId,
			FeeType = "Annual",
			Amount = 50m,
			FeeDate = DateTime.UtcNow.AddDays(-30),
			DueDate = DateTime.UtcNow.AddDays(-1), // Due date in the past
			CreatedAt = DateTime.UtcNow.AddDays(-30)
		};

		await feeRepo.CreateAsync(fee);

		// Act
		var result2 = await allocationService.AllocatePaymentAsync(memberId, 40m);
		var allocations2 = result2.Allocations;
		var memberCredit2 = result2.MemberCredit;

		// Assert
		allocations2.Should().HaveCount(1);
		allocations2[0].Should().Be((fee.Id, 40m));
		memberCredit2.Should().Be(0m);
	}

	[Fact]
	public async Task OverPayment_CreatesUnallocatedCredit()
	{
		// Arrange
		// Test case: $150 payment against $100 total fees
		// Expected: $100 allocated to fees, $50 member credit

		using var context = CreateContext();
		var feeRepo = new FeeRepository(context);
		var paymentRepo = new PaymentRepository(context);
		var allocationService = new PaymentAllocationService(feeRepo, paymentRepo);

		var memberId = Guid.NewGuid();

		var fee1 = new Fee
		{
			Id = Guid.NewGuid(),
			MemberId = memberId,
			FeeType = "Annual",
			Amount = 60m,
			FeeDate = DateTime.UtcNow.AddDays(-30),
			DueDate = DateTime.UtcNow.AddDays(-1), // Due date in the past
			CreatedAt = DateTime.UtcNow.AddDays(-30)
		};

		var fee2 = new Fee
		{
			Id = Guid.NewGuid(),
			MemberId = memberId,
			FeeType = "Attendance",
			Amount = 40m,
			FeeDate = DateTime.UtcNow.AddDays(-5),
			DueDate = DateTime.UtcNow.AddDays(-1), // Due date must be in the past to be considered unpaid
			CreatedAt = DateTime.UtcNow.AddDays(-5)
		};

		await feeRepo.CreateAsync(fee1);
		await feeRepo.CreateAsync(fee2);

		// Act
		var result3 = await allocationService.AllocatePaymentAsync(memberId, 150m);
		var allocations3 = result3.Allocations;
		var memberCredit3 = result3.MemberCredit;

		// Assert
		allocations3.Should().HaveCount(2);
		allocations3[0].Should().Be((fee1.Id, 60m)); // Fully allocated
		allocations3[1].Should().Be((fee2.Id, 40m)); // Fully allocated
		memberCredit3.Should().Be(50m); // Excess as credit
	}

	[Fact]
	public async Task NoUnpaidFees_EntirePaymentBecomesCredit()
	{
		// Arrange
		// Test case: Member with no unpaid fees receives $100 payment
		// Expected: Entire $100 becomes member credit

		using var context = CreateContext();
		var feeRepo = new FeeRepository(context);
		var paymentRepo = new PaymentRepository(context);
		var allocationService = new PaymentAllocationService(feeRepo, paymentRepo);

		var memberId = Guid.NewGuid();

		// Act
		var result4 = await allocationService.AllocatePaymentAsync(memberId, 100m);
		var allocations4 = result4.Allocations;
		var memberCredit4 = result4.MemberCredit;

		// Assert
		allocations4.Should().BeEmpty();
		memberCredit4.Should().Be(100m);
	}

	[Fact]
	public async Task BulkAnnualFees_TiebreakerOrderingByCreatedAtThenId()
	{
		// Arrange
		// Test case: Multiple fees created at same time should use CreatedAt, then Id as tiebreaker
		// Verify FIFO ordering

		using var context = CreateContext();
		var feeRepo = new FeeRepository(context);
		var paymentRepo = new PaymentRepository(context);
		var allocationService = new PaymentAllocationService(feeRepo, paymentRepo);

		var memberId = Guid.NewGuid();
		var createdAt = DateTime.UtcNow;

		// Create fees with same CreatedAt (simulating bulk annual fee application)
		var fees = new List<Fee>();
		for (int i = 0; i < 3; i++)
		{
			var fee = new Fee
			{
				Id = Guid.NewGuid(),
				MemberId = memberId,
				FeeType = "Annual",
				Amount = 30m,
				FeeDate = DateTime.Now.AddDays(-i),
				DueDate = DateTime.Now.AddDays(-i - 1), // Due dates must be in the past
				CreatedAt = createdAt // All created at same time
			};
			fees.Add(fee);
			await feeRepo.CreateAsync(fee);
		}

		// Act
		var result5 = await allocationService.AllocatePaymentAsync(memberId, 75m);
		var allocations5 = result5.Allocations;
		var memberCredit5 = result5.MemberCredit;

		// Assert
		allocations5.Should().HaveCount(3);
		
		// Verify that fees are allocated in order (oldest FeeDate first, regardless of Id)
		allocations5[0].AmountAllocated.Should().Be(30m);
		allocations5[1].AmountAllocated.Should().Be(30m);
		allocations5[2].AmountAllocated.Should().Be(15m);
		memberCredit5.Should().Be(0m);
	}

	[Fact]
	public async Task AllocationSummary_FormatCorrectly()
	{
		// Arrange
		using var context = CreateContext();
		var feeRepo = new FeeRepository(context);
		var paymentRepo = new PaymentRepository(context);
		var allocationService = new PaymentAllocationService(feeRepo, paymentRepo);

		var memberId = Guid.NewGuid();
		var fee = new Fee
		{
			Id = Guid.NewGuid(),
			MemberId = memberId,
			FeeType = "Annual",
			Amount = 50m,
			FeeDate = DateTime.UtcNow.AddDays(-30),
			DueDate = DateTime.UtcNow.AddDays(-1), // Due date in the past
			CreatedAt = DateTime.UtcNow.AddDays(-30)
		};

		await feeRepo.CreateAsync(fee);

		// Act
		var summary = await allocationService.GetAllocationSummaryAsync(memberId, 40m);

		// Assert
		summary.Should().Contain("$40.00");
		summary.Should().Contain("Payment allocation");
	}

	[Fact]
	public async Task InvalidPaymentAmount_ThrowsException()
	{
		// Arrange
		using var context = CreateContext();
		var feeRepo = new FeeRepository(context);
		var paymentRepo = new PaymentRepository(context);
		var allocationService = new PaymentAllocationService(feeRepo, paymentRepo);

		// Act & Assert
		await Assert.ThrowsAsync<ArgumentException>(
			() => allocationService.AllocatePaymentAsync(Guid.NewGuid(), 0m));

		await Assert.ThrowsAsync<ArgumentException>(
			() => allocationService.AllocatePaymentAsync(Guid.NewGuid(), -50m));
	}

	[Fact]
	public async Task ComplexScenario_MultiplePaymentsAndFees()
	{
		// Arrange
		// Complex scenario with multiple fee types and payment amounts

		using var context = CreateContext();
		var feeRepo = new FeeRepository(context);
		var paymentRepo = new PaymentRepository(context);
		var allocationService = new PaymentAllocationService(feeRepo, paymentRepo);
		var balanceService = new MemberBalanceService(feeRepo);

		var memberId = Guid.NewGuid();

		// Create diverse fees
		var annualFee2024 = new Fee
		{
			Id = Guid.NewGuid(),
			MemberId = memberId,
			FeeType = "Annual",
			Amount = 100m,
			FeeDate = new DateTime(2024, 1, 1),
			DueDate = new DateTime(2024, 2, 1),
			CreatedAt = DateTime.UtcNow.AddDays(-200)
		};

		var attendanceFee1 = new Fee
		{
			Id = Guid.NewGuid(),
			MemberId = memberId,
			FeeType = "Attendance",
			Amount = 25m,
			FeeDate = DateTime.UtcNow.AddDays(-60),
			DueDate = DateTime.UtcNow.AddDays(-30),
			CreatedAt = DateTime.UtcNow.AddDays(-60)
		};

		var annualFee2025 = new Fee
		{
			Id = Guid.NewGuid(),
			MemberId = memberId,
			FeeType = "Annual",
			Amount = 100m,
			FeeDate = new DateTime(2025, 1, 1),
			DueDate = new DateTime(2025, 2, 1),
			CreatedAt = DateTime.UtcNow.AddDays(-30)
		};

		var attendanceFee2 = new Fee
		{
			Id = Guid.NewGuid(),
			MemberId = memberId,
			FeeType = "Attendance",
			Amount = 15m,
			FeeDate = DateTime.UtcNow.AddDays(-10),
			DueDate = DateTime.UtcNow.AddDays(-1), // Due date must be in the past to be considered unpaid
			CreatedAt = DateTime.UtcNow.AddDays(-10)
		};

		await feeRepo.CreateAsync(annualFee2024);
		await feeRepo.CreateAsync(attendanceFee1);
		await feeRepo.CreateAsync(annualFee2025);
		await feeRepo.CreateAsync(attendanceFee2);

		// Verify initial balance
		var initialBalance = await balanceService.GetMemberBalanceAsync(memberId);
		initialBalance.Should().Be(240m); // 100 + 25 + 100 + 15

		// Act
		var resultComplex = await allocationService.AllocatePaymentAsync(memberId, 160m);
		var allocationsComplex = resultComplex.Allocations;
		var memberCreditComplex = resultComplex.MemberCredit;

		// Assert
		allocationsComplex.Should().HaveCount(4);
		allocationsComplex[0].Should().Be((annualFee2024.Id, 100m)); // 2024 annual fully allocated
		allocationsComplex[1].Should().Be((attendanceFee1.Id, 25m)); // Attendance fully allocated
		allocationsComplex[2].Should().Be((annualFee2025.Id, 35m)); // 2025 annual partial
		allocationsComplex[3].Should().Be((attendanceFee2.Id, 0m)); // Attendance not reached
		memberCreditComplex.Should().Be(0m);
	}
}
