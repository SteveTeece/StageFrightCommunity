using Microsoft.EntityFrameworkCore;
using StageFright.Core.Entities;
using StageFright.Data.Context;
using StageFright.Data.Repositories;
using Xunit;

namespace StageFright.Data.Tests;

/// <summary>
/// Comprehensive CRUD tests for all repository implementations.
/// Verifies basic create, read, update, delete operations and specialized repository methods.
/// </summary>
public class RepositoryTests
{
	private DbContextOptions<StageFrightContext> CreateInMemoryOptions()
	{
		return new DbContextOptionsBuilder<StageFrightContext>()
			.UseInMemoryDatabase(Guid.NewGuid().ToString())
			.EnableSensitiveDataLogging()
			.ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
			.Options;
	}

	#region Member Repository Tests

	[Fact]
	public async Task MemberRepository_CreateAsync_PersistsMember()
	{
		// Arrange
		var options = CreateInMemoryOptions();
		using var context = new StageFrightContext(options);
		var repository = new MemberRepository(context);
		var member = new Member { Name = "John Doe", StreetAddress = "123 Main St", JoinDate = DateTime.Now };

		// Act
		await repository.CreateAsync(member);
		var retrieved = await repository.GetByIdAsync(member.Id);

		// Assert
		Assert.NotNull(retrieved);
		Assert.Equal("John Doe", retrieved.Name);
	}

	[Fact]
	public async Task MemberRepository_GetActiveMembersAsync_ReturnsOnlyActivemembers()
	{
		// Arrange
		var options = CreateInMemoryOptions();
		using var context = new StageFrightContext(options);
		var repository = new MemberRepository(context);

		var activeMember = new Member { Name = "Active", StreetAddress = "123 Main St", JoinDate = DateTime.Now, Status = "Active" };
		var inactiveMember = new Member { Name = "Inactive", StreetAddress = "456 Oak Ave", JoinDate = DateTime.Now, Status = "Inactive" };

		await repository.CreateAsync(activeMember);
		await repository.CreateAsync(inactiveMember);

		// Act
		var active = await repository.GetActiveMembersAsync();

		// Assert
		Assert.Single(active);
		Assert.Equal("Active", active.First().Name);
	}

	[Fact]
	public async Task MemberRepository_GetHistoricalActiveMembersAsync_FiltersByDate()
	{
		// Arrange
		var options = CreateInMemoryOptions();
		using var context = new StageFrightContext(options);
		var repository = new MemberRepository(context);

		var joinDate = new DateTime(2024, 1, 1);
		var asOfDate = new DateTime(2024, 6, 1);
		var member = new Member { Name = "Test", StreetAddress = "123 Main St", JoinDate = joinDate, Status = "Active" };

		await repository.CreateAsync(member);

		// Act
		var historical = await repository.GetHistoricalActiveMembersAsync(asOfDate);

		// Assert
		Assert.Single(historical);
		Assert.Equal("Test", historical.First().Name);
	}

	[Fact]
	public async Task MemberRepository_GetByEmailAsync_FindsMemberByEmail()
	{
		// Arrange
		var options = CreateInMemoryOptions();
		using var context = new StageFrightContext(options);
		var repository = new MemberRepository(context);

		var member = new Member { Name = "Test", StreetAddress = "123 Main St", JoinDate = DateTime.Now, Email = "test@example.com" };
		await repository.CreateAsync(member);

		// Act
		var found = await repository.GetByEmailAsync("test@example.com");

		// Assert
		Assert.NotNull(found);
		Assert.Equal("test@example.com", found.Email);
	}

	[Fact]
	public async Task MemberRepository_GetActiveMemberCountAsync_ReturnsCorrectCount()
	{
		// Arrange
		var options = CreateInMemoryOptions();
		using var context = new StageFrightContext(options);
		var repository = new MemberRepository(context);

		for (int i = 0; i < 3; i++)
		{
			var member = new Member { Name = $"Member {i}", StreetAddress = "123 Main St", JoinDate = DateTime.Now, Status = "Active" };
			await repository.CreateAsync(member);
		}

		// Act
		var count = await repository.GetActiveMemberCountAsync();

		// Assert
		Assert.Equal(3, count);
	}

	#endregion

