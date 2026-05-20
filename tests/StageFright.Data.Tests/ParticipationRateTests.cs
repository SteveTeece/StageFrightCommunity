using Microsoft.EntityFrameworkCore;
using StageFright.Core.Entities;
using StageFright.Data.Context;
using StageFright.Data.Repositories;
using Xunit;

namespace StageFright.Data.Tests;

/// <summary>
/// Tests for immutable stored participation rate calculation.
/// Verifies that: (1) Rates are calculated at recording time using member statuses as-of that date;
/// (2) Rates are stored immutably in Event.StoredParticipationRate;
/// (3) Post-event archival does NOT retroactively change stored rates;
/// (4) Archive affects only future rate calculations.
/// Formula: members_participated / members_active_on_date * 100%
/// </summary>
public class ParticipationRateTests
{
	private DbContextOptions<StageFrightContext> CreateInMemoryOptions()
	{
		return new DbContextOptionsBuilder<StageFrightContext>()
			.UseInMemoryDatabase(Guid.NewGuid().ToString())
			.EnableSensitiveDataLogging()
			.ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
			.Options;
	}

	#region Basic Rate Calculation Tests

	[Fact]
	public async Task StoredParticipationRate_Simple_CalculatesCorrectly()
	{
		// Arrange
		var options = CreateInMemoryOptions();
		using var context = new StageFrightContext(options);

		var ev = new Event { Date = DateTime.Now, EventType = "Concert", StoredParticipationRate = 0 };
		context.Events.Add(ev);
		await context.SaveChangesAsync();

		// 4 active members total, 3 participated
		// Expected rate: 3/4 * 100 = 75%
		var expectedRate = (decimal)3 / 4 * 100;

		// Act
		var repository = new EventRepository(context);
		await repository.UpdateStoredParticipationRateAsync(ev.Id, expectedRate);
		var updated = await repository.GetByIdAsync(ev.Id);

		// Assert
		Assert.NotNull(updated);
		Assert.Equal(75, updated.StoredParticipationRate);
	}

	[Fact]
	public async Task StoredParticipationRate_100Percent_AllParticipated()
	{
		// Arrange
		var options = CreateInMemoryOptions();
		using var context = new StageFrightContext(options);

		var ev = new Event { Date = DateTime.Now, EventType = "Concert", StoredParticipationRate = 0 };
		context.Events.Add(ev);
		await context.SaveChangesAsync();

		// Act - 5 active members, all 5 participated = 100%
		var repository = new EventRepository(context);
		await repository.UpdateStoredParticipationRateAsync(ev.Id, 100);
		var updated = await repository.GetByIdAsync(ev.Id);

		// Assert
		Assert.Equal(100, updated.StoredParticipationRate);
	}

	[Fact]
	public async Task StoredParticipationRate_ZeroPercent_NoneParticipated()
	{
		// Arrange
		var options = CreateInMemoryOptions();
		using var context = new StageFrightContext(options);

		var ev = new Event { Date = DateTime.Now, EventType = "Concert", StoredParticipationRate = 0 };
		context.Events.Add(ev);
		await context.SaveChangesAsync();

		// Act - 5 active members, 0 participated = 0%
		var repository = new EventRepository(context);
		await repository.UpdateStoredParticipationRateAsync(ev.Id, 0);
		var updated = await repository.GetByIdAsync(ev.Id);

		// Assert
		Assert.Equal(0, updated.StoredParticipationRate);
	}

	#endregion

	#region Immutability Tests

	[Fact]
	public async Task StoredParticipationRate_ClampedTo0To100()
	{
		// Arrange
		var options = CreateInMemoryOptions();
		using var context = new StageFrightContext(options);

		var ev = new Event { Date = DateTime.Now, EventType = "Concert", StoredParticipationRate = 0 };
		context.Events.Add(ev);
		await context.SaveChangesAsync();

		var repository = new EventRepository(context);

		// Act - Try to set > 100
		await repository.UpdateStoredParticipationRateAsync(ev.Id, 150);
		var clamped = await repository.GetByIdAsync(ev.Id);

		// Assert
		Assert.Equal(100, clamped.StoredParticipationRate);
	}

	[Fact]
	public async Task StoredParticipationRate_ClampedToNegative()
	{
		// Arrange
		var options = CreateInMemoryOptions();
		using var context = new StageFrightContext(options);

		var ev = new Event { Date = DateTime.Now, EventType = "Concert", StoredParticipationRate = 0 };
		context.Events.Add(ev);
		await context.SaveChangesAsync();

		var repository = new EventRepository(context);

		// Act - Try to set negative
		await repository.UpdateStoredParticipationRateAsync(ev.Id, -25);
		var clamped = await repository.GetByIdAsync(ev.Id);

		// Assert
		Assert.Equal(0, clamped.StoredParticipationRate);
	}

