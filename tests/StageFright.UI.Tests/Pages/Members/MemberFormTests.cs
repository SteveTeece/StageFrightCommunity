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
                FirstName = "Jane",
                LastName = "Doe",
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

        cut.Find("#firstName");
        cut.Find("#lastName");
        cut.Find("#address");
        cut.Find("#joinDate");
        cut.Find("[type=submit]");
    }

    [Fact]
    public void Renders_DateOfBirthField_ForNewMember()
    {
        var cut = Render<MemberForm>();

        cut.Find("#dob");
    }

    [Fact]
    public async Task SubmitWithDobViolatingAgeRange_ShowsValidationError_FromService()
    {
        _memberService.CreateAsync(Arg.Any<CreateMemberRequest>(), Arg.Any<CancellationToken>())
            .Returns<Member>(_ => throw new ValidationException(
                "Date of birth implies an age of 200 years, which exceeds the maximum allowed range of 150 years.",
                "Member", "CreateAsync"));

        var cut = Render<MemberForm>();

        cut.Find("#firstName").Change("Jane");
        cut.Find("#lastName").Change("Doe");
        cut.Find("#address").Change("1 Main St");
        await cut.Find("form").SubmitAsync();

        var alert = cut.Find(".alert-danger");
        Assert.Contains("exceeds the maximum allowed range", alert.TextContent);

        // Age-range violations only reach the global banner today — the DOB field itself
        // never gets an is-invalid class or its own <div class="invalid-feedback">.
        var dob = cut.Find("#dob");
        Assert.DoesNotContain("is-invalid", dob.ClassList);
    }

    [Fact]
    public async Task SubmitWithEmptyFirstName_ShowsValidationError_FromService()
    {
        _memberService.CreateAsync(Arg.Any<CreateMemberRequest>(), Arg.Any<CancellationToken>())
            .Returns<Member>(_ => throw new ValidationException("First name is required.", "Member", "CreateAsync"));

        var cut = Render<MemberForm>();

        cut.Find("#lastName").Change("Doe");
        cut.Find("#address").Change("1 Test St");
        await cut.Find("form").SubmitAsync();

        var alert = cut.Find(".alert-danger");
        Assert.Contains("First name is required.", alert.TextContent);
    }

    [Fact]
    public async Task SubmitWithEmptyLastName_ShowsValidationError_FromService()
    {
        _memberService.CreateAsync(Arg.Any<CreateMemberRequest>(), Arg.Any<CancellationToken>())
            .Returns<Member>(_ => throw new ValidationException("Last name is required.", "Member", "CreateAsync"));

        var cut = Render<MemberForm>();

        cut.Find("#firstName").Change("Jane");
        cut.Find("#address").Change("1 Test St");
        await cut.Find("form").SubmitAsync();

        var alert = cut.Find(".alert-danger");
        Assert.Contains("Last name is required.", alert.TextContent);
    }

    [Fact]
    public async Task SubmitWithInvalidEmail_ShowsValidationError_FromService()
    {
        _memberService.CreateAsync(Arg.Any<CreateMemberRequest>(), Arg.Any<CancellationToken>())
            .Returns<Member>(_ => throw new ValidationException("Email format is invalid.", "Member", "CreateAsync"));

        var cut = Render<MemberForm>();

        cut.Find("#firstName").Change("Jane");
        cut.Find("#lastName").Change("Doe");
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
        cut.Find("#firstName").Change("Jane");
        cut.Find("#lastName").Change("Doe");
        cut.Find("#address").Change("1 Main St");

        await cut.Find("form").SubmitAsync();

        var invalid = cut.Find("#position.is-invalid, #position + .invalid-feedback");
        Assert.NotNull(invalid);
    }

    [Fact]
    public async Task ValidSubmit_CallsCreateAsync()
    {
        var cut = Render<MemberForm>();

        cut.Find("#firstName").Change("Jane");
        cut.Find("#lastName").Change("Doe");
        cut.Find("#address").Change("1 Main St");
        await cut.Find("form").SubmitAsync();

        await _memberService.Received(1).CreateAsync(
            Arg.Is<CreateMemberRequest>(r => r.FirstName == "Jane" && r.LastName == "Doe"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ValidSubmit_NavigatesToMemberDetail()
    {
        var cut = Render<MemberForm>();

        cut.Find("#firstName").Change("Jane");
        cut.Find("#lastName").Change("Doe");
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
        cut.Find("#firstName").Change("Jane");
        cut.Find("#lastName").Change("Doe");
        cut.Find("#address").Change("1 Main St");
        cut.Find("#position").Change("President");
        await cut.Find("form").SubmitAsync();

        await _committeeService.Received(1).AddOrUpdateAsync(
            Arg.Any<Guid>(),
            Arg.Is<int>(y => y == DateTime.UtcNow.Year),
            Arg.Is<string>(p => p == "President"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void EditMember_PrePopulates_FirstNameAndLastName_Independently()
    {
        var memberId = Guid.NewGuid();
        _memberService.GetByIdAsync(memberId, Arg.Any<CancellationToken>())
            .Returns(new Member
            {
                Id = memberId,
                FirstName = "Existing",
                LastName = "Member",
                StreetAddress = "9 Old St",
                Status = MemberStatus.Active,
                JoinDate = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });

        var cut = Render<MemberForm>(parameters => parameters.Add(p => p.Id, memberId));

        Assert.Equal("Existing", cut.Find("#firstName").GetAttribute("value"));
        Assert.Equal("Member", cut.Find("#lastName").GetAttribute("value"));
    }

    [Fact]
    public async Task EditMember_ValidSubmit_CallsUpdateAsync_WithBothFieldsIndependently()
    {
        var memberId = Guid.NewGuid();
        _memberService.GetByIdAsync(memberId, Arg.Any<CancellationToken>())
            .Returns(new Member
            {
                Id = memberId,
                FirstName = "Existing",
                LastName = "Member",
                StreetAddress = "9 Old St",
                Status = MemberStatus.Active,
                JoinDate = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });

        var cut = Render<MemberForm>(parameters => parameters.Add(p => p.Id, memberId));

        cut.Find("#firstName").Change("Updated");
        await cut.Find("form").SubmitAsync();

        await _memberService.Received(1).UpdateAsync(
            memberId,
            Arg.Is<UpdateMemberRequest>(r => r.FirstName == "Updated" && r.LastName == "Member"),
            Arg.Any<CancellationToken>());
    }
}
