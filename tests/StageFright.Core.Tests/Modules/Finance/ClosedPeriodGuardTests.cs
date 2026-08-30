using NSubstitute;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Exceptions;
using StageFright.Core.Modules.Finance;
using StageFright.Core.Tests.Fixtures;

namespace StageFright.Core.Tests.Modules.Finance;

/// <summary>
/// Unit tests for <see cref="ClosedPeriodGuard"/> (spec 028, US6 / FR-017): a posting dated on
/// or before <c>Settings.ClosedThroughDate</c> is rejected; everything else — including the
/// pre-setup (null settings) and nothing-closed (null date) states — passes.
/// </summary>
public class ClosedPeriodGuardTests : TestBase
{
    private readonly ISettingsRepository _settingsRepo = Substitute.For<ISettingsRepository>();

    private ClosedPeriodGuard CreateGuard() => new(_settingsRepo);

    private void GivenSettings(Settings? settings) =>
        _settingsRepo.GetAsync(Arg.Any<CancellationToken>()).Returns(settings);

    private static Settings SettingsClosedThrough(DateTime? closedThrough) => new()
    {
        Id = Guid.NewGuid(),
        OrganizationName = "Test Org",
        ClosedThroughDate = closedThrough
    };

    [Fact]
    public async Task Should_NotThrow_When_SettingsIsNull()
    {
        GivenSettings(null);

        await CreateGuard().EnsureOpen(new DateTime(1900, 1, 1), Ct); // must not throw
    }

    [Fact]
    public async Task Should_NotThrow_When_ClosedThroughDateIsNull()
    {
        GivenSettings(SettingsClosedThrough(null));

        await CreateGuard().EnsureOpen(new DateTime(1900, 1, 1), Ct); // must not throw
    }

    [Fact]
    public async Task Should_NotThrow_When_PostingDateIsAfterClosedThroughDate()
    {
        GivenSettings(SettingsClosedThrough(new DateTime(2025, 6, 30)));

        await CreateGuard().EnsureOpen(new DateTime(2025, 7, 1), Ct); // must not throw
    }

    [Fact]
    public async Task Should_Throw_When_PostingDateIsExactlyOnClosedThroughDate()
    {
        GivenSettings(SettingsClosedThrough(new DateTime(2025, 6, 30)));

        await Assert.ThrowsAsync<ClosedPeriodException>(
            () => CreateGuard().EnsureOpen(new DateTime(2025, 6, 30), Ct));
    }

    [Fact]
    public async Task Should_Throw_When_PostingDateIsBeforeClosedThroughDate()
    {
        GivenSettings(SettingsClosedThrough(new DateTime(2025, 6, 30)));

        await Assert.ThrowsAsync<ClosedPeriodException>(
            () => CreateGuard().EnsureOpen(new DateTime(2025, 1, 15), Ct));
    }

    [Fact]
    public async Task Should_CompareByDateOnly_When_PostingDateHasALaterTimeOnTheClosedThroughDay()
    {
        GivenSettings(SettingsClosedThrough(new DateTime(2025, 6, 30, 0, 0, 0)));

        // 23:59 on the closed-through day is still inside the closed period.
        await Assert.ThrowsAsync<ClosedPeriodException>(
            () => CreateGuard().EnsureOpen(new DateTime(2025, 6, 30, 23, 59, 0), Ct));
    }
}
