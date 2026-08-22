using Microsoft.EntityFrameworkCore;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Data.Repositories;
using StageFright.Data.Tests.Infrastructure;

namespace StageFright.Data.Tests.Repositories;

/// <summary>
/// Integration tests for CommitteePositionRecordRepository using SQLite in-memory connections.
/// Covers the OfficeHolderType navigation attach (which deliberately bypasses that entity's
/// own soft-delete query filter — spec.md edge case: "What happens to a custom office-holder
/// title that is archived after it was used in a past AGM? The historical AGM and committee
/// records keep showing the archived title's name.").
/// </summary>
public class CommitteePositionRecordRepositoryTests : IDisposable
{
    private readonly DbContextFactory _factory = new();

    [Fact]
    public async Task GetByAgmAsync_ResolvesOfficeHolderTypeName_EvenAfterTitleIsArchived()
    {
        using var db = _factory.CreateContext();

        var (member, officeHolderType, agm, term, position) = await SeedTermPositionAsync(db, "Publicity Officer");

        officeHolderType.IsDeleted = true;
        officeHolderType.DeletedAt = DateTime.UtcNow;
        officeHolderType.DeletedBy = "coordinator";
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        using var freshDb = _factory.CreateContext();
        var repo = new CommitteePositionRecordRepository(freshDb);

        var records = await repo.GetByAgmAsync(agm.Id, TestContext.Current.CancellationToken);

        var record = Assert.Single(records);
        Assert.NotNull(record.OfficeHolderType);
        Assert.Equal("Publicity Officer", record.OfficeHolderType!.Name);
    }

    [Fact]
    public async Task GetByTermAsync_ResolvesOfficeHolderTypeName_EvenAfterTitleIsArchived()
    {
        using var db = _factory.CreateContext();

        var (member, officeHolderType, agm, term, position) = await SeedTermPositionAsync(db, "Webmaster");

        officeHolderType.IsDeleted = true;
        officeHolderType.DeletedAt = DateTime.UtcNow;
        officeHolderType.DeletedBy = "coordinator";
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        using var freshDb = _factory.CreateContext();
        var repo = new CommitteePositionRecordRepository(freshDb);

        var records = await repo.GetByTermAsync(term.Id, TestContext.Current.CancellationToken);

        var record = Assert.Single(records);
        Assert.NotNull(record.OfficeHolderType);
        Assert.Equal("Webmaster", record.OfficeHolderType!.Name);
        Assert.NotNull(record.Member);
    }

    [Fact]
    public async Task GetByMemberAsync_ResolvesOfficeHolderTypeName_EvenAfterTitleIsArchived()
    {
        using var db = _factory.CreateContext();

        var (member, officeHolderType, agm, term, position) = await SeedTermPositionAsync(db, "Historian");

        officeHolderType.IsDeleted = true;
        officeHolderType.DeletedAt = DateTime.UtcNow;
        officeHolderType.DeletedBy = "coordinator";
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        using var freshDb = _factory.CreateContext();
        var repo = new CommitteePositionRecordRepository(freshDb);

        var records = await repo.GetByMemberAsync(member.Id, TestContext.Current.CancellationToken);

        var record = Assert.Single(records);
        Assert.NotNull(record.OfficeHolderType);
        Assert.Equal("Historian", record.OfficeHolderType!.Name);
        Assert.NotNull(record.CommitteeTerm);
    }

    [Fact]
    public async Task GetByAgmAsync_LeavesOfficeHolderTypeNull_ForGeneralCommitteeRecords()
    {
        using var db = _factory.CreateContext();

        var member = new Member
        {
            Id = Guid.NewGuid(), FirstName = "Dana", LastName = "Test", StreetAddress = "1 St",
            JoinDate = DateTime.UtcNow, Status = MemberStatus.Active,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        var agm = new AnnualGeneralMeeting
        {
            Id = Guid.NewGuid(), Date = DateTime.UtcNow, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        var term = new CommitteeTerm
        {
            Id = Guid.NewGuid(), StartedByAgmId = agm.Id, StartDate = agm.Date, LabelYear = agm.Date.Year,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        var position = new CommitteePositionRecord
        {
            Id = Guid.NewGuid(), MemberId = member.Id, CommitteeTermId = term.Id,
            OfficeHolderTypeId = null, StartDate = agm.Date,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };

        db.Members.Add(member);
        db.AnnualGeneralMeetings.Add(agm);
        db.CommitteeTerms.Add(term);
        db.CommitteePositionRecords.Add(position);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        using var freshDb = _factory.CreateContext();
        var repo = new CommitteePositionRecordRepository(freshDb);

        var records = await repo.GetByAgmAsync(agm.Id, TestContext.Current.CancellationToken);

        var record = Assert.Single(records);
        Assert.Null(record.OfficeHolderType);
    }

    private static async Task<(Member Member, CommitteeOfficeHolderType Type, AnnualGeneralMeeting Agm, CommitteeTerm Term, CommitteePositionRecord Position)>
        SeedTermPositionAsync(StageFright.Data.StageFrightDbContext db, string officeHolderTypeName)
    {
        var member = new Member
        {
            Id = Guid.NewGuid(), FirstName = "Alice", LastName = "Test", StreetAddress = "1 St",
            JoinDate = DateTime.UtcNow, Status = MemberStatus.Active,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        var officeHolderType = new CommitteeOfficeHolderType
        {
            Id = Guid.NewGuid(), Name = officeHolderTypeName, DisplayOrder = 3, IsBuiltIn = false,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        var agm = new AnnualGeneralMeeting
        {
            Id = Guid.NewGuid(), Date = DateTime.UtcNow, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        var term = new CommitteeTerm
        {
            Id = Guid.NewGuid(), StartedByAgmId = agm.Id, StartDate = agm.Date, LabelYear = agm.Date.Year,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        var position = new CommitteePositionRecord
        {
            Id = Guid.NewGuid(), MemberId = member.Id, CommitteeTermId = term.Id,
            OfficeHolderTypeId = officeHolderType.Id, StartDate = agm.Date,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };

        db.Members.Add(member);
        db.CommitteeOfficeHolderTypes.Add(officeHolderType);
        db.AnnualGeneralMeetings.Add(agm);
        db.CommitteeTerms.Add(term);
        db.CommitteePositionRecords.Add(position);
        await db.SaveChangesAsync();

        return (member, officeHolderType, agm, term, position);
    }

    public void Dispose() { }
}
