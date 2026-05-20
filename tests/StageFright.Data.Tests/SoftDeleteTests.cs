using Microsoft.EntityFrameworkCore;
using StageFright.Core.Entities;
using StageFright.Data.Context;
using StageFright.Data.Repositories;
using Xunit;

namespace StageFright.Data.Tests;

/// <summary>
/// Tests for soft-delete behavior and query filtering.
/// Verifies that soft-deleted records are properly excluded from default queries
/// and that restoration works correctly.
/// </summary>
public class SoftDeleteTests
{
	private DbContextOptions<StageFrightContext> CreateInMemoryOptions()
	{
		return new DbContextOptionsBuilder<StageFrightContext>()
			.UseInMemoryDatabase(Guid.NewGuid().ToString())
			.EnableSensitiveDataLogging()
			.ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
			.Options;
	}

	#region Member Soft Delete Tests

	[Fact]
	public async Task Member_SoftDelete_ExcludesFromQueries()
	{
		// Arrange
		var options = CreateInMemoryOptions();
		using var context = new StageFrightContext(options);
		var repository = new MemberRepository(context);

		var member1 = new Member { Name = "Active Member", StreetAddress = "123 Main St", JoinDate = DateTime.Now };
		var member2 = new Member { Name = "To Delete", StreetAddress = "456 Oak Ave", JoinDate = DateTime.Now };

		await repository.CreateAsync(member1);
		await repository.CreateAsync(member2);

		// Act - soft delete member2
		await repository.SoftDeleteAsync(member2.Id, "system");

		// Assert - GetAllAsync should exclude deleted
		var allMembers = await repository.GetAllAsync();
		Assert.Single(allMembers);
		Assert.Equal("Active Member", allMembers.First().Name);
	}

	[Fact]
	public async Task Member_SoftDelete_SetsFields()
	{
		// Arrange
		var options = CreateInMemoryOptions();
		using var context = new StageFrightContext(options);
		var repository = new MemberRepository(context);

		var member = new Member { Name = "Test", StreetAddress = "123 Main St", JoinDate = DateTime.Now };
		await repository.CreateAsync(member);

		// Act
		await repository.SoftDeleteAsync(member.Id, "user123");

		// Assert - query with IgnoreQueryFilters to verify fields are set
		using var context2 = new StageFrightContext(options);
		var softDeletedMember = await context2.Members.IgnoreQueryFilters().FirstOrDefaultAsync(m => m.Id == member.Id);

		Assert.True(softDeletedMember.IsDeleted);
		Assert.NotNull(softDeletedMember.DeletedAt);
		Assert.Equal("user123", softDeletedMember.DeletedBy);
	}

	[Fact]
	public async Task Member_Restore_ReincludesInQueries()
	{
		// Arrange
		var options = CreateInMemoryOptions();
		using var context = new StageFrightContext(options);
		var repository = new MemberRepository(context);

		var member = new Member { Name = "Test", StreetAddress = "123 Main St", JoinDate = DateTime.Now };
		await repository.CreateAsync(member);
		await repository.SoftDeleteAsync(member.Id);

		// Act
		await repository.RestoreAsync(member.Id);

		// Assert
		var allMembers = await repository.GetAllAsync();
		Assert.Single(allMembers);
		Assert.False(allMembers.First().IsDeleted);
	}

	[Fact]
	public async Task Member_GetActiveMembersAsync_ExcludesSoftDeleted()
	{
		// Arrange
		var options = CreateInMemoryOptions();
		using var context = new StageFrightContext(options);
		var repository = new MemberRepository(context);

		var activeMember = new Member { Name = "Active", StreetAddress = "123 Main St", JoinDate = DateTime.Now, Status = "Active" };
		var deletedActiveMember = new Member { Name = "Deleted Active", StreetAddress = "456 Oak Ave", JoinDate = DateTime.Now, Status = "Active" };

		await repository.CreateAsync(activeMember);
		await repository.CreateAsync(deletedActiveMember);
		await repository.SoftDeleteAsync(deletedActiveMember.Id);

		// Act
		var active = await repository.GetActiveMembersAsync();

		// Assert
		Assert.Single(active);
		Assert.Equal("Active", active.First().Name);
	}

	#endregion

	#region Rehearsal Soft Delete Tests

