using NSubstitute;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Exceptions;
using StageFright.Core.Modules.Settings;
using StageFright.Core.Tests.Fixtures;

namespace StageFright.Core.Tests.Setup;

/// <summary>
/// Unit tests for SettingsService.SaveAsync's Abn validation: empty passes (existing
/// installs aren't blocked), malformed non-empty throws, valid non-empty saves successfully.
/// </summary>
public class SettingsServiceTests : TestBase
{
    private readonly ISettingsRepository _settingsRepo = Substitute.For<ISettingsRepository>();
    private readonly IAuditTrailService _audit = Substitute.For<IAuditTrailService>();

    private SettingsService CreateService() => new(_settingsRepo, _audit);

    private static Settings ValidSettings(string? abn) => new()
    {
        Id = Guid.NewGuid(),
        OrganizationName = "Test Org",
        Abn = abn,
        AnnualFee = 75m,
        AttendanceFee = 5m,
        MembershipRenewalMonth = 1
    };

    [Fact]
    public async Task SaveAsync_Saves_WhenAbnEmpty()
    {
        _settingsRepo.GetAsync(Arg.Any<CancellationToken>()).Returns((Settings?)null);
        var svc = CreateService();

        var settings = ValidSettings(null);
        await svc.SaveAsync(settings, Ct); // must not throw

        await _settingsRepo.Received(1).SaveAsync(settings, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SaveAsync_Throws_WhenAbnMalformed()
    {
        _settingsRepo.GetAsync(Arg.Any<CancellationToken>()).Returns((Settings?)null);
        var svc = CreateService();

        var settings = ValidSettings("12345678901");
        await Assert.ThrowsAsync<ValidationException>(() => svc.SaveAsync(settings, Ct));

        await _settingsRepo.DidNotReceive().SaveAsync(Arg.Any<Settings>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SaveAsync_Saves_WhenAbnValid()
    {
        _settingsRepo.GetAsync(Arg.Any<CancellationToken>()).Returns((Settings?)null);
        var svc = CreateService();

        var settings = ValidSettings("51824753556");
        await svc.SaveAsync(settings, Ct); // must not throw

        await _settingsRepo.Received(1).SaveAsync(settings, Arg.Any<CancellationToken>());
    }
}
