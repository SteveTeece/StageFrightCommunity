using Microsoft.EntityFrameworkCore;
using StageFright.Core.Entities;
using StageFright.Data.Context;
using StageFright.Data.Repositories;
using Xunit;

namespace StageFright.Data.Tests;

/// <summary>
/// Tests for historical member queries and effective date scenarios.
/// Verifies that member status filters work correctly with effective dating for
/// reactivation, inactivation, and archive scenarios.
/// </summary>
public class HistoricalMemberTests
{
	private DbContextOptions<StageFrightContext> CreateInMemoryOptions()
	{
		return new DbContextOptionsBuilder<StageFrightContext>()
			.UseInMemoryDatabase(Guid.NewGuid().ToString())
			.EnableSensitiveDataLogging()
			.ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
			.Options;
	}

	#region Basic Effective Date Tests

	[Fact]
	public async Task GetHistoricalActiveMembersAsync_BeforeJoinDate_ExcludesMember()
	{
		// Arrange
		var options = CreateInMemoryOptions();
		using var context = new StageFrightContext(options);
		var repository = new MemberRepository(context);

		var joinDate = new DateTime(2024, 6, 1);
		var member = new Member
		{
			Name = "Test",
			StreetAddress = "123 Main St",
			JoinDate = joinDate,
			Status = "Active"
		};

		await repository.CreateAsync(member);

		// Act - query as-of date before join
		var historicalBefore = await repository.GetHistoricalActiveMembersAsync(new DateTime(2024, 5, 1));

		// Assert
		Assert.Empty(historicalBefore);
	}

	[Fact]
	public async Task GetHistoricalActiveMembersAsync_OnJoinDate_IncludesMember()
	{
		// Arrange
		var options = CreateInMemoryOptions();
		using var context = new StageFrightContext(options);
		var repository = new MemberRepository(context);

		var joinDate = new DateTime(2024, 6, 1);
		var member = new Member
		{
			Name = "Test",
			StreetAddress = "123 Main St",
			JoinDate = joinDate,
			Status = "Active"
		};

		await repository.CreateAsync(member);

		// Act
		var historicalOnDate = await repository.GetHistoricalActiveMembersAsync(joinDate);

		// Assert
		Assert.Single(historicalOnDate);
	}

	[Fact]
	public async Task GetHistoricalActiveMembersAsync_AfterJoinDate_IncludesMember()
	{
		// Arrange
		var options = CreateInMemoryOptions();
		using var context = new StageFrightContext(options);
		var repository = new MemberRepository(context);

		var joinDate = new DateTime(2024, 6, 1);
		var member = new Member
		{
			Name = "Test",
			StreetAddress = "123 Main St",
			JoinDate = joinDate,
			Status = "Active"
		};

		await repository.CreateAsync(member);

		// Act
		var historicalAfter = await repository.GetHistoricalActiveMembersAsync(new DateTime(2024, 12, 31));

		// Assert
		Assert.Single(historicalAfter);
	}

	#endregion

	#region Inactivation Scenario Tests

	[Fact]
	public async Task GetHistoricalActiveMembersAsync_BeforeInactivation_IncludesMember()
	{
		// Arrange
		var options = CreateInMemoryOptions();
		using var context = new StageFrightContext(options);
		var repository = new MemberRepository(context);

		var joinDate = new DateTime(2024, 1, 1);
		var inactivateDate = new DateTime(2024, 9, 1);

		var member = new Member
		{
			Name = "Inactivated",
			StreetAddress = "123 Main St",
			JoinDate = joinDate,
			InactivateDate = inactivateDate,
			Status = "Inactive"
		};

		await repository.CreateAsync(member);

		// Act - query before inactivation date
		var before = await repository.GetHistoricalActiveMembersAsync(new DateTime(2024, 8, 15));

		// Assert
		Assert.Single(before);
		Assert.Equal("Inactivated", before.First().Name);
	}

	[Fact]
	public async Task GetHistoricalActiveMembersAsync_OnInactivationDate_ExcludesMember()
	{
		// Arrange
		var options = CreateInMemoryOptions();
		using var context = new StageFrightContext(options);
		var repository = new MemberRepository(context);

		var joinDate = new DateTime(2024, 1, 1);
		var inactivateDate = new DateTime(2024, 9, 1);

		var member = new Member
		{
			Name = "Inactivated",
			StreetAddress = "123 Main St",
			JoinDate = joinDate,
			InactivateDate = inactivateDate,
			Status = "Inactive"
		};

		await repository.CreateAsync(member);

		// Act - query on inactivation date (not-inclusive)
		var onDate = await repository.GetHistoricalActiveMembersAsync(inactivateDate);

		// Assert
		Assert.Empty(onDate);
	}