	#region Rehearsal Repository Tests

	[Fact]
	public async Task RehearsalRepository_GetByDateRangeAsync_FiltersCorrectly()
	{
		// Arrange
		var options = CreateInMemoryOptions();
		using var context = new StageFrightContext(options);
		var repository = new RehearsalRepository(context);

		var date1 = new DateTime(2024, 6, 1);
		var date2 = new DateTime(2024, 6, 15);
		var date3 = new DateTime(2024, 7, 1);

		var rehearsal1 = new Rehearsal { Date = date1, Time = TimeSpan.Parse("19:00") };
		var rehearsal2 = new Rehearsal { Date = date2, Time = TimeSpan.Parse("19:00") };
		var rehearsal3 = new Rehearsal { Date = date3, Time = TimeSpan.Parse("19:00") };

		await repository.CreateAsync(rehearsal1);
		await repository.CreateAsync(rehearsal2);
		await repository.CreateAsync(rehearsal3);

		// Act
		var inRange = await repository.GetByDateRangeAsync(date1, date2);

		// Assert
		Assert.Equal(2, inRange.Count());
	}

	[Fact]
	public async Task RehearsalRepository_UpdateStoredAttendanceRateAsync_UpdatesRate()
	{
		// Arrange
		var options = CreateInMemoryOptions();
		using var context = new StageFrightContext(options);
		var repository = new RehearsalRepository(context);

		var rehearsal = new Rehearsal { Date = DateTime.Now, Time = TimeSpan.Parse("19:00"), StoredAttendanceRate = 0 };
		await repository.CreateAsync(rehearsal);

		// Act
		await repository.UpdateStoredAttendanceRateAsync(rehearsal.Id, 85.5m);
		var updated = await repository.GetByIdAsync(rehearsal.Id);

		// Assert
		Assert.NotNull(updated);
		Assert.Equal(85.5m, updated.StoredAttendanceRate);
	}

	[Fact]
	public async Task RehearsalRepository_UpdateStoredAttendanceRateAsync_ClampsTo0To100()
	{
		// Arrange
		var options = CreateInMemoryOptions();
		using var context = new StageFrightContext(options);
		var repository = new RehearsalRepository(context);

		var rehearsal = new Rehearsal { Date = DateTime.Now, Time = TimeSpan.Parse("19:00"), StoredAttendanceRate = 0 };
		await repository.CreateAsync(rehearsal);

		// Act
		await repository.UpdateStoredAttendanceRateAsync(rehearsal.Id, 150);
		var updated = await repository.GetByIdAsync(rehearsal.Id);

		// Assert
		Assert.Equal(100, updated.StoredAttendanceRate);
	}

	#endregion

	#region Event Repository Tests

	[Fact]
	public async Task EventRepository_GetByDateRangeAsync_FiltersCorrectly()
	{
		// Arrange
		var options = CreateInMemoryOptions();
		using var context = new StageFrightContext(options);
		var repository = new EventRepository(context);

		var date1 = new DateTime(2024, 6, 1);
		var date2 = new DateTime(2024, 6, 15);
		var date3 = new DateTime(2024, 7, 1);

		var event1 = new Event { Date = date1, EventType = "Concert" };
		var event2 = new Event { Date = date2, EventType = "Concert" };
		var event3 = new Event { Date = date3, EventType = "Concert" };

		await repository.CreateAsync(event1);
		await repository.CreateAsync(event2);
		await repository.CreateAsync(event3);

		// Act
		var inRange = await repository.GetByDateRangeAsync(date1, date2);

		// Assert
		Assert.Equal(2, inRange.Count());
	}

	[Fact]
	public async Task EventRepository_UpdateStoredParticipationRateAsync_UpdatesRate()
	{
		// Arrange
		var options = CreateInMemoryOptions();
		using var context = new StageFrightContext(options);
		var repository = new EventRepository(context);

		var ev = new Event { Date = DateTime.Now, EventType = "Concert", StoredParticipationRate = 0 };
		await repository.CreateAsync(ev);

		// Act
		await repository.UpdateStoredParticipationRateAsync(ev.Id, 75.0m);
		var updated = await repository.GetByIdAsync(ev.Id);

		// Assert
		Assert.NotNull(updated);
		Assert.Equal(75.0m, updated.StoredParticipationRate);
	}

