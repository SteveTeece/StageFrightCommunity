using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Exceptions;
using StageFright.Core.Modules.Members;
using StageFright.UI.Pages.Members;

namespace StageFright.UI.Tests.Pages.Members;

/// <summary>
/// bUnit tests for MemberForm — validation, committee checkbox, and submit behavior.
/// </summary>
public class MemberFormTests : BunitContext
{
    private readonly IMemberService _memberService = Substitute.For<IMemberService>();
    private readonly ICommitteeService _committeeService = Substitute.For<ICommitteeService>();

    public MemberFormTests()
    {
        Services.AddSingleton(_memberService);
        Services.AddSingleton(_committeeService);

        _memberService.CreateAsync(Arg.Any<CreateMemberRequest>(), Arg.Any<CancellationToken>())
            .Returns(new Member
            {
                Id = Guid.NewGuid(),
                Name = "Jane Doe",
                StreetAddress = "1 Main St",
                Status = MemberStatus.Active,
                JoinDate = DateTime.UtcNow,
                ActivateDate = DateTime.UtcNow.Date,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });

        _committeeService.AddOrUpdateAsync(Arg.Any<Guid>(), Arg.Any<int>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new CommitteeMembership
            {
                Id = Guid.NewGuid(), MemberId = Guid.NewGuid(),
                Year = DateTime.UtcNow.Year, Position = "President",
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
            });
    }

    [Fact]
    public void Renders_RequiredFieldsForNewMember()
    {
        var cut = Render<MemberForm>();

        cut.Find("#name");
        cut.Find("#address");
        cut.Find("#joinDate");
        cut.Find("[type=submit]");
    }

    [Fact]
    public async Task SubmitWithEmptyName_ShowsValidationError_FromService()
    {
        _memberService.CreateAsync(Arg.Any<CreateMemberRequest>(), Arg.Any<CancellationToken>())
            .Returns<Member>(_ => throw new ValidationException("Name is required.", "Member", "CreateAsync"));

        var cut = Render<MemberForm>();

        cut.Find("#address").Change("1 Test St");
        await cut.Find("form").SubmitAsync();

        var alert = cut.Find(".alert-danger");
        Assert.Contains("Name is required.", alert.TextContent);
    }

    [Fact]
    public async Task SubmitWithInvalidEmail_ShowsValidationError_FromService()
    {
        _memberService.CreateAsync(Arg.Any<CreateMemberRequest>(), Arg.Any<CancellationToken>())
            .Returns<Member>(_ => throw new ValidationException("Email format is invalid.", "Member", "CreateAsync"));

        var cut = Render<MemberForm>();

        cut.Find("#name").Change("Jane Doe");
        cut.Find("#address").Change("1 Main St");
        cut.Find("#email").Change("not-an-email");
        await cut.Find("form").SubmitAsync();

        var alert = cut.Find(".alert-danger");
        Assert.Contains("Email format is invalid.", alert.TextContent);
    }

    [Fact]
    public async Task CommitteeCheckbox_WhenChecked_ShowsPositionField()
    {
        var cut = Render<MemberForm>();

        cut.Find("#isCommittee").Change(true);

        // Position field should now be visible
        cut.Find("#position");
    }

    [Fact]
    public async Task CommitteeCheckbox_Checked_SubmitWithoutPosition_ShowsPositionRequired()
    {
        var cut = Render<MemberForm>();

        cut.Find("#isCommittee").Change(true);
        cut.Find("#name").Change("Jane Doe");
        cut.Find("#address").Change("1 Main St");

        await cut.Find("form").SubmitAsync();

        var invalid = cut.Find("#position.is-invalid, #position + .invalid-feedback");
        Assert.NotNull(invalid);
    }

    [Fact]
    public async Task ValidSubmit_CallsCreateAsync()
    {
        var cut = Render<MemberForm>();

        cut.Find("#name").Change("Jane Doe");
        cut.Find("#address").Change("1 Main St");
        await cut.Find("form").SubmitAsync();

        await _memberService.Received(1).CreateAsync(
            Arg.Is<CreateMemberRequest>(r => r.Name == "Jane Doe"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ValidSubmit_NavigatesToMemberDetail()
    {
        var cut = Render<MemberForm>();

        cut.Find("#name").Change("Jane Doe");
        cut.Find("#address").Change("1 Main St");
        await cut.Find("form").SubmitAsync();

        var nav = Services.GetRequiredService<NavigationManager>();
        Assert.Contains("/members/", nav.Uri);
    }

    [Fact]
    public async Task CommitteeCheckbox_Checked_WithPosition_CallsAddOrUpdateAsync()
    {
        var cut = Render<MemberForm>();

        cut.Find("#isCommittee").Change(true);
        cut.Find("#name").Change("Jane Doe");
        cut.Find("#address").Change("1 Main St");
        cut.Find("#position").Change("President");
        await cut.Find("form").SubmitAsync();

        await _committeeService.Received(1).AddOrUpdateAsync(
            Arg.Any<Guid>(),
            Arg.Is<int>(y => y == DateTime.UtcNow.Year),
            Arg.Is<string>(p => p == "President"),
            Arg.Any<CancellationToken>());
    }
}