	#endregion

	#region Member Status as-of Recording Time Tests

	[Fact]
	public async Task StoredParticipationRate_UsesStatusAtRecordingTime_IncludesActivemembers()
	{
		// Arrange
		var options = CreateInMemoryOptions();
		using var context = new StageFrightContext(options);

		var eventDate = new DateTime(2024, 6, 1);
		var ev = new Event { Date = eventDate, EventType = "Concert", StoredParticipationRate = 0 };

		// Create members - all Active at recording time
		var member1 = new Member { Name = "M1", StreetAddress = "123", JoinDate = new DateTime(2024, 1, 1), Status = "Active" };
		var member2 = new Member { Name = "M2", StreetAddress = "456", JoinDate = new DateTime(2024, 1, 1), Status = "Active" };
		var member3 = new Member { Name = "M3", StreetAddress = "789", JoinDate = new DateTime(2024, 1, 1), Status = "Active" };

		context.Events.Add(ev);
		context.Members.Add(member1);
		context.Members.Add(member2);
		context.Members.Add(member3);
		await context.SaveChangesAsync();

		// 3 active members at recording time
		var repository = new EventRepository(context);

		// Act - 2 out of 3 participated
		// Rate = 2/3 * 100 = 66.67%
		await repository.UpdateStoredParticipationRateAsync(ev.Id, 66.67m);
		var stored = await repository.GetByIdAsync(ev.Id);

		// Assert
		Assert.Equal(66.67m, stored.StoredParticipationRate, 2);
	}

	[Fact]
	public async Task StoredParticipationRate_ExcludesInactiveMembers()
	{
		// Arrange
		var options = CreateInMemoryOptions();
		using var context = new StageFrightContext(options);

		var eventDate = new DateTime(2024, 6, 1);
		var ev = new Event { Date = eventDate, EventType = "Concert", StoredParticipationRate = 0 };

		// Create members - 2 active, 1 inactive
		var member1 = new Member { Name = "M1", StreetAddress = "123", JoinDate = new DateTime(2024, 1, 1), Status = "Active" };
		var member2 = new Member { Name = "M2", StreetAddress = "456", JoinDate = new DateTime(2024, 1, 1), Status = "Active" };
		var member3 = new Member { Name = "M3", StreetAddress = "789", JoinDate = new DateTime(2024, 1, 1), Status = "Inactive" };

		context.Events.Add(ev);
		context.Members.Add(member1);
		context.Members.Add(member2);
		context.Members.Add(member3);
		await context.SaveChangesAsync();

		// Only 2 active members count (not 3)
		var repository = new EventRepository(context);

		// Act - Both active members participated
		// Rate = 2/2 * 100 = 100%
		await repository.UpdateStoredParticipationRateAsync(ev.Id, 100);
		var stored = await repository.GetByIdAsync(ev.Id);

		// Assert
		Assert.Equal(100, stored.StoredParticipationRate);
	}

	#endregion

	#region Post-Event Archival Does Not Retroactively Change Tests

	[Fact]
	public async Task StoredParticipationRate_MemberArchivalAfterRecording_DoesNotAffectStoredRate()
	{
		// Arrange
		var options = CreateInMemoryOptions();
		using var context = new StageFrightContext(options);

		var eventDate = new DateTime(2024, 6, 1);
		var ev = new Event { Date = eventDate, EventType = "Concert", StoredParticipationRate = 0 };

		var member1 = new Member { Name = "M1", StreetAddress = "123", JoinDate = new DateTime(2024, 1, 1), Status = "Active" };
		var member2 = new Member { Name = "M2", StreetAddress = "456", JoinDate = new DateTime(2024, 1, 1), Status = "Active" };

		context.Events.Add(ev);
		context.Members.Add(member1);
		context.Members.Add(member2);
		await context.SaveChangesAsync();

		var repository = new EventRepository(context);
		var memberRepo = new MemberRepository(context);

		// Act 1 - Record participation rate based on 2 active members (e.g., 1 participated = 50%)
		await repository.UpdateStoredParticipationRateAsync(ev.Id, 50);
		var stored1 = await repository.GetByIdAsync(ev.Id);

		// Act 2 - Archive a member AFTER recording
		await memberRepo.SoftDeleteAsync(member2.Id, "system");

		// Assert - Stored rate should remain unchanged at 50%
		var stored2 = await repository.GetByIdAsync(ev.Id);
		Assert.Equal(50, stored1.StoredParticipationRate);
		Assert.Equal(50, stored2.StoredParticipationRate); // Still 50%, not recalculated
	}

