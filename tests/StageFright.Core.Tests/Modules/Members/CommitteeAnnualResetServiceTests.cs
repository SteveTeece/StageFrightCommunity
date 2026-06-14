using NSubstitute;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Exceptions;
using StageFright.Core.Modules.Members;
using StageFright.Core.Tests.Fixtures;

namespace StageFright.Core.Tests.Modules.Members;

/// <summary>
/// Unit tests for CommitteeAnnualResetService — annual reset logic and AGM banner conditions.
/// </summary>
public class CommitteeAnnualResetServiceTests : TestBase
{
    private readonly ICommitteeMembershipRepository _committeeRepo = Substitute.For<ICommitteeMembershipRepository>();
    private readonly ISettingsService _settingsService = Substitute.For<ISettingsService>();
    private readonly IEventRepository _eventRepo = Substitute.For<IEventRepository>();
    private readonly IAuditTrailService _audit = Substitute.For<IAuditTrailService>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    private readonly int _currentYear = DateTime.UtcNow.Year;

    public CommitteeAnnualResetServiceTests()
    {
        _unitOfWork
            .ExecuteInTransactionAsync(Arg.Any<Func<CancellationToken, Task>>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.ArgAt<Func<CancellationToken, Task>>(0)(ci.ArgAt<CancellationToken>(1)));
    }

    private CommitteeAnnualResetService CreateService() =>
        new(_committeeRepo, _settingsService, _eventRepo, _audit, _unitOfWork);

    private Settings ValidSettings(int? lastResetYear = null) => new()
    {
        Id = Guid.NewGuid(),
        OrganizationName = "Test Org",
        AnnualFee = 50m,
        AttendanceFee = 5m,
        MembershipRenewalMonth = 1,
        LastCommitteeResetYear = lastResetYear,
        SchemaVersion = "1.0.0",
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    // --- ResetAsync ---

    [Fact]
    public async Task ResetAsync_SoftDeletesCurrentYear_AndUpdatesLastResetYear()
    {
        var settings = ValidSettings(lastResetYear: null);
        _settingsService.GetAsync(Arg.Any<CancellationToken>()).Returns(settings);

        var svc = CreateService();
        await svc.ResetAsync();

        await _committeeRepo.Received(1).SoftDeleteCurrentYearAsync(_currentYear, "system", Arg.Any<CancellationToken>());
        await _settingsService.Received(1).SaveAsync(
            Arg.Is<Settings>(s => s.LastCommitteeResetYear == _currentYear),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ResetAsync_WritesAuditEntry_WithCommitteeResetAction()
    {
        var settings = ValidSettings();
        _settingsService.GetAsync(Arg.Any<CancellationToken>()).Returns(settings);

        var svc = CreateService();
        await svc.ResetAsync();

        await _audit.Received(1).LogAsync(
            "CommitteeMembership",
            Guid.Empty,
            AuditAction.CommitteeReset,
            newValue: _currentYear.ToString(),
            ct: Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ResetAsync_WhenSettingsNull_Throws_ValidationException()
    {
        _settingsService.GetAsync(Arg.Any<CancellationToken>()).Returns((Settings?)null);

        var svc = CreateService();
        await Assert.ThrowsAsync<ValidationException>(() => svc.ResetAsync());
    }

    [Fact]
    public async Task ResetAsync_ExecutesInsideTransaction()
    {
        var settings = ValidSettings();
        _settingsService.GetAsync(Arg.Any<CancellationToken>()).Returns(settings);

        var svc = CreateService();
        await svc.ResetAsync();

        await _unitOfWork.Received(1).ExecuteInTransactionAsync(
            Arg.Any<Func<CancellationToken, Task>>(),
            Arg.Any<CancellationToken>());
    }

    // --- CheckAgmBannerAsync ---

    [Fact]
    public async Task CheckAgmBannerAsync_ReturnsNull_WhenSettingsIsNull()
    {
        _settingsService.GetAsync(Arg.Any<CancellationToken>()).Returns((Settings?)null);

        var svc = CreateService();
        var result = await svc.CheckAgmBannerAsync();

        Assert.Null(result);
    }

    [Fact]
    public async Task CheckAgmBannerAsync_ReturnsNull_WhenLastResetYearIsCurrentYear()
    {
        var settings = ValidSettings(lastResetYear: _currentYear);
        _settingsService.GetAsync(Arg.Any<CancellationToken>()).Returns(settings);

        var svc = CreateService();
        var result = await svc.CheckAgmBannerAsync();

        Assert.Null(result);
    }

    [Fact]
    public async Task CheckAgmBannerAsync_ReturnsNull_WhenNoAgmExistsThisYear()
    {
        var settings = ValidSettings(lastResetYear: null);
        _settingsService.GetAsync(Arg.Any<CancellationToken>()).Returns(settings);
        _eventRepo.AgmExistsInYearAsync(_currentYear, Arg.Any<CancellationToken>()).Returns(false);

        var svc = CreateService();
        var result = await svc.CheckAgmBannerAsync();

        Assert.Null(result);
    }

    [Fact]
    public async Task CheckAgmBannerAsync_ReturnsNull_WhenAgmIsWithinLast7Days()
    {
        var settings = ValidSettings(lastResetYear: null);
        _settingsService.GetAsync(Arg.Any<CancellationToken>()).Returns(settings);
        _eventRepo.AgmExistsInYearAsync(_currentYear, Arg.Any<CancellationToken>()).Returns(true);

        // AGM occurred only 3 days ago — too recent for the banner
        var recentAgm = new Event { Date = DateTime.UtcNow.AddDays(-3) };
        _eventRepo.GetMostRecentPastAsync(Arg.Any<DateTime>(), Arg.Any<CancellationToken>()).Returns(recentAgm);

        var svc = CreateService();
        var result = await svc.CheckAgmBannerAsync();

        Assert.Null(result);
    }

    [Fact]
    public async Task CheckAgmBannerAsync_ReturnsBannerMessage_WhenAllConditionsMet()
    {
        var settings = ValidSettings(lastResetYear: _currentYear - 1);
        _settingsService.GetAsync(Arg.Any<CancellationToken>()).Returns(settings);
        _eventRepo.AgmExistsInYearAsync(_currentYear, Arg.Any<CancellationToken>()).Returns(true);

        // AGM occurred 10 days ago — old enough for the banner
        var oldAgm = new Event { Date = DateTime.UtcNow.AddDays(-10) };
        _eventRepo.GetMostRecentPastAsync(Arg.Any<DateTime>(), Arg.Any<CancellationToken>()).Returns(oldAgm);

        var svc = CreateService();
        var result = await svc.CheckAgmBannerAsync();

        Assert.NotNull(result);
        Assert.NotEmpty(result);
    }

    [Fact]
    public async Task CheckAgmBannerAsync_ReturnsNull_WhenNoRecentEventFound()
    {
        var settings = ValidSettings(lastResetYear: null);
        _settingsService.GetAsync(Arg.Any<CancellationToken>()).Returns(settings);
        _eventRepo.AgmExistsInYearAsync(_currentYear, Arg.Any<CancellationToken>()).Returns(true);
        _eventRepo.GetMostRecentPastAsync(Arg.Any<DateTime>(), Arg.Any<CancellationToken>()).Returns((Event?)null);

        var svc = CreateService();
        var result = await svc.CheckAgmBannerAsync();

        Assert.Null(result);
    }
}
