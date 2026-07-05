using NSubstitute;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
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

    private static SetupRequest ValidRequest() => new(
        OrganizationName: "Test Org",
        AnnualFee: 75m,
        AttendanceFee: 5m,
        MembershipRenewalMonth: 1);
}