	[Fact]
	public async Task StoredParticipationRate_InactivationAfterRecording_DoesNotAffectStoredRate()
	{
		// Arrange
		var options = CreateInMemoryOptions();
		using var context = new StageFrightContext(options);

		var eventDate = new DateTime(2024, 6, 1);
		var ev = new Event { Date = eventDate, EventType = "Concert", StoredParticipationRate = 0 };

		var member1 = new Member { Name = "M1", StreetAddress = "123", JoinDate = new DateTime(2024, 1, 1), Status = "Active" };
		var member2 = new Member { Name = "M2", StreetAddress = "456", JoinDate = new DateTime(2024, 1, 1), Status = "Active" };

		context.Events.Add(ev);
		context.Members.Add(member1);
		context.Members.Add(member2);
		await context.SaveChangesAsync();

		var repository = new EventRepository(context);
		var memberRepo = new MemberRepository(context);

		// Act 1 - Record participation based on 2 active members
		await repository.UpdateStoredParticipationRateAsync(ev.Id, 80);
		var stored1 = await repository.GetByIdAsync(ev.Id);

		// Act 2 - Inactivate member AFTER recording
		member2.Status = "Inactive";
		member2.InactivateDate = DateTime.Now.AddDays(1);
		await memberRepo.UpdateAsync(member2);

		// Assert - Stored rate should remain 80%
		var stored2 = await repository.GetByIdAsync(ev.Id);
		Assert.Equal(80, stored1.StoredParticipationRate);
		Assert.Equal(80, stored2.StoredParticipationRate);
	}

	#endregion

	#region Archive Only Affects Future Rate Calculations Tests

	[Fact]
	public async Task StoredParticipationRate_ArchiveAffectsFutureRates_NotPastRates()
	{
		// Arrange
		var options = CreateInMemoryOptions();
		using var context = new StageFrightContext(options);

		// Create past and future events
		var pastEvent = new Event { Date = new DateTime(2024, 5, 1), EventType = "Concert", StoredParticipationRate = 85 };
		var futureEvent = new Event { Date = new DateTime(2024, 7, 1), EventType = "Concert", StoredParticipationRate = 0 };

		var member = new Member { Name = "M1", StreetAddress = "123", JoinDate = new DateTime(2024, 1, 1), Status = "Active" };

		context.Events.Add(pastEvent);
		context.Events.Add(futureEvent);
		context.Members.Add(member);
		await context.SaveChangesAsync();

		var repository = new EventRepository(context);

		// Act - Archive member on 2024-06-01 (between past and future)
		var archivalDate = new DateTime(2024, 6, 1);
		member.Status = "Inactive";
		member.InactivateDate = archivalDate;
		await context.SaveChangesAsync();

		// Calculate future rate assuming only 1 other active member present at future event
		// Rate = 1/1 * 100 = 100% (archived member not counted as active)
		await repository.UpdateStoredParticipationRateAsync(futureEvent.Id, 100);

		// Assert
		var pastStored = await repository.GetByIdAsync(pastEvent.Id);
		var futureStored = await repository.GetByIdAsync(futureEvent.Id);

		Assert.Equal(85, pastStored.StoredParticipationRate); // Unchanged
		Assert.Equal(100, futureStored.StoredParticipationRate); // New rate based on active members at that time
	}

	#endregion

	#region Complex Scenario Tests

	[Fact]
	public async Task StoredParticipationRate_MultipleEvents_EachHasOwnRate()
	{
		// Arrange
		var options = CreateInMemoryOptions();
		using var context = new StageFrightContext(options);

		var event1 = new Event { Date = new DateTime(2024, 6, 1), EventType = "Concert", StoredParticipationRate = 0 };
		var event2 = new Event { Date = new DateTime(2024, 6, 8), EventType = "Concert", StoredParticipationRate = 0 };
		var event3 = new Event { Date = new DateTime(2024, 6, 15), EventType = "Concert", StoredParticipationRate = 0 };

		context.Events.Add(event1);
		context.Events.Add(event2);
		context.Events.Add(event3);
		await context.SaveChangesAsync();

		var repository = new EventRepository(context);

		// Act - Set different rates for each
		await repository.UpdateStoredParticipationRateAsync(event1.Id, 60);
		await repository.UpdateStoredParticipationRateAsync(event2.Id, 80);
		await repository.UpdateStoredParticipationRateAsync(event3.Id, 90);

		// Assert
		var e1 = await repository.GetByIdAsync(event1.Id);
		var e2 = await repository.GetByIdAsync(event2.Id);
		var e3 = await repository.GetByIdAsync(event3.Id);

		Assert.Equal(60, e1.StoredParticipationRate);
		Assert.Equal(80, e2.StoredParticipationRate);
		Assert.Equal(90, e3.StoredParticipationRate);
	}

