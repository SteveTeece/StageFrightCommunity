using Xunit;
using StageFright.Core.Entities;
using StageFright.Core.Services;

namespace StageFright.UI.Tests;

/// <summary>
/// Acceptance tests for User Story 2: Member Management
/// Verifies member entity structure and service availability.
/// </summary>
public class MemberModuleTests
{
    [Fact]
    public void Member_CanBeCreated_WithValidData()
    {
        // Arrange & Act
        var member = new Member
        {
            Id = Guid.NewGuid(),
            Name = "John Smith",
            StreetAddress = "123 Main St",
            Status = "Active"
        };

        // Assert
        Assert.NotNull(member);
        Assert.Equal("John Smith", member.Name);
        Assert.Equal("Active", member.Status);
    }

    [Fact]
    public void Member_StatusTracking_ActiveInactiveSupported()
    {
        // Arrange
        var activeMember = new Member { Status = "Active" };
        var inactiveMember = new Member { Status = "Inactive" };

        // Act & Assert
        Assert.Equal("Active", activeMember.Status);
        Assert.Equal("Inactive", inactiveMember.Status);
    }

    [Fact]
    public void AgeCalculationService_CalculatesAge_Correctly()
    {
        // Arrange
        var service = new AgeCalculationService();
        var dob = DateTime.Now.AddYears(-25).AddDays(-10);

        // Act
        var age = service.CalculateAge(dob);

        // Assert
        Assert.Equal(25, age);
    }

    [Fact]
    public void CommitteeMembership_Structure_Valid()
    {
        // Arrange & Act
        var membership = new CommitteeMembership
        {
            Id = Guid.NewGuid(),
            MemberId = Guid.NewGuid(),
            Year = DateTime.Now.Year,
            Position = "Treasurer"
        };

        // Assert
        Assert.NotNull(membership);
        Assert.Equal("Treasurer", membership.Position);
        Assert.Equal(DateTime.Now.Year, membership.Year);
    }
}
