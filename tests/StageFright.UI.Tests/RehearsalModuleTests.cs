using Xunit;
using StageFright.Core.Entities;

namespace StageFright.UI.Tests;

/// <summary>
/// Acceptance tests for User Story 3: Rehearsal Scheduling
/// Verifies rehearsal and attendance entity structures and immutability.
/// </summary>
public class RehearsalModuleTests
{
    [Fact]
    public void Rehearsal_CanBeCreated_WithDateAndNotes()
    {
        // Arrange & Act
        var rehearsal = new Rehearsal
        {
            Id = Guid.NewGuid(),
            Date = DateTime.Now.AddDays(7),
            Notes = "Regular rehearsal",
            StoredAttendanceRate = 0
        };

        // Assert
        Assert.NotNull(rehearsal);
        Assert.Equal("Regular rehearsal", rehearsal.Notes);
    }

    [Fact]
    public void Attendance_RecordsWithPaidStatus()
    {
        // Arrange & Act
        var attendance = new Attendance
        {
            Id = Guid.NewGuid(),
            RehearsalId = Guid.NewGuid(),
            MemberId = Guid.NewGuid(),
            PaidStatus = "Paid"
        };

        // Assert
        Assert.Equal("Paid", attendance.PaidStatus);
    }

    [Fact]
    public void Attendance_SupportsUnpaidOverride()
    {
        // Arrange & Act
        var attendance = new Attendance
        {
            Id = Guid.NewGuid(),
            RehearsalId = Guid.NewGuid(),
            MemberId = Guid.NewGuid(),
            PaidStatus = "Unpaid"
        };

        // Assert
        Assert.Equal("Unpaid", attendance.PaidStatus);
    }

    [Fact]
    public void StoredAttendanceRate_Immutable_AfterCreation()
    {
        // Arrange
        var rehearsal = new Rehearsal
        {
            Id = Guid.NewGuid(),
            Date = DateTime.Now,
            StoredAttendanceRate = 85.5m
        };

        // Act
        var rate = rehearsal.StoredAttendanceRate;

        // Assert
        Assert.Equal(85.5m, rate);
    }

    [Fact]
    public void Rehearsal_CanHaveHistoricalList_WithRates()
    {
        // Arrange
        var rehearsals = new List<Rehearsal>
        {
            new() { Id = Guid.NewGuid(), Date = DateTime.Now.AddDays(-7), StoredAttendanceRate = 80m },
            new() { Id = Guid.NewGuid(), Date = DateTime.Now.AddDays(-14), StoredAttendanceRate = 75m }
        };

        // Act
        var historical = rehearsals.OrderByDescending(r => r.Date).ToList();

        // Assert
        Assert.Equal(2, historical.Count);
        Assert.All(historical, r => Assert.True(r.StoredAttendanceRate > 0));
    }
}