	#endregion

	#region Attendance Repository Tests

	[Fact]
	public async Task AttendanceRepository_RecordAsync_CreatesAttendanceWithPaidStatus()
	{
		// Arrange
		var options = CreateInMemoryOptions();
		using var context = new StageFrightContext(options);
		var repository = new AttendanceRepository(context);

		var memberId = Guid.NewGuid();
		var rehearsalId = Guid.NewGuid();

		// Act
		await repository.RecordAsync(rehearsalId, memberId, "Paid");
		var attendance = await repository.GetByRehearsalAsync(rehearsalId);

		// Assert
		Assert.Single(attendance);
		Assert.Equal("Paid", attendance.First().PaidStatus);
	}

	[Fact]
	public async Task AttendanceRepository_RecordAsync_DefaultsPaidStatusToPaid()
	{
		// Arrange
		var options = CreateInMemoryOptions();
		using var context = new StageFrightContext(options);
		var repository = new AttendanceRepository(context);

		var memberId = Guid.NewGuid();
		var rehearsalId = Guid.NewGuid();

		// Act
		await repository.RecordAsync(rehearsalId, memberId);
		var attendance = await repository.GetByRehearsalAsync(rehearsalId);

		// Assert
		Assert.Single(attendance);
		Assert.Equal("Paid", attendance.First().PaidStatus);
	}

	[Fact]
	public async Task AttendanceRepository_GetAttendanceRateAsync_CalculatesCorrectly()
	{
		// Arrange
		var options = CreateInMemoryOptions();
		using var context = new StageFrightContext(options);
		var repository = new AttendanceRepository(context);
		var memberId = Guid.NewGuid();

		var rehearsalIds = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
		var fromDate = new DateTime(2024, 6, 1);
		var toDate = new DateTime(2024, 6, 30);

		// Create rehearsals in the date range
		foreach (var id in rehearsalIds)
		{
			context.Rehearsals.Add(new Rehearsal { Id = id, Date = new DateTime(2024, 6, 15), Time = TimeSpan.Parse("19:00") });
		}
		await context.SaveChangesAsync();

		// Record attendance for 2 out of 3 rehearsals
		await repository.RecordAsync(rehearsalIds[0], memberId);
		await repository.RecordAsync(rehearsalIds[1], memberId);

		// Act
		var rate = await repository.GetAttendanceRateAsync(memberId, fromDate, toDate);

		// Assert
		Assert.Equal(66.66666666666666m, rate, 2);
	}

	#endregion

	#region Participation Repository Tests

	[Fact]
	public async Task ParticipationRepository_RecordAsync_CreatesParticipation()
	{
		// Arrange
		var options = CreateInMemoryOptions();
		using var context = new StageFrightContext(options);
		var repository = new ParticipationRepository(context);

		var memberId = Guid.NewGuid();
		var eventId = Guid.NewGuid();

		// Act
		await repository.RecordAsync(eventId, memberId);
		var participations = await repository.GetByEventAsync(eventId);

		// Assert
		Assert.Single(participations);
	}

	[Fact]
	public async Task ParticipationRepository_GetParticipationRateAsync_CalculatesCorrectly()
	{
		// Arrange
		var options = CreateInMemoryOptions();
		using var context = new StageFrightContext(options);
		var repository = new ParticipationRepository(context);
		var memberId = Guid.NewGuid();

		var eventIds = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
		var fromDate = new DateTime(2024, 6, 1);
		var toDate = new DateTime(2024, 6, 30);

		// Create events in the date range
		foreach (var id in eventIds)
		{
			context.Events.Add(new Event { Id = id, Date = new DateTime(2024, 6, 15), EventType = "Concert" });
		}
		await context.SaveChangesAsync();

		// Record participation for 3 out of 4 events
		await repository.RecordAsync(eventIds[0], memberId);
		await repository.RecordAsync(eventIds[1], memberId);
		await repository.RecordAsync(eventIds[2], memberId);

		// Act
		var rate = await repository.GetParticipationRateAsync(memberId, fromDate, toDate);

		// Assert
		Assert.Equal(75, rate);
	}

	#endregion

	#region Category Repository Tests

