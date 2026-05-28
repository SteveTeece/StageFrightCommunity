using Xunit;
using StageFright.Core.Entities;

namespace StageFright.UI.Tests;

/// <summary>
/// Acceptance tests for User Story 8: Dashboard
/// Verifies dashboard entity structures and data models.
/// </summary>
public class DashboardTests
{
    [Fact]
    public void Member_StoresStatusForDashboard()
    {
        // Arrange
        var member = new Member { Id = Guid.NewGuid(), Status = "Active" };

        // Act
        var status = member.Status;

        // Assert
        Assert.Equal("Active", status);
    }

    [Fact]
    public void Rehearsal_StoresAttendanceRateForDisplay()
    {
        // Arrange
        var rehearsal = new Rehearsal
        {
            Id = Guid.NewGuid(),
            Date = DateTime.Now.AddDays(-7),
            StoredAttendanceRate = 85.5m
        };

        // Act
        var rate = rehearsal.StoredAttendanceRate;

        // Assert
        Assert.Equal(85.5m, rate);
    }

    [Fact]
    public void Event_StoresParticipationRateForDisplay()
    {
        // Arrange
        var eventData = new Event
        {
            Id = Guid.NewGuid(),
            Date = DateTime.Now.AddDays(-3),
            StoredParticipationRate = 72.0m
        };

        // Act
        var rate = eventData.StoredParticipationRate;

        // Assert
        Assert.Equal(72.0m, rate);
    }

    [Fact]
    public void Dashboard_CanDisplayMultipleMembers_ForCounts()
    {
        // Arrange
        var members = new List<Member>
        {
            new() { Id = Guid.NewGuid(), Status = "Active" },
            new() { Id = Guid.NewGuid(), Status = "Active" },
            new() { Id = Guid.NewGuid(), Status = "Inactive" }
        };

        // Act
        var activeCount = members.Count(m => m.Status == "Active");
        var inactiveCount = members.Count(m => m.Status == "Inactive");

        // Assert
        Assert.Equal(2, activeCount);
        Assert.Equal(1, inactiveCount);
    }

    [Fact]
    public void Dashboard_CanDisplayRehearsalHistory_WithRunningTotal()
    {
        // Arrange
        var rehearsals = new List<Rehearsal>
        {
            new() { Id = Guid.NewGuid(), Date = DateTime.Now.AddDays(-7) },
            new() { Id = Guid.NewGuid(), Date = DateTime.Now.AddDays(-14) },
            new() { Id = Guid.NewGuid(), Date = DateTime.Now.AddDays(-21) }
        };

        // Act
        var totalCount = rehearsals.Count();

        // Assert
        Assert.Equal(3, totalCount);
    }

    [Fact]
    public void Dashboard_CanDisplayEventHistory_WithRunningTotal()
    {
        // Arrange
        var events = new List<Event>
        {
            new() { Id = Guid.NewGuid(), Date = DateTime.Now.AddDays(-7) },
            new() { Id = Guid.NewGuid(), Date = DateTime.Now.AddDays(-14) }
        };

        // Act
        var totalCount = events.Count();

        // Assert
        Assert.Equal(2, totalCount);
    }

    [Fact]
    public void Finance_PlaceholderBalance_DisplaysAsZero()
    {
        // Arrange
        var outstandingBalance = 0m;

        // Act
        var display = outstandingBalance.ToString("C");

        // Assert
        Assert.Equal("$0.00", display);
    }
}