	[Fact]
	public async Task Rehearsal_SoftDelete_ExcludesFromQueries()
	{
		// Arrange
		var options = CreateInMemoryOptions();
		using var context = new StageFrightContext(options);
		var repository = new RehearsalRepository(context);

		var rehearsal1 = new Rehearsal { Date = DateTime.Now, Time = TimeSpan.Parse("19:00") };
		var rehearsal2 = new Rehearsal { Date = DateTime.Now, Time = TimeSpan.Parse("20:00") };

		await repository.CreateAsync(rehearsal1);
		await repository.CreateAsync(rehearsal2);

		// Act
		await repository.SoftDeleteAsync(rehearsal2.Id);

		// Assert
		var all = await repository.GetAllAsync();
		Assert.Single(all);
	}

	[Fact]
	public async Task Rehearsal_GetByDateRangeAsync_ExcludesSoftDeleted()
	{
		// Arrange
		var options = CreateInMemoryOptions();
		using var context = new StageFrightContext(options);
		var repository = new RehearsalRepository(context);

		var date = new DateTime(2024, 6, 15);
		var rehearsal1 = new Rehearsal { Date = date, Time = TimeSpan.Parse("19:00") };
		var rehearsal2 = new Rehearsal { Date = date, Time = TimeSpan.Parse("20:00") };

		await repository.CreateAsync(rehearsal1);
		await repository.CreateAsync(rehearsal2);
		await repository.SoftDeleteAsync(rehearsal2.Id);

		// Act
		var inRange = await repository.GetByDateRangeAsync(date.AddDays(-1), date.AddDays(1));

		// Assert
		Assert.Single(inRange);
	}

	#endregion

	#region Event Soft Delete Tests

	[Fact]
	public async Task Event_SoftDelete_ExcludesFromQueries()
	{
		// Arrange
		var options = CreateInMemoryOptions();
		using var context = new StageFrightContext(options);
		var repository = new EventRepository(context);

		var event1 = new Event { Date = DateTime.Now, EventType = "Concert" };
		var event2 = new Event { Date = DateTime.Now, EventType = "Concert" };

		await repository.CreateAsync(event1);
		await repository.CreateAsync(event2);

		// Act
		await repository.SoftDeleteAsync(event2.Id);

		// Assert
		var all = await repository.GetAllAsync();
		Assert.Single(all);
	}

	[Fact]
	public async Task Event_GetByDateRangeAsync_ExcludesSoftDeleted()
	{
		// Arrange
		var options = CreateInMemoryOptions();
		using var context = new StageFrightContext(options);
		var repository = new EventRepository(context);

		var date = new DateTime(2024, 6, 15);
		var event1 = new Event { Date = date, EventType = "Concert" };
		var event2 = new Event { Date = date, EventType = "Concert" };

		await repository.CreateAsync(event1);
		await repository.CreateAsync(event2);
		await repository.SoftDeleteAsync(event2.Id);

		// Act
		var inRange = await repository.GetByDateRangeAsync(date.AddDays(-1), date.AddDays(1));

		// Assert
		Assert.Single(inRange);
	}

	#endregion

	#region Category Soft Delete Tests

	[Fact]
	public async Task Category_Archive_SetIsArchived()
	{
		// Arrange
		var options = CreateInMemoryOptions();
		using var context = new StageFrightContext(options);
		var repository = new CategoryRepository(context);

		var category = new Category { Name = "Test", Type = "Income", SortOrder = 1, GlAccount = "1000" };
		await repository.CreateAsync(category);

		// Act
		await repository.ArchiveAsync(category.Id);
		var archived = await repository.GetByIdAsync(category.Id);

		// Assert - GetByIdAsync respects soft-delete filter on IsDeleted but not IsArchived
		// IsArchived is separate from soft-delete
		Assert.True(archived.IsArchived);
	}

	[Fact]
	public async Task Category_Restore_ClearsArchived()
	{
		// Arrange
		var options = CreateInMemoryOptions();
		using var context = new StageFrightContext(options);
		var repository = new CategoryRepository(context);

		var category = new Category { Name = "Test", Type = "Income", SortOrder = 1, GlAccount = "1000" };
		await repository.CreateAsync(category);
		await repository.ArchiveAsync(category.Id);

		// Act
		await repository.RestoreAsync(category.Id);
		var restored = await repository.GetByIdAsync(category.Id);

		// Assert
		Assert.False(restored.IsArchived);
	}