	[Fact]
	public async Task CategoryRepository_GetIncomeCategoriesAsync_ReturnsOnlyIncome()
	{
		// Arrange
		var options = CreateInMemoryOptions();
		using var context = new StageFrightContext(options);
		var repository = new CategoryRepository(context);

		var income = new Category { Name = "Membership", Type = "Income", SortOrder = 1, GlAccount = "1000" };
		var expense = new Category { Name = "Equipment", Type = "Expense", SortOrder = 1, GlAccount = "2000" };

		await repository.CreateAsync(income);
		await repository.CreateAsync(expense);

		// Act
		var incomeCategories = await repository.GetIncomeCategoriesAsync();

		// Assert
		Assert.Single(incomeCategories);
		Assert.Equal("Membership", incomeCategories.First().Name);
	}

	[Fact]
	public async Task CategoryRepository_ArchiveAsync_MarksAsArchived()
	{
		// Arrange
		var options = CreateInMemoryOptions();
		using var context = new StageFrightContext(options);
		var repository = new CategoryRepository(context);

		var category = new Category { Name = "TestCategory", Type = "Income", SortOrder = 1, GlAccount = "1000" };
		await repository.CreateAsync(category);

		// Act
		await repository.ArchiveAsync(category.Id);
		var archived = await repository.GetByIdAsync(category.Id);

		// Assert
		Assert.True(archived.IsArchived);
	}

	#endregion

	#region Fee Repository Tests

	[Fact]
	public async Task FeeRepository_CreateAsync_PersistsFee()
	{
		// Arrange
		var options = CreateInMemoryOptions();
		using var context = new StageFrightContext(options);
		var repository = new FeeRepository(context);

		var memberId = Guid.NewGuid();
		var fee = new Fee { MemberId = memberId, FeeType = "Annual", Amount = 150, FeeDate = DateTime.Now, DueDate = DateTime.Now.AddMonths(1) };

		// Act
		await repository.CreateAsync(fee);
		var retrieved = await repository.GetByIdAsync(fee.Id);

		// Assert
		Assert.NotNull(retrieved);
		Assert.Equal(150, retrieved.Amount);
	}

	[Fact]
	public async Task FeeRepository_UpdateAsync_ThrowsException()
	{
		// Arrange
		var options = CreateInMemoryOptions();
		using var context = new StageFrightContext(options);
		var repository = new FeeRepository(context);

		var memberId = Guid.NewGuid();
		var fee = new Fee { MemberId = memberId, FeeType = "Annual", Amount = 150, FeeDate = DateTime.Now, DueDate = DateTime.Now.AddMonths(1) };
		await repository.CreateAsync(fee);

		// Act & Assert
		await Assert.ThrowsAsync<InvalidOperationException>(() => repository.UpdateAsync(fee));
	}

	[Fact]
	public async Task FeeRepository_GetByYearAsync_FiltersCorrectly()
	{
		// Arrange
		var options = CreateInMemoryOptions();
		using var context = new StageFrightContext(options);
		var repository = new FeeRepository(context);

		var memberId = Guid.NewGuid();
		var fee2024 = new Fee { MemberId = memberId, FeeType = "Annual", Amount = 150, FeeDate = new DateTime(2024, 1, 1), DueDate = DateTime.Now.AddMonths(1) };
		var fee2025 = new Fee { MemberId = memberId, FeeType = "Annual", Amount = 150, FeeDate = new DateTime(2025, 1, 1), DueDate = DateTime.Now.AddMonths(1) };

		await repository.CreateAsync(fee2024);
		await repository.CreateAsync(fee2025);

		// Act
		var fees2024 = await repository.GetByYearAsync(2024);

		// Assert
		Assert.Single(fees2024);
	}

	#endregion

	#region Payment Repository Tests

	[Fact]
	public async Task PaymentRepository_CreateAsync_PersistsPayment()
	{
		// Arrange
		var options = CreateInMemoryOptions();
		using var context = new StageFrightContext(options);
		var repository = new PaymentRepository(context);

		var memberId = Guid.NewGuid();
		var payment = new Payment { MemberId = memberId, Date = DateTime.Now, Amount = 100, Category = "Annual", PaymentMethod = "Cash" };

		// Act
		await repository.CreateAsync(payment);
		var retrieved = await repository.GetByIdAsync(payment.Id);

		// Assert
		Assert.NotNull(retrieved);
		Assert.Equal(100, retrieved.Amount);
	}