	[Fact]
	public async Task GetHistoricalActiveMembersAsync_AfterInactivation_ExcludesMember()
	{
		// Arrange
		var options = CreateInMemoryOptions();
		using var context = new StageFrightContext(options);
		var repository = new MemberRepository(context);

		var joinDate = new DateTime(2024, 1, 1);
		var inactivateDate = new DateTime(2024, 9, 1);

		var member = new Member
		{
			Name = "Inactivated",
			StreetAddress = "123 Main St",
			JoinDate = joinDate,
			InactivateDate = inactivateDate,
			Status = "Inactive"
		};

		await repository.CreateAsync(member);

		// Act - query after inactivation
		var after = await repository.GetHistoricalActiveMembersAsync(new DateTime(2024, 10, 1));

		// Assert
		Assert.Empty(after);
	}

	#endregion

	#region Reactivation Scenario Tests

	[Fact]
	public async Task GetHistoricalActiveMembersAsync_BothInactivateAndActivateDates_HandlesReactivation()
	{
		// Arrange
		var options = CreateInMemoryOptions();
		using var context = new StageFrightContext(options);
		var repository = new MemberRepository(context);

		var joinDate = new DateTime(2024, 1, 1);
		var inactivateDate = new DateTime(2024, 6, 1);
		var reactivateDate = new DateTime(2024, 8, 1); // Note: This test assumes ActivateDate field exists for reactivation

		var member = new Member
		{
			Name = "Reactivated",
			StreetAddress = "123 Main St",
			JoinDate = joinDate,
			InactivateDate = inactivateDate,
			ActivateDate = reactivateDate,
			Status = "Active"
		};

		await repository.CreateAsync(member);

		// Act
		var before = await repository.GetHistoricalActiveMembersAsync(new DateTime(2024, 5, 1));
		var during = await repository.GetHistoricalActiveMembersAsync(new DateTime(2024, 7, 1));
		var after = await repository.GetHistoricalActiveMembersAsync(new DateTime(2024, 9, 1));

		// Assert
		Assert.Single(before); // Active before inactivation
		Assert.Empty(during);  // Inactive between dates
		Assert.Single(after);  // Active after reactivation
	}

	#endregion

	#region Multiple Member Effective Date Tests

	[Fact]
	public async Task GetHistoricalActiveMembersAsync_MultipleMembersWithDifferentDates()
	{
		// Arrange
		var options = CreateInMemoryOptions();
		using var context = new StageFrightContext(options);
		var repository = new MemberRepository(context);

		var asOfDate = new DateTime(2024, 6, 15);

		// Member 1: Joined before, never inactive
		var member1 = new Member
		{
			Name = "Member1",
			StreetAddress = "123 Main St",
			JoinDate = new DateTime(2024, 1, 1),
			Status = "Active"
		};

		// Member 2: Joined before, became inactive before asOf date
		var member2 = new Member
		{
			Name = "Member2",
			StreetAddress = "456 Oak Ave",
			JoinDate = new DateTime(2024, 1, 1),
			InactivateDate = new DateTime(2024, 6, 1),
			Status = "Inactive"
		};

		// Member 3: Hasn't joined yet as of date
		var member3 = new Member
		{
			Name = "Member3",
			StreetAddress = "789 Pine Rd",
			JoinDate = new DateTime(2024, 7, 1),
			Status = "Active"
		};

		await repository.CreateAsync(member1);
		await repository.CreateAsync(member2);
		await repository.CreateAsync(member3);

		// Act
		var active = await repository.GetHistoricalActiveMembersAsync(asOfDate);

		// Assert
		Assert.Single(active);
		Assert.Equal("Member1", active.First().Name);
	}

	#endregion

	#region Status Filtering Tests

	[Fact]
	public async Task GetActiveMembersAsync_FiltersByStatusOnly()
	{
		// Arrange
		var options = CreateInMemoryOptions();
		using var context = new StageFrightContext(options);
		var repository = new MemberRepository(context);

		var activeMembers = new List<Member>
		{
			new() { Name = "Active1", StreetAddress = "123 Main St", JoinDate = DateTime.Now, Status = "Active" },
			new() { Name = "Active2", StreetAddress = "456 Oak Ave", JoinDate = DateTime.Now, Status = "Active" }
		};

		var inactiveMembers = new List<Member>
		{
			new() { Name = "Inactive1", StreetAddress = "789 Pine Rd", JoinDate = DateTime.Now, Status = "Inactive" },
			new() { Name = "Inactive2", StreetAddress = "321 Elm St", JoinDate = DateTime.Now, Status = "Inactive" }
		};

		foreach (var m in activeMembers.Concat(inactiveMembers))
		{
			await repository.CreateAsync(m);
		}

		// Act
		var active = await repository.GetActiveMembersAsync();
		var inactive = await repository.GetInactiveMembersAsync();

		// Assert
		Assert.Equal(2, active.Count());
		Assert.Equal(2, inactive.Count());
	}

	[Fact]
	public async Task GetInactiveMembersAsync_ExcludesdeletedInactiveMembers()
	{
		// Arrange
		var options = CreateInMemoryOptions();
		using var context = new StageFrightContext(options);
		var repository = new MemberRepository(context);

		var inactiveMember1 = new Member { Name = "Inactive1", StreetAddress = "123 Main St", JoinDate = DateTime.Now, Status = "Inactive" };
		var inactiveMember2 = new Member { Name = "Inactive2", StreetAddress = "456 Oak Ave", JoinDate = DateTime.Now, Status = "Inactive" };

		await repository.CreateAsync(inactiveMember1);
		await repository.CreateAsync(inactiveMember2);

		// Soft delete one
		await repository.SoftDeleteAsync(inactiveMember2.Id);

		// Act
		var inactive = await repository.GetInactiveMembersAsync();

		// Assert
		Assert.Single(inactive);
		Assert.Equal("Inactive1", inactive.First().Name);
	}