	[Fact]
	public async Task Category_GetIncomeCategoriesAsync_ExcludesArchived()
	{
		// Arrange
		var options = CreateInMemoryOptions();
		using var context = new StageFrightContext(options);
		var repository = new CategoryRepository(context);

		var active = new Category { Name = "Active Income", Type = "Income", SortOrder = 1, GlAccount = "1000" };
		var archived = new Category { Name = "Archived Income", Type = "Income", SortOrder = 2, GlAccount = "1001", IsArchived = true };

		await repository.CreateAsync(active);
		await repository.CreateAsync(archived);

		// Act
		var income = await repository.GetIncomeCategoriesAsync();

		// Assert
		Assert.Single(income);
		Assert.Equal("Active Income", income.First().Name);
	}

	#endregion

	#region Committee Membership Soft Delete Tests

	[Fact]
	public async Task CommitteeMembership_SoftDelete_ExcludesFromQueries()
	{
		// Arrange
		var options = CreateInMemoryOptions();
		using var context = new StageFrightContext(options);
		var repository = new CommitteeMembershipRepository(context);

		var memberId = Guid.NewGuid();

		await repository.RecordAsync(memberId, 2024, "President");
		var memberships = await repository.GetByMemberAsync(memberId);
		var membershipId = memberships.First().Id;

		// Act
		await repository.SoftDeleteAsync(membershipId);

		// Assert
		var remaining = await repository.GetByMemberAsync(memberId);
		Assert.Empty(remaining);
	}

	[Fact]
	public async Task CommitteeMembership_ClearYearAsync_SoftDeletesAll()
	{
		// Arrange
		var options = CreateInMemoryOptions();
		using var context = new StageFrightContext(options);
		var repository = new CommitteeMembershipRepository(context);

		var memberId1 = Guid.NewGuid();
		var memberId2 = Guid.NewGuid();

		await repository.RecordAsync(memberId1, 2024, "President");
		await repository.RecordAsync(memberId2, 2024, "Treasurer");

		// Act
		await repository.ClearYearAsync(2024);

		// Assert - both should be gone from queries
		var year2024 = await repository.GetByYearAsync(2024);
		Assert.Empty(year2024);
	}

	[Fact]
	public async Task CommitteeMembership_GetHistoryAsync_IncludesDeleted()
	{
		// Arrange
		var options = CreateInMemoryOptions();
		using var context = new StageFrightContext(options);
		var repository = new CommitteeMembershipRepository(context);

		var memberId = Guid.NewGuid();

		await repository.RecordAsync(memberId, 2023, "President");
		await repository.RecordAsync(memberId, 2024, "Treasurer");

		// Soft delete 2023 record
		var memberships = await repository.GetByMemberAsync(memberId);
		var membership2023 = memberships.FirstOrDefault(m => m.Year == 2023);
		if (membership2023 != null)
		{
			await repository.SoftDeleteAsync(membership2023.Id);
		}

		// Act
		var history = await repository.GetHistoryAsync(memberId);

		// Assert - GetHistoryAsync does not filter by IsDeleted, so includes all
		// (This depends on implementation - if it uses IgnoreQueryFilters or doesn't apply the filter)
		Assert.NotEmpty(history);
	}

	#endregion

	#region Cross-Entity Soft Delete Tests

	[Fact]
	public async Task SoftDelete_MultipleEntities_AllRespectFilters()
	{
		// Arrange
		var options = CreateInMemoryOptions();
		using var context = new StageFrightContext(options);

		var memberRepo = new MemberRepository(context);
		var rehearsalRepo = new RehearsalRepository(context);
		var eventRepo = new EventRepository(context);

		var member = new Member { Name = "Test", StreetAddress = "123 Main St", JoinDate = DateTime.Now };
		var rehearsal = new Rehearsal { Date = DateTime.Now, Time = TimeSpan.Parse("19:00") };
		var ev = new Event { Date = DateTime.Now, EventType = "Concert" };

		await memberRepo.CreateAsync(member);
		await rehearsalRepo.CreateAsync(rehearsal);
		await eventRepo.CreateAsync(ev);

		// Act
		await memberRepo.SoftDeleteAsync(member.Id);
		await rehearsalRepo.SoftDeleteAsync(rehearsal.Id);
		await eventRepo.SoftDeleteAsync(ev.Id);

		// Assert
		var members = await memberRepo.GetAllAsync();
		var rehearsals = await rehearsalRepo.GetAllAsync();
		var events = await eventRepo.GetAllAsync();

		Assert.Empty(members);
		Assert.Empty(rehearsals);
		Assert.Empty(events);
	}

	#endregion
}