	[Fact]
	public async Task PaymentRepository_UpdateNotesAsync_UpdatesOnlyNotes()
	{
		// Arrange
		var options = CreateInMemoryOptions();
		using var context = new StageFrightContext(options);
		var repository = new PaymentRepository(context);

		var memberId = Guid.NewGuid();
		var payment = new Payment { MemberId = memberId, Date = DateTime.Now, Amount = 100, Category = "Annual", PaymentMethod = "Cash", Notes = "Original" };

		await repository.CreateAsync(payment);

		// Act
		await repository.UpdateNotesAsync(payment.Id, "Updated notes");
		var updated = await repository.GetByIdAsync(payment.Id);

		// Assert
		Assert.Equal("Updated notes", updated.Notes);
		Assert.Equal(100, updated.Amount); // Amount unchanged
	}

	#endregion

	#region Transaction Repository Tests

	[Fact]
	public async Task TransactionRepository_UpdateAsync_ThrowsException()
	{
		// Arrange
		var options = CreateInMemoryOptions();
		using var context = new StageFrightContext(options);
		var repository = new TransactionRepository(context);

		var transaction = new Transaction { Date = DateTime.Now, Category = "Income", DebitAmount = 100, CreditAmount = 0 };
		await repository.CreateAsync(transaction);

		// Act & Assert
		await Assert.ThrowsAsync<InvalidOperationException>(() => repository.UpdateAsync(transaction));
	}

	[Fact]
	public async Task TransactionRepository_CreatePairAsync_CreatesBothDebitAndCredit()
	{
		// Arrange
		var options = CreateInMemoryOptions();
		using var context = new StageFrightContext(options);
		var repository = new TransactionRepository(context);

		var debit = new Transaction { Date = DateTime.Now, Category = "Income", DebitAmount = 100, CreditAmount = 0 };
		var credit = new Transaction { Date = DateTime.Now, Category = "Income", DebitAmount = 0, CreditAmount = 100 };

		// Act
		await repository.CreatePairAsync(debit, credit);
		var retrieved = await repository.GetByIdAsync(debit.Id);

		// Assert
		Assert.NotNull(retrieved);
		Assert.Equal(100, retrieved.DebitAmount);
	}

	[Fact]
	public async Task TransactionRepository_ValidateGLBalanceAsync_ReturnsTrue()
	{
		// Arrange
		var options = CreateInMemoryOptions();
		using var context = new StageFrightContext(options);
		var repository = new TransactionRepository(context);

		var debit = new Transaction { Date = DateTime.Now, Category = "Income", DebitAmount = 100, CreditAmount = 0 };
		var credit = new Transaction { Date = DateTime.Now, Category = "Income", DebitAmount = 0, CreditAmount = 100 };

		await repository.CreatePairAsync(debit, credit);

		// Act
		var balanced = await repository.ValidateGLBalanceAsync();

		// Assert
		Assert.True(balanced);
	}

	#endregion

	#region Committee Membership Repository Tests

	[Fact]
	public async Task CommitteeMembershipRepository_RecordAsync_CreatesRecord()
	{
		// Arrange
		var options = CreateInMemoryOptions();
		using var context = new StageFrightContext(options);
		var repository = new CommitteeMembershipRepository(context);

		var memberId = Guid.NewGuid();

		// Act
		await repository.RecordAsync(memberId, 2024, "President");
		var memberships = await repository.GetByMemberAsync(memberId);

		// Assert
		Assert.Single(memberships);
		Assert.Equal("President", memberships.First().Position);
	}

	[Fact]
	public async Task CommitteeMembershipRepository_GetByYearAsync_FiltersCorrectly()
	{
		// Arrange
		var options = CreateInMemoryOptions();
		using var context = new StageFrightContext(options);
		var repository = new CommitteeMembershipRepository(context);

		var memberId1 = Guid.NewGuid();
		var memberId2 = Guid.NewGuid();

		await repository.RecordAsync(memberId1, 2024, "President");
		await repository.RecordAsync(memberId2, 2024, "Treasurer");
		await repository.RecordAsync(memberId1, 2025, "Secretary");

		// Act
		var members2024 = await repository.GetByYearAsync(2024);

		// Assert
		Assert.Equal(2, members2024.Count());
	}

