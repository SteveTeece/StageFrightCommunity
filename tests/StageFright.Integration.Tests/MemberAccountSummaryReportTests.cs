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
/// Integration tests for Member Account Summary report provider.
/// Verifies aging bucket calculations, member balances, and archived member inclusion.
/// </summary>
public class MemberAccountSummaryReportTests : IAsyncLifetime
{
	private readonly StageFrightContext _context;
	private MemberAccountSummaryReportProvider _provider;
	private readonly Mock<ILogger<MemberAccountSummaryReportProvider>> _mockLogger;

	public MemberAccountSummaryReportTests()
	{
		var options = new DbContextOptionsBuilder<StageFrightContext>()
			.UseInMemoryDatabase($"MemberAccountSummaryTest_{Guid.NewGuid()}")
			.Options;

		_context = new StageFrightContext(options);
		_mockLogger = new Mock<ILogger<MemberAccountSummaryReportProvider>>();
	}

	public async Task InitializeAsync()
	{
		await _context.Database.EnsureCreatedAsync();
		_provider = new MemberAccountSummaryReportProvider(
			new MemberRepository(_context),
			new FeeRepository(_context),
			new PaymentRepository(_context),
			new MemberBalanceService(new FeeRepository(_context)),
			_mockLogger.Object);
	}

	public async Task DisposeAsync()
	{
		await _context.Database.EnsureDeletedAsync();
		_context.Dispose();
	}

	[Fact]
	public async Task GenerateAsync_WithMembersHavingUnpaidFees_ReturnsOutstandingBalances()
	{
		// Arrange
		var member = new Member
		{
			Id = Guid.NewGuid(),
			Name = "Test Member",
			StreetAddress = "123 Main St",
			JoinDate = new DateTime(2025, 1, 1),
			Status = "Active"
		};

		var fee = new Fee
		{
			Id = Guid.NewGuid(),
			MemberId = member.Id,
			FeeType = "Annual",
			Amount = 100m,
			FeeDate = new DateTime(2026, 1, 1),
			DueDate = new DateTime(2026, 1, 15),
			CreatedAt = DateTime.UtcNow
		};

		_context.Members.Add(member);
		_context.Fees.Add(fee);
		await _context.SaveChangesAsync();

		// Act
		var report = await _provider.GenerateAsync();

		// Assert
		report.ReportTitle.Should().Contain("Member Account Summary");
		report.ColumnHeaders.Should().Equal("Member Name", "Status", "Unpaid Fees", "Outstanding Balance", "Aging");
		report.Rows.Should().HaveCount(1);
		report.Rows[0][0].Should().Be("Test Member");
		report.Rows[0][1].Should().Be("Active");
		report.Rows[0][2].Should().Be("1");
		report.Rows[0][3].Should().Contain("100");
	}

	[Fact]
	public async Task GenerateAsync_WithOldUnpaidFees_ClassifiesCorrectAgingBucket()
	{
		// Arrange
		var member = new Member
		{
			Id = Guid.NewGuid(),
			Name = "Overdue Member",
			StreetAddress = "456 Oak St",
			JoinDate = new DateTime(2025, 1, 1),
			Status = "Active"
		};

		var oldFee = new Fee
		{
			Id = Guid.NewGuid(),
			MemberId = member.Id,
			FeeType = "Annual",
			Amount = 150m,
			FeeDate = new DateTime(2025, 6, 1),  // 90+ days old
			DueDate = new DateTime(2025, 7, 1),
			CreatedAt = DateTime.UtcNow
		};

		_context.Members.Add(member);
		_context.Fees.Add(oldFee);
		await _context.SaveChangesAsync();

		// Act
		var report = await _provider.GenerateAsync();

		// Assert
		report.Rows.Should().HaveCount(1);
		report.Rows[0][4].Should().Contain("90+ days");
	}

	[Fact]
	public async Task GenerateAsync_IncludesInactiveMembers()
	{
		// Arrange
		var activeMember = new Member
		{
			Id = Guid.NewGuid(),
			Name = "Active Member",
			StreetAddress = "111 Oak St",
			JoinDate = new DateTime(2025, 1, 1),
			Status = "Active"
		};

		var inactiveMember = new Member
		{
			Id = Guid.NewGuid(),
			Name = "Inactive Member",
			StreetAddress = "222 Oak St",
			JoinDate = new DateTime(2024, 1, 1),
			Status = "Inactive"
		};

		// Add unpaid fee for active member
		var fee1 = new Fee
		{
			Id = Guid.NewGuid(),
			MemberId = activeMember.Id,
			FeeType = "Annual",
			Amount = 100m,
			FeeDate = new DateTime(2026, 1, 1),
			DueDate = new DateTime(2026, 1, 15),
			CreatedAt = DateTime.UtcNow
		};

		// Add unpaid fee for inactive member
		var fee2 = new Fee
		{
			Id = Guid.NewGuid(),
			MemberId = inactiveMember.Id,
			FeeType = "Annual",
			Amount = 50m,
			FeeDate = new DateTime(2026, 1, 1),
			DueDate = new DateTime(2026, 1, 15),
			CreatedAt = DateTime.UtcNow
		};

		_context.Members.Add(activeMember);
		_context.Members.Add(inactiveMember);
		_context.Fees.Add(fee1);
		_context.Fees.Add(fee2);
		await _context.SaveChangesAsync();

		// Act
		var report = await _provider.GenerateAsync();

		// Assert
		report.Rows.Should().HaveCount(2);
		var inactiveRow = report.Rows.FirstOrDefault(r => r[0] == "Inactive Member");
		inactiveRow.Should().NotBeNull();
		inactiveRow[1].Should().Be("Inactive");
	}

	[Fact]
	public async Task GenerateAsync_WithNoUnpaidFees_ExcludesMemberFromReport()
	{
		// Arrange
		var member = new Member
		{
			Id = Guid.NewGuid(),
			Name = "Paid Member",
			StreetAddress = "333 Oak St",
			JoinDate = new DateTime(2025, 1, 1),
			Status = "Active"
		};

		_context.Members.Add(member);
		await _context.SaveChangesAsync();

		// Act
		var report = await _provider.GenerateAsync();

		// Assert
		report.Rows.Should().BeEmpty();
	}
}
