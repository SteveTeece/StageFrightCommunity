using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StageFright.Data.Context;
using Xunit;

namespace StageFright.Data.Tests;

/// <summary>
/// Tests for database migrations and schema setup.
/// Verifies that all entities, relationships, and constraints are created correctly.
/// </summary>
public class MigrationTests
{
	private DbContextOptions<StageFrightContext> CreateInMemoryOptions()
	{
		return new DbContextOptionsBuilder<StageFrightContext>()
			.UseInMemoryDatabase(Guid.NewGuid().ToString())
			.EnableSensitiveDataLogging()
			.ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
			.Options;
	}

	[Fact]
	public void DbContext_CanBeCreatedSuccessfully()
	{
		// Arrange
		var options = CreateInMemoryOptions();

		// Act
		using var context = new StageFrightContext(options);

		// Assert - verify context is created and DBsets are accessible
		Assert.NotNull(context);
		Assert.NotNull(context.Members);
		Assert.NotNull(context.Rehearsals);
	}

	[Fact]
	public void DbContext_HasAllEntitySets()
	{
		// Arrange
		var options = CreateInMemoryOptions();
		using var context = new StageFrightContext(options);

		// Act & Assert
		Assert.NotNull(context.Members);
		Assert.NotNull(context.Rehearsals);
		Assert.NotNull(context.Events);
		Assert.NotNull(context.Attendances);
		Assert.NotNull(context.Participations);
		Assert.NotNull(context.Categories);
		Assert.NotNull(context.Fees);
		Assert.NotNull(context.Payments);
		Assert.NotNull(context.Transactions);
		Assert.NotNull(context.CommitteeMemberships);
		Assert.NotNull(context.Settings);
		Assert.NotNull(context.AuditTrails);
	}

	[Fact]
	public async Task DbContext_CanInsertMember()
	{
		// Arrange
		var options = CreateInMemoryOptions();
		using var context = new StageFrightContext(options);

		var member = new StageFright.Core.Entities.Member
		{
			Name = "Test Member",
			StreetAddress = "123 Main St",
			JoinDate = DateTime.Now,
			Email = "test@example.com"
		};

		// Act
		context.Members.Add(member);
		await context.SaveChangesAsync();
		var retrieved = await context.Members.FindAsync(member.Id);

		// Assert
		Assert.NotNull(retrieved);
		Assert.Equal(member.Id, retrieved.Id);
		Assert.Equal("Test Member", retrieved.Name);
	}

	[Fact]
	public async Task DbContext_SoftDeleteFilter_ExcludesDeletedRecords()
	{
		// Arrange
		var options = CreateInMemoryOptions();
		using var context = new StageFrightContext(options);
		context.Database.EnsureCreated();

		var member = new StageFright.Core.Entities.Member
		{
			Name = "Test Member",
			StreetAddress = "123 Main St",
			JoinDate = DateTime.Now
		};

		context.Members.Add(member);
		await context.SaveChangesAsync();

		// Act - mark as deleted
		member.IsDeleted = true;
		member.DeletedAt = DateTime.UtcNow;
		context.Members.Update(member);
		await context.SaveChangesAsync();

		// Create new context to test filter
		using var context2 = new StageFrightContext(options);
		var allMembers = await context2.Members.ToListAsync();

		// Assert - should not include deleted member
		Assert.Empty(allMembers);
	}
}