	[Fact]
	public async Task CommitteeMembershipRepository_ClearYearAsync_SoftDeletesYear()
	{
		// Arrange
		var options = CreateInMemoryOptions();
		using var context = new StageFrightContext(options);
		var repository = new CommitteeMembershipRepository(context);

		var memberId = Guid.NewGuid();
		await repository.RecordAsync(memberId, 2024, "President");

		// Act
		await repository.ClearYearAsync(2024);
		var remaining = await repository.GetByYearAsync(2024);

		// Assert
		Assert.Empty(remaining);
	}

	#endregion

	#region Settings Repository Tests

	[Fact]
	public async Task SettingsRepository_GetSettingsAsync_ReturnsSingleton()
	{
		// Arrange
		var options = CreateInMemoryOptions();
		using var context = new StageFrightContext(options);
		var repository = new SettingsRepository(context);

		var settings = new Settings { OrganizationName = "Test Org", AnnualFee = 150 };
		await repository.CreateAsync(settings);

		// Act
		var retrieved = await repository.GetSettingsAsync();

		// Assert
		Assert.NotNull(retrieved);
		Assert.Equal("Test Org", retrieved.OrganizationName);
	}

	[Fact]
	public async Task SettingsRepository_UpdateSettingsAsync_UpdatesExisting()
	{
		// Arrange
		var options = CreateInMemoryOptions();
		using var context = new StageFrightContext(options);
		var repository = new SettingsRepository(context);

		var settings = new Settings { OrganizationName = "Original", AnnualFee = 150 };
		await repository.CreateAsync(settings);

		// Act
		var updated = new Settings { OrganizationName = "Updated", AnnualFee = 200 };
		await repository.UpdateSettingsAsync(updated);
		var retrieved = await repository.GetSettingsAsync();

		// Assert
		Assert.Equal("Updated", retrieved.OrganizationName);
		Assert.Equal(200, retrieved.AnnualFee);
	}

	#endregion

	#region Audit Trail Repository Tests

	[Fact]
	public async Task AuditTrailRepository_LogAsync_CreatesAuditEntry()
	{
		// Arrange
		var options = CreateInMemoryOptions();
		using var context = new StageFrightContext(options);
		var repository = new AuditTrailRepository(context);

		var entityId = Guid.NewGuid();

		// Act
		await repository.LogAsync("Member", entityId, "Create", "user123", null, "New Member");
		var entries = await repository.GetByEntityAsync("Member", entityId);

		// Assert
		Assert.Single(entries);
		Assert.Equal("Create", entries.First().Action);
	}

	[Fact]
	public async Task AuditTrailRepository_GetByEntityAsync_ReturnsChronological()
	{
		// Arrange
		var options = CreateInMemoryOptions();
		using var context = new StageFrightContext(options);
		var repository = new AuditTrailRepository(context);

		var entityId = Guid.NewGuid();

		await repository.LogAsync("Member", entityId, "Create");
		await Task.Delay(10);
		await repository.LogAsync("Member", entityId, "Update");

		// Act
		var entries = await repository.GetByEntityAsync("Member", entityId);

		// Assert
		Assert.Equal(2, entries.Count());
		Assert.Equal("Update", entries.First().Action); // Most recent first
	}

	[Fact]
	public async Task AuditTrailRepository_PurgeExpiredAsync_RemovesOldEntries()
	{
		// Arrange
		var options = CreateInMemoryOptions();
		using var context = new StageFrightContext(options);
		var repository = new AuditTrailRepository(context);

		var entityId = Guid.NewGuid();
		var oldEntry = new AuditTrail
		{
			EntityType = "Member",
			EntityId = entityId,
			Action = "Create",
			Timestamp = DateTime.UtcNow.AddMonths(-14) // Older than 13 months
		};

		context.AuditTrails.Add(oldEntry);
		await context.SaveChangesAsync();

		// Act
		await repository.PurgeExpiredAsync();
		var entries = await repository.GetByEntityAsync("Member", entityId);

		// Assert
		Assert.Empty(entries);
	}

	#endregion
}