	#endregion

	#region Edge Case Tests

	[Fact]
	public async Task GetHistoricalActiveMembersAsync_WithNullInactivateDate_TreatsAsStillActive()
	{
		// Arrange
		var options = CreateInMemoryOptions();
		using var context = new StageFrightContext(options);
		var repository = new MemberRepository(context);

		var member = new Member
		{
			Name = "StillActive",
			StreetAddress = "123 Main St",
			JoinDate = new DateTime(2024, 1, 1),
			InactivateDate = null, // Never inactivated
			Status = "Active"
		};

		await repository.CreateAsync(member);

		// Act
		var historical = await repository.GetHistoricalActiveMembersAsync(new DateTime(2024, 12, 31));

		// Assert
		Assert.Single(historical);
	}

	[Fact]
	public async Task GetHistoricalActiveMembersAsync_LongTimespan_IncludesMultipleCandidates()
	{
		// Arrange
		var options = CreateInMemoryOptions();
		using var context = new StageFrightContext(options);
		var repository = new MemberRepository(context);

		for (int i = 0; i < 10; i++)
		{
			var joinDate = new DateTime(2020, 1, 1).AddMonths(i);
			var member = new Member
			{
				Name = $"Member{i}",
				StreetAddress = "123 Main St",
				JoinDate = joinDate,
				Status = "Active"
			};
			await repository.CreateAsync(member);
		}

		// Act
		var historical = await repository.GetHistoricalActiveMembersAsync(new DateTime(2024, 6, 1));

		// Assert
		Assert.Equal(10, historical.Count()); // All should be active by 2024
	}

	[Fact]
	public async Task GetHistoricalActiveMembersAsync_InactivateDateEqualsQueryDate_Excludes()
	{
		// Arrange
		var options = CreateInMemoryOptions();
		using var context = new StageFrightContext(options);
		var repository = new MemberRepository(context);

		var inactivateDate = new DateTime(2024, 6, 1);
		var member = new Member
		{
			Name = "Test",
			StreetAddress = "123 Main St",
			JoinDate = new DateTime(2024, 1, 1),
			InactivateDate = inactivateDate,
			Status = "Inactive"
		};

		await repository.CreateAsync(member);

		// Act - query on exact inactivation date
		var historical = await repository.GetHistoricalActiveMembersAsync(inactivateDate);

		// Assert
		Assert.Empty(historical);
	}

	#endregion

	#region Integration Tests

	[Fact]
	public async Task HistoricalQueries_CombinedWithSoftDelete_WorkCorrectly()
	{
		// Arrange
		var options = CreateInMemoryOptions();
		using var context = new StageFrightContext(options);
		var repository = new MemberRepository(context);

		var asOfDate = new DateTime(2024, 6, 15);

		var member1 = new Member { Name = "Active", StreetAddress = "123 Main St", JoinDate = new DateTime(2024, 1, 1), Status = "Active" };
		var member2 = new Member { Name = "Deleted", StreetAddress = "456 Oak Ave", JoinDate = new DateTime(2024, 1, 1), Status = "Active" };

		await repository.CreateAsync(member1);
		await repository.CreateAsync(member2);
		await repository.SoftDeleteAsync(member2.Id);

		// Act
		var historical = await repository.GetHistoricalActiveMembersAsync(asOfDate);

		// Assert - only non-deleted members returned
		Assert.Single(historical);
		Assert.Equal("Active", historical.First().Name);
	}

	[Fact]
	public async Task HistoricalQueries_MultipleStatusChanges_TracksCorrectly()
	{
		// Arrange
		var options = CreateInMemoryOptions();
		using var context = new StageFrightContext(options);
		var repository = new MemberRepository(context);

		var joinDate = new DateTime(2024, 1, 1);
		var inactivateDate = new DateTime(2024, 3, 1);
		var reactivateDate = new DateTime(2024, 5, 1);

		var member = new Member
		{
			Name = "MultiStatus",
			StreetAddress = "123 Main St",
			JoinDate = joinDate,
			InactivateDate = inactivateDate,
			ActivateDate = reactivateDate,
			Status = "Active"
		};

		await repository.CreateAsync(member);

		// Act & Assert
		var q1 = await repository.GetHistoricalActiveMembersAsync(new DateTime(2024, 2, 1)); // Between join and inactivate
		Assert.Single(q1);

		var q2 = await repository.GetHistoricalActiveMembersAsync(new DateTime(2024, 4, 1)); // Between inactivate and reactivate
		Assert.Empty(q2);

		var q3 = await repository.GetHistoricalActiveMembersAsync(new DateTime(2024, 6, 1)); // After reactivate
		Assert.Single(q3);
	}

	#endregion
}
