using NSubstitute;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Modules.Members;
using StageFright.Reports.Models;
using StageFright.Reports.Providers;

namespace StageFright.Reports.Tests;

/// <summary>
/// Tests for MemberListReportProvider — column age calculation (via the canonical
/// AgeCalculationService, including the Feb-29 Mar-1-anniversary edge case) and status filtering.
/// </summary>
public class MemberListReportProviderTests
{
    private readonly IMemberRepository _members = Substitute.For<IMemberRepository>();
    private readonly MemberListReportProvider _sut;

    public MemberListReportProviderTests()
    {
        _sut = new MemberListReportProvider(_members, new AgeCalculationService(RealLocalizer.Instance), RealLocalizer.Instance);
    }

    [Fact]
    public void ReportId_IsMemberList()
    {
        Assert.Equal("member-list", _sut.ReportId);
    }

    [Fact]
    public void ModuleName_IsMembers()
    {
        Assert.Equal("Members", _sut.ModuleName);
    }

    [Fact]
    public async Task GenerateAsync_Age_MatchesAgeCalculationService_ForOrdinaryDob()
    {
        var dob = DateTime.UtcNow.Date.AddYears(-40).AddDays(-1); // birthday already passed this year
        var member = MakeMember("Alice", dob);
        SetupActiveMembers(member);

        var result = await _sut.GenerateAsync(new ReportFilterValues(), TestContext.Current.CancellationToken);

        var row = result.Sections.Single().Rows.Single();
        var expectedAge = new AgeCalculationService(RealLocalizer.Instance).Calculate(dob, DateTime.UtcNow.Date);
        Assert.Equal(expectedAge.ToString(), row.Cells[5]);
    }

    [Fact]
    public async Task GenerateAsync_Age_MatchesAgeCalculationService_ForFeb29Dob()
    {
        // Leap-day DOB. AgeCalculationService treats 1 March as the anniversary in
        // non-leap years — the old inline AddYears-based calc rolled to 28 Feb instead,
        // diverging on 28 Feb / 29 Feb of a non-leap "today". This pins the two calculations
        // to agree regardless of which day the suite happens to run on.
        var dob = new DateTime(1992, 2, 29, 0, 0, 0, DateTimeKind.Utc);
        var member = MakeMember("Leap Day", dob);
        SetupActiveMembers(member);

        var result = await _sut.GenerateAsync(new ReportFilterValues(), TestContext.Current.CancellationToken);

        var row = result.Sections.Single().Rows.Single();
        var expectedAge = new AgeCalculationService(RealLocalizer.Instance).Calculate(dob, DateTime.UtcNow.Date);
        Assert.Equal(expectedAge.ToString(), row.Cells[5]);
    }

    [Fact]
    public async Task GenerateAsync_Age_IsEmpty_WhenDobIsNull()
    {
        var member = MakeMember("No Dob", null);
        SetupActiveMembers(member);

        var result = await _sut.GenerateAsync(new ReportFilterValues(), TestContext.Current.CancellationToken);

        var row = result.Sections.Single().Rows.Single();
        Assert.Equal(string.Empty, row.Cells[5]);
    }

    [Fact]
    public async Task GenerateAsync_DefaultFilter_ReturnsActiveMembersOnly()
    {
        var active = MakeMember("Active One", DateTime.UtcNow.Date.AddYears(-30));
        SetupActiveMembers(active);

        var result = await _sut.GenerateAsync(new ReportFilterValues(), TestContext.Current.CancellationToken);

        var names = result.Sections.Single().Rows.Select(r => r.Cells[0]).ToList();
        Assert.Contains("Active One", names);
        await _members.Received(1).GetByStatusAsync(MemberStatus.Active, Arg.Any<CancellationToken>());
    }

    // --- Helpers ---

    private void SetupActiveMembers(params Member[] members) =>
        _members.GetByStatusAsync(MemberStatus.Active, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<Member>>(members.ToList()));

    private static Member MakeMember(string name, DateTime? dob) => new()
    {
        Id = Guid.NewGuid(),
        FirstName = name,
        StreetAddress = "1 Test St",
        JoinDate = DateTime.UtcNow.Date,
        DateOfBirth = dob,
        Status = MemberStatus.Active,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };
}
