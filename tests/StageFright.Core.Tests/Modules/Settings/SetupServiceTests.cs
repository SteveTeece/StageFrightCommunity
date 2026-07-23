using NSubstitute;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Exceptions;
using StageFright.Core.Modules.Settings;
using StageFright.Core.Tests.Fixtures;

namespace StageFright.Core.Tests.Setup;

/// <summary>
/// Unit tests for SetupService validation and initialization logic.
/// All validation tests assert the exact constraint (required fields, fee ≥ 0, month 1–12).
/// </summary>
public class SetupServiceTests : TestBase
{
    private readonly ISettingsRepository _settingsRepo = Substitute.For<ISettingsRepository>();
    private readonly IAccountRepository _accountRepo = Substitute.For<IAccountRepository>();
    private readonly IEventTypeRepository _eventTypeRepo = Substitute.For<IEventTypeRepository>();
    private readonly IAuditTrailService _audit = Substitute.For<IAuditTrailService>();

    public SetupServiceTests()
    {
        _eventTypeRepo.AddAsync(Arg.Any<StageFright.Core.Entities.EventType>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.ArgAt<StageFright.Core.Entities.EventType>(0));
    }

    private SetupService CreateService() => new(_settingsRepo, _accountRepo, _eventTypeRepo, _audit);

    // --- IsSetupCompleteAsync ---

    [Fact]
    public async Task IsSetupCompleteAsync_ReturnsFalse_WhenNoSettingsRecord()
    {
        _settingsRepo.GetAsync(Arg.Any<CancellationToken>()).Returns((Settings?)null);

        var svc = CreateService();
        Assert.False(await svc.IsSetupCompleteAsync(Ct));
    }

    [Fact]
    public async Task IsSetupCompleteAsync_ReturnsTrue_WhenSettingsExist()
    {
        _settingsRepo.GetAsync(Arg.Any<CancellationToken>()).Returns(new Settings { Id = Guid.NewGuid() });

        var svc = CreateService();
        Assert.True(await svc.IsSetupCompleteAsync(Ct));
    }

    // --- InitializeAsync validation ---

    [Fact]
    public async Task InitializeAsync_Throws_WhenOrganizationNameEmpty()
    {
        _settingsRepo.GetAsync(Arg.Any<CancellationToken>()).Returns((Settings?)null);
        var svc = CreateService();

        var request = ValidRequest() with { OrganizationName = "" };
        await Assert.ThrowsAsync<ValidationException>(() => svc.InitializeAsync(request, Ct));
    }

    [Fact]
    public async Task InitializeAsync_Throws_WhenAbnMissing()
    {
        _settingsRepo.GetAsync(Arg.Any<CancellationToken>()).Returns((Settings?)null);
        var svc = CreateService();

        var request = ValidRequest() with { Abn = "" };
        await Assert.ThrowsAsync<ValidationException>(() => svc.InitializeAsync(request, Ct));
    }

#if !DEBUG
    [Fact]
    public async Task InitializeAsync_Throws_WhenAbnChecksumInvalid()
    {
        _settingsRepo.GetAsync(Arg.Any<CancellationToken>()).Returns((Settings?)null);
        var svc = CreateService();

        var request = ValidRequest() with { Abn = "12345678901" };
        await Assert.ThrowsAsync<ValidationException>(() => svc.InitializeAsync(request, Ct));
    }
#else
    [Fact]
    public async Task InitializeAsync_AllowsAbnChecksumInvalid_InDebugBuild()
    {
        // ABN checksum validation is disabled in Debug builds (see SetupService.Validate)
        // so developers can complete setup without a real, checksum-valid ABN.
        _settingsRepo.GetAsync(Arg.Any<CancellationToken>()).Returns((Settings?)null);
        var svc = CreateService();

        var request = ValidRequest() with { Abn = "12345678901" };
        await svc.InitializeAsync(request, Ct);
    }
#endif