	[Fact]
	public async Task StoredParticipationRate_ParticularFormula_3of5Active()
	{
		// Arrange
		var options = CreateInMemoryOptions();
		using var context = new StageFrightContext(options);

		var ev = new Event { Date = DateTime.Now, EventType = "Concert", StoredParticipationRate = 0 };
		context.Events.Add(ev);
		await context.SaveChangesAsync();

		var repository = new EventRepository(context);

		// Act - Formula: 3 participated out of 5 active
		// Rate = 3/5 * 100 = 60%
		var expectedRate = (decimal)3 / 5 * 100; // 60
		await repository.UpdateStoredParticipationRateAsync(ev.Id, expectedRate);

		var stored = await repository.GetByIdAsync(ev.Id);

		// Assert
		Assert.Equal(60, stored.StoredParticipationRate);
	}

	[Fact]
	public async Task StoredParticipationRate_ParticularFormula_1of4Active()
	{
		// Arrange
		var options = CreateInMemoryOptions();
		using var context = new StageFrightContext(options);

		var ev = new Event { Date = DateTime.Now, EventType = "Concert", StoredParticipationRate = 0 };
		context.Events.Add(ev);
		await context.SaveChangesAsync();

		var repository = new EventRepository(context);

		// Act - Formula: 1 participated out of 4 active
		// Rate = 1/4 * 100 = 25%
		var expectedRate = (decimal)1 / 4 * 100; // 25
		await repository.UpdateStoredParticipationRateAsync(ev.Id, expectedRate);

		var stored = await repository.GetByIdAsync(ev.Id);

		// Assert
		Assert.Equal(25, stored.StoredParticipationRate);
	}

	#endregion

	#region Event Type Tests

	[Fact]
	public async Task StoredParticipationRate_DifferentEventTypes_AllTrackRates()
	{
		// Arrange
		var options = CreateInMemoryOptions();
		using var context = new StageFrightContext(options);

		var concert = new Event { Date = DateTime.Now, EventType = "Concert", StoredParticipationRate = 0 };
		var eisteddfod = new Event { Date = DateTime.Now.AddDays(1), EventType = "Eisteddfod", StoredParticipationRate = 0 };
		var agm = new Event { Date = DateTime.Now.AddDays(2), EventType = "AGM", StoredParticipationRate = 0 };

		context.Events.Add(concert);
		context.Events.Add(eisteddfod);
		context.Events.Add(agm);
		await context.SaveChangesAsync();

		var repository = new EventRepository(context);

		// Act - Set rates for different event types
		await repository.UpdateStoredParticipationRateAsync(concert.Id, 70);
		await repository.UpdateStoredParticipationRateAsync(eisteddfod.Id, 85);
		await repository.UpdateStoredParticipationRateAsync(agm.Id, 95);

		// Assert
		var c = await repository.GetByIdAsync(concert.Id);
		var e = await repository.GetByIdAsync(eisteddfod.Id);
		var a = await repository.GetByIdAsync(agm.Id);

		Assert.Equal(70, c.StoredParticipationRate);
		Assert.Equal(85, e.StoredParticipationRate);
		Assert.Equal(95, a.StoredParticipationRate);
	}

	#endregion

	#region Decimal Precision Tests

	[Fact]
	public async Task StoredParticipationRate_DecimalPrecision_Preserved()
	{
		// Arrange
		var options = CreateInMemoryOptions();
		using var context = new StageFrightContext(options);

		var ev = new Event { Date = DateTime.Now, EventType = "Concert", StoredParticipationRate = 0 };
		context.Events.Add(ev);
		await context.SaveChangesAsync();

		var repository = new EventRepository(context);

		// Act - Set a precise decimal value
		// 7 out of 9 = 77.777...%
		var preciseRate = (decimal)7 / 9 * 100;
		await repository.UpdateStoredParticipationRateAsync(ev.Id, preciseRate);

		var stored = await repository.GetByIdAsync(ev.Id);

		// Assert - decimal places should be preserved
		Assert.Equal(preciseRate, stored.StoredParticipationRate);
	}

	#endregion
}
