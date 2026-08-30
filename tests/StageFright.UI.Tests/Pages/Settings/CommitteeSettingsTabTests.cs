using Bunit;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.UI.Pages.Settings;
using AppSettings = StageFright.Core.Entities.Settings;

namespace StageFright.UI.Tests.Pages.Settings;

/// <summary>
/// bUnit tests for CommitteeSettingsTab — office-holder title add/archive flow, seat-count-target
/// persistence, and built-in titles rendering as read-only (FR-012/FR-013/FR-014).
/// </summary>
public class CommitteeSettingsTabTests : LocalizedTestContext
{
    private readonly ICommitteeOfficeHolderTypeService _officeHolderTypeService = Substitute.For<ICommitteeOfficeHolderTypeService>();
    private readonly ISettingsService _settingsService = Substitute.For<ISettingsService>();

    private static readonly Guid PresidentId = Guid.NewGuid();
    private static readonly Guid CustomId = Guid.NewGuid();

    public CommitteeSettingsTabTests()
    {
        Services.AddSingleton(_officeHolderTypeService);
        Services.AddSingleton(_settingsService);

        _officeHolderTypeService.GetActiveAsync(Arg.Any<CancellationToken>())
            .Returns(new List<CommitteeOfficeHolderType>
            {
                new() { Id = PresidentId, Name = "President", DisplayOrder = 0, IsBuiltIn = true,
                         CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
                new() { Id = CustomId, Name = "Publicity Officer", DisplayOrder = 3, IsBuiltIn = false,
                         CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow }
            });

        _settingsService.GetAsync(Arg.Any<CancellationToken>())
            .Returns(new AppSettings
            {
                Id = Guid.NewGuid(), OrganizationName = "Test", AnnualFee = 50m, AttendanceFee = 5m,
                MembershipRenewalMonth = 1, GeneralCommitteeSeatCountTarget = 7,
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
            });

        _officeHolderTypeService.AddAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ci => new CommitteeOfficeHolderType
            {
                Id = Guid.NewGuid(), Name = ci.ArgAt<string>(0), DisplayOrder = 4, IsBuiltIn = false,
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
            });
    }

    [Fact]
    public void Renders_ActiveOfficeHolderTitles()
    {
        var cut = Render<CommitteeSettingsTab>();

        Assert.Contains("President", cut.Markup);
        Assert.Contains("Publicity Officer", cut.Markup);
    }

    [Fact]
    public void BuiltInTitle_ShowsReadOnly_NotArchiveButton()
    {
        var cut = Render<CommitteeSettingsTab>();

        var rows = cut.FindAll("tbody tr");
        var presidentRow = rows.Single(r => r.TextContent.Contains("President"));

        Assert.Contains("Read-only", presidentRow.TextContent);
        Assert.Empty(presidentRow.QuerySelectorAll("button.btn-outline-danger"));
    }

    [Fact]
    public void CustomTitle_ShowsArchiveButton()
    {
        var cut = Render<CommitteeSettingsTab>();

        var rows = cut.FindAll("tbody tr");
        var customRow = rows.Single(r => r.TextContent.Contains("Publicity Officer"));

        Assert.NotEmpty(customRow.QuerySelectorAll("button.btn-outline-danger"));
    }

    [Fact]
    public async Task AddTitle_CallsAddAsync_AndReloadsList()
    {
        var cut = Render<CommitteeSettingsTab>();

        cut.Find("#oht-name").Change("Webmaster");
        await cut.Find("form").SubmitAsync();

        await _officeHolderTypeService.Received(1).AddAsync("Webmaster", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ArchiveCustomTitle_CallsArchiveAsync()
    {
        var cut = Render<CommitteeSettingsTab>();

        var rows = cut.FindAll("tbody tr");
        var customRow = rows.Single(r => r.TextContent.Contains("Publicity Officer"));
        await customRow.QuerySelector("button.btn-outline-danger")!.ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        await _officeHolderTypeService.Received(1).ArchiveAsync(CustomId, Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void SeatCountTargetInput_Renders_WithCurrentValue()
    {
        var cut = Render<CommitteeSettingsTab>();

        var input = cut.Find("#seatCountTarget");
        Assert.Equal("7", input.GetAttribute("value"));
    }

    [Fact]
    public async Task SavingSeatCountTarget_CallsSettingsServiceSaveAsync_WithNewValue()
    {
        var cut = Render<CommitteeSettingsTab>();

        cut.Find("#seatCountTarget").Change("10");
        await cut.Find("button.btn-primary").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        await _settingsService.Received(1).SaveAsync(
            Arg.Is<AppSettings>(s => s!.GeneralCommitteeSeatCountTarget == 10),
            Arg.Any<CancellationToken>());
    }
}
