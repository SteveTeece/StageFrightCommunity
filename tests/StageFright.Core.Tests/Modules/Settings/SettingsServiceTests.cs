using NSubstitute;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
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
    public async Task SaveAsync_ClearsGstCodes_WhenIsGstRegisteredFalse()
    {
        // Regression for #282: un-registering GST post-setup must clear stale GST codes,
        // matching Settings.IsGstRegistered's own doc comment ("GST codes stay null").
        _settingsRepo.GetAsync(Arg.Any<CancellationToken>()).Returns((Settings?)null);
        var svc = CreateService();

        var settings = ValidSettings(null);
        settings.IsGstRegistered = false;
        settings.AnnualFeeGstCode = GstCode.Gst;
        settings.AttendanceFeeGstCode = GstCode.Gst;

        await svc.SaveAsync(settings, Ct);

        Assert.Null(settings.AnnualFeeGstCode);
        Assert.Null(settings.AttendanceFeeGstCode);
        await _settingsRepo.Received(1).SaveAsync(
            Arg.Is<Settings>(s => s.AnnualFeeGstCode == null && s.AttendanceFeeGstCode == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SaveAsync_PreservesGstCodes_WhenIsGstRegisteredTrue()
    {
        _settingsRepo.GetAsync(Arg.Any<CancellationToken>()).Returns((Settings?)null);
        var svc = CreateService();

        var settings = ValidSettings(null);
        settings.IsGstRegistered = true;
        settings.AnnualFeeGstCode = GstCode.Gst;
        settings.AttendanceFeeGstCode = GstCode.GstFree;

        await svc.SaveAsync(settings, Ct);

        Assert.Equal(GstCode.Gst, settings.AnnualFeeGstCode);
        Assert.Equal(GstCode.GstFree, settings.AttendanceFeeGstCode);
    }

#if !DEBUG
    [Fact]
    public async Task SaveAsync_Throws_WhenAbnMalformed()
    {
        _settingsRepo.GetAsync(Arg.Any<CancellationToken>()).Returns((Settings?)null);
        var svc = CreateService();

        var settings = ValidSettings("12345678901");
        await Assert.ThrowsAsync<ValidationException>(() => svc.SaveAsync(settings, Ct));

        await _settingsRepo.DidNotReceive().SaveAsync(Arg.Any<Settings>(), Arg.Any<CancellationToken>());
    }
#else
    [Fact]
    public async Task SaveAsync_AllowsMalformedAbn_InDebugBuild()
    {
        // ABN checksum validation is disabled in Debug builds (see Settings.Abn).
        _settingsRepo.GetAsync(Arg.Any<CancellationToken>()).Returns((Settings?)null);
        var svc = CreateService();

        var settings = ValidSettings("12345678901");
        await svc.SaveAsync(settings, Ct); // must not throw

        await _settingsRepo.Received(1).SaveAsync(settings, Arg.Any<CancellationToken>());
    }
#endif

    [Fact]
    public async Task SaveAsync_Saves_WhenAbnValid()
    {
        _settingsRepo.GetAsync(Arg.Any<CancellationToken>()).Returns((Settings?)null);
        var svc = CreateService();

        var settings = ValidSettings("51824753556");
        await svc.SaveAsync(settings, Ct); // must not throw

        await _settingsRepo.Received(1).SaveAsync(settings, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SaveAsync_Throws_WhenMinimumMemberAgeNegative()
    {
        _settingsRepo.GetAsync(Arg.Any<CancellationToken>()).Returns((Settings?)null);
        var svc = CreateService();

        var settings = ValidSettings(null);
        settings.MinimumMemberAge = -1;

        await Assert.ThrowsAsync<ValidationException>(() => svc.SaveAsync(settings, Ct));
        await _settingsRepo.DidNotReceive().SaveAsync(Arg.Any<Settings>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SaveAsync_Throws_WhenMaxAgeRangeYearsNegative()
    {
        _settingsRepo.GetAsync(Arg.Any<CancellationToken>()).Returns((Settings?)null);
        var svc = CreateService();

        var settings = ValidSettings(null);
        settings.MaxAgeRangeYears = -1;

        await Assert.ThrowsAsync<ValidationException>(() => svc.SaveAsync(settings, Ct));
        await _settingsRepo.DidNotReceive().SaveAsync(Arg.Any<Settings>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SaveAsync_Throws_WhenMinimumMemberAgeExceedsMaxAgeRangeYears()
    {
        _settingsRepo.GetAsync(Arg.Any<CancellationToken>()).Returns((Settings?)null);
        var svc = CreateService();

        var settings = ValidSettings(null);
        settings.MinimumMemberAge = 20;
        settings.MaxAgeRangeYears = 19;

        await Assert.ThrowsAsync<ValidationException>(() => svc.SaveAsync(settings, Ct));
        await _settingsRepo.DidNotReceive().SaveAsync(Arg.Any<Settings>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SaveAsync_Saves_WhenMinimumMemberAgeEqualsMaxAgeRangeYears()
    {
        _settingsRepo.GetAsync(Arg.Any<CancellationToken>()).Returns((Settings?)null);
        var svc = CreateService();

        var settings = ValidSettings(null);
        settings.MinimumMemberAge = 18;
        settings.MaxAgeRangeYears = 18;

        await svc.SaveAsync(settings, Ct); // must not throw
        await _settingsRepo.Received(1).SaveAsync(settings, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SaveAsync_Saves_WhenMinimumMemberAgeIsZero_AndMaxIsZero()
    {
        _settingsRepo.GetAsync(Arg.Any<CancellationToken>()).Returns((Settings?)null);
        var svc = CreateService();

        var settings = ValidSettings(null);
        settings.MinimumMemberAge = 0;
        settings.MaxAgeRangeYears = 0;

        await svc.SaveAsync(settings, Ct); // must not throw
        await _settingsRepo.Received(1).SaveAsync(settings, Arg.Any<CancellationToken>());
    }
}