    [Fact]
    public async Task InitializeAsync_ForcesGstCodesNull_WhenNotRegistered()
    {
        _settingsRepo.GetAsync(Arg.Any<CancellationToken>()).Returns((Settings?)null);
        _accountRepo.GetNextAccountNumberAsync(Arg.Any<Core.Enums.AccountType>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns("4000");
        var svc = CreateService();

        var request = ValidRequest() with
        {
            IsGstRegistered = false,
            AnnualFeeGstCode = GstCode.Gst,
            AttendanceFeeGstCode = GstCode.Gst
        };
        await svc.InitializeAsync(request, Ct);

        await _settingsRepo.Received(1).SaveAsync(
            Arg.Is<Settings>(s => s.IsGstRegistered == false && s.AnnualFeeGstCode == null && s.AttendanceFeeGstCode == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InitializeAsync_PersistsGstCodes_WhenRegistered()
    {
        _settingsRepo.GetAsync(Arg.Any<CancellationToken>()).Returns((Settings?)null);
        _accountRepo.GetNextAccountNumberAsync(Arg.Any<Core.Enums.AccountType>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns("4000");
        var svc = CreateService();

        var request = ValidRequest() with
        {
            IsGstRegistered = true,
            AnnualFeeGstCode = GstCode.Gst,
            AttendanceFeeGstCode = GstCode.GstFree
        };
        await svc.InitializeAsync(request, Ct);

        await _settingsRepo.Received(1).SaveAsync(
            Arg.Is<Settings>(s => s.IsGstRegistered && s.AnnualFeeGstCode == GstCode.Gst && s.AttendanceFeeGstCode == GstCode.GstFree),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InitializeAsync_Throws_WhenAnnualFeeNegative()
    {
        _settingsRepo.GetAsync(Arg.Any<CancellationToken>()).Returns((Settings?)null);
        var svc = CreateService();

        var request = ValidRequest() with { AnnualFee = -1m };
        await Assert.ThrowsAsync<ValidationException>(() => svc.InitializeAsync(request, Ct));
    }

    [Fact]
    public async Task InitializeAsync_Allows_ZeroAnnualFee()
    {
        _settingsRepo.GetAsync(Arg.Any<CancellationToken>()).Returns((Settings?)null);
        _accountRepo.GetNextAccountNumberAsync(Arg.Any<Core.Enums.AccountType>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns("4000");
        var svc = CreateService();

        var request = ValidRequest() with { AnnualFee = 0m };
        await svc.InitializeAsync(request, Ct); // must not throw
    }

    [Fact]
    public async Task InitializeAsync_Throws_WhenAttendanceFeeNegative()
    {
        _settingsRepo.GetAsync(Arg.Any<CancellationToken>()).Returns((Settings?)null);
        var svc = CreateService();

        var request = ValidRequest() with { AttendanceFee = -0.01m };
        await Assert.ThrowsAsync<ValidationException>(() => svc.InitializeAsync(request, Ct));
    }

    [Fact]
    public async Task InitializeAsync_Throws_WhenRenewalMonthZero()
    {
        _settingsRepo.GetAsync(Arg.Any<CancellationToken>()).Returns((Settings?)null);
        var svc = CreateService();

        var request = ValidRequest() with { MembershipRenewalMonth = 0 };
        await Assert.ThrowsAsync<ValidationException>(() => svc.InitializeAsync(request, Ct));
    }

    [Fact]
    public async Task InitializeAsync_Throws_WhenRenewalMonthThirteen()
    {
        _settingsRepo.GetAsync(Arg.Any<CancellationToken>()).Returns((Settings?)null);
        var svc = CreateService();

        var request = ValidRequest() with { MembershipRenewalMonth = 13 };
        await Assert.ThrowsAsync<ValidationException>(() => svc.InitializeAsync(request, Ct));
    }

    [Fact]
    public async Task InitializeAsync_SavesSettings_WithCorrectValues()
    {
        _settingsRepo.GetAsync(Arg.Any<CancellationToken>()).Returns((Settings?)null);
        _accountRepo.GetNextAccountNumberAsync(Arg.Any<Core.Enums.AccountType>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns("4000");
        var svc = CreateService();

        var request = ValidRequest();
        await svc.InitializeAsync(request, Ct);

        await _settingsRepo.Received(1).SaveAsync(
            Arg.Is<Settings>(s => s.OrganizationName == "Test Org" && s.AnnualFee == 75m),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InitializeAsync_Throws_WhenAlreadySetup()
    {
        _settingsRepo.GetAsync(Arg.Any<CancellationToken>())
            .Returns(new Settings { Id = Guid.NewGuid() });
        var svc = CreateService();

        await Assert.ThrowsAsync<ValidationException>(
            () => svc.InitializeAsync(ValidRequest(), Ct));
    }

    [Theory]
    [InlineData(Theme.Light)]
    [InlineData(Theme.Dark)]
    public async Task InitializeAsync_PersistsRequestedTheme(Theme requestedTheme)
    {
        _settingsRepo.GetAsync(Arg.Any<CancellationToken>()).Returns((Settings?)null);
        _accountRepo.GetNextAccountNumberAsync(Arg.Any<Core.Enums.AccountType>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns("4000");
        var svc = CreateService();

        var request = ValidRequest() with { Theme = requestedTheme };
        await svc.InitializeAsync(request, Ct);

        await _settingsRepo.Received(1).SaveAsync(
            Arg.Is<Settings>(s => s.Theme == requestedTheme),
            Arg.Any<CancellationToken>());
    }

    private static SetupRequest ValidRequest() => new(
        OrganizationName: "Test Org",
        Abn: "51824753556",
        AnnualFee: 75m,
        AttendanceFee: 5m,
        MembershipRenewalMonth: 1,
        IsGstRegistered: false,
        AnnualFeeGstCode: null,
        AttendanceFeeGstCode: null,
        Theme: Theme.Dark);
}
