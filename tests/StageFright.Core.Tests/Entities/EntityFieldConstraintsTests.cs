using StageFright.Core.Entities;
using StageFright.Core.Tests.Fixtures;

namespace StageFright.Core.Tests.Entities;

/// <summary>
/// Verifies field constraints on domain entities via reflection:
/// - Financial entities (Fee, Payment, Transaction) have NO soft-delete fields (Constitution §3.4)
/// - Soft-deletable entities carry IsDeleted, DeletedAt, DeletedBy
/// - Decimal fields are present on financial entities
/// </summary>
public class EntityFieldConstraintsTests : TestBase
{
    // Financial entities must NOT have soft-delete fields
    [Theory]
    [InlineData(typeof(Fee))]
    [InlineData(typeof(Payment))]
    [InlineData(typeof(Transaction))]
    public void FinancialEntities_DoNotHaveIsDeletedField(Type entityType)
    {
        Assert.Null(entityType.GetProperty("IsDeleted"));
    }

    [Theory]
    [InlineData(typeof(Fee))]
    [InlineData(typeof(Payment))]
    [InlineData(typeof(Transaction))]
    public void FinancialEntities_DoNotHaveDeletedAtField(Type entityType)
    {
        Assert.Null(entityType.GetProperty("DeletedAt"));
    }

    [Theory]
    [InlineData(typeof(Fee))]
    [InlineData(typeof(Payment))]
    [InlineData(typeof(Transaction))]
    public void FinancialEntities_DoNotHaveDeletedByField(Type entityType)
    {
        Assert.Null(entityType.GetProperty("DeletedBy"));
    }

    // Soft-deletable entities must have all three soft-delete fields
    [Theory]
    [InlineData(typeof(Member))]
    [InlineData(typeof(CommitteePositionRecord))]
    [InlineData(typeof(Rehearsal))]
    [InlineData(typeof(AttendanceRecord))]
    [InlineData(typeof(Event))]
    [InlineData(typeof(EventType))]
    [InlineData(typeof(ParticipationRecord))]
    [InlineData(typeof(Account))]
    [InlineData(typeof(Settings))]
    public void SoftDeletableEntities_HaveIsDeletedField(Type entityType)
    {
        var prop = entityType.GetProperty("IsDeleted");
        Assert.NotNull(prop);
        Assert.Equal(typeof(bool), prop!.PropertyType);
    }

    [Theory]
    [InlineData(typeof(Member))]
    [InlineData(typeof(CommitteePositionRecord))]
    [InlineData(typeof(Rehearsal))]
    [InlineData(typeof(Account))]
    [InlineData(typeof(Settings))]
    public void SoftDeletableEntities_HaveDeletedAtField(Type entityType)
    {
        var prop = entityType.GetProperty("DeletedAt");
        Assert.NotNull(prop);
        Assert.Equal(typeof(DateTime?), prop!.PropertyType);
    }

    [Theory]
    [InlineData(typeof(Member))]
    [InlineData(typeof(CommitteePositionRecord))]
    [InlineData(typeof(Rehearsal))]
    [InlineData(typeof(Account))]
    [InlineData(typeof(Settings))]
    public void SoftDeletableEntities_HaveDeletedByField(Type entityType)
    {
        var prop = entityType.GetProperty("DeletedBy");
        Assert.NotNull(prop);
        Assert.Equal(typeof(string), prop!.PropertyType);
    }

    // Fee Amount must be decimal
    [Fact]
    public void Fee_Amount_IsDecimal()
    {
        var prop = typeof(Fee).GetProperty("Amount");
        Assert.NotNull(prop);
        Assert.Equal(typeof(decimal), prop!.PropertyType);
    }

    // Payment Amount must be decimal
    [Fact]
    public void Payment_Amount_IsDecimal()
    {
        var prop = typeof(Payment).GetProperty("Amount");
        Assert.NotNull(prop);
        Assert.Equal(typeof(decimal), prop!.PropertyType);
    }

    // Transaction debit/credit must be decimal
    [Fact]
    public void Transaction_DebitAndCreditAmounts_AreDecimal()
    {
        Assert.Equal(typeof(decimal), typeof(Transaction).GetProperty("DebitAmount")!.PropertyType);
        Assert.Equal(typeof(decimal), typeof(Transaction).GetProperty("CreditAmount")!.PropertyType);
    }

    // AuditTrailEntry has no soft-delete fields (log-record exemption)
    [Fact]
    public void AuditTrailEntry_DoesNotHaveIsDeletedField()
    {
        Assert.Null(typeof(AuditTrailEntry).GetProperty("IsDeleted"));
    }

    // All entities have a Guid Id primary key
    [Theory]
    [InlineData(typeof(Member))]
    [InlineData(typeof(CommitteePositionRecord))]
    [InlineData(typeof(Rehearsal))]
    [InlineData(typeof(Fee))]
    [InlineData(typeof(Payment))]
    [InlineData(typeof(Transaction))]
    [InlineData(typeof(Account))]
    [InlineData(typeof(Settings))]
    [InlineData(typeof(AuditTrailEntry))]
    public void AllEntities_HaveGuidIdProperty(Type entityType)
    {
        var prop = entityType.GetProperty("Id");
        Assert.NotNull(prop);
        Assert.Equal(typeof(Guid), prop!.PropertyType);
    }

    // Settings has no UpdatedAt (it does per data model, verify)
    [Fact]
    public void Settings_HasUpdatedAtField()
    {
        var prop = typeof(Settings).GetProperty("UpdatedAt");
        Assert.NotNull(prop);
    }
}
