using NSubstitute;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Exceptions;
using StageFright.Core.Modules.Settings;
using StageFright.Core.Tests.Fixtures;

namespace StageFright.Core.Tests.Setup;

/// <summary>
/// Unit tests for SettingsService.SaveAsync's tax-applicability invariant: turning tax off
/// clears the rate and both per-fee tax codes; turning it on requires a positive rate.
/// </summary>
public class SettingsServiceTests : TestBase
{
    private readonly ISettingsRepository _settingsRepo = Substitute.For<ISettingsRepository>();
    private readonly IAuditTrailService _audit = Substitute.For<IAuditTrailService>();

    private SettingsService CreateService() => new(_settingsRepo, _audit, RealLocalizer.Instance);

    private static Settings ValidSettings() => new()
    {
        Id = Guid.NewGuid(),
        OrganizationName = "Test Org",
        AnnualFee = 75m,
        AttendanceFee = 5m,
        MembershipRenewalMonth = 1
    };

    [Fact]
    public async Task SaveAsync_ClearsTaxFields_WhenIsTaxApplicableFalse()
    {
        // Regression for #282 (originally GST): turning tax off post-setup must clear stale
        // rate/tax codes, matching Settings.IsTaxApplicable's own doc comment.
        _settingsRepo.GetAsync(Arg.Any<CancellationToken>()).Returns((Settings?)null);
        var svc = CreateService();

        var settings = ValidSettings();
        settings.IsTaxApplicable = false;
        settings.TaxRate = 10m;
        settings.AnnualFeeTaxCode = TaxCode.Taxable;
        settings.AttendanceFeeTaxCode = TaxCode.Taxable;

        await svc.SaveAsync(settings, Ct);

        Assert.Null(settings.TaxRate);
        Assert.Null(settings.AnnualFeeTaxCode);
        Assert.Null(settings.AttendanceFeeTaxCode);
        await _settingsRepo.Received(1).SaveAsync(
            Arg.Is<Settings>(s => s!.TaxRate == null && s.AnnualFeeTaxCode == null && s.AttendanceFeeTaxCode == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SaveAsync_PreservesTaxFields_WhenIsTaxApplicableTrue()
    {
        _settingsRepo.GetAsync(Arg.Any<CancellationToken>()).Returns((Settings?)null);
        var svc = CreateService();

        var settings = ValidSettings();
        settings.IsTaxApplicable = true;
        settings.TaxRate = 15m;
        settings.AnnualFeeTaxCode = TaxCode.Taxable;
        settings.AttendanceFeeTaxCode = TaxCode.TaxExempt;

        await svc.SaveAsync(settings, Ct);

        Assert.Equal(15m, settings.TaxRate);
        Assert.Equal(TaxCode.Taxable, settings.AnnualFeeTaxCode);
        Assert.Equal(TaxCode.TaxExempt, settings.AttendanceFeeTaxCode);
    }

    [Fact]
    public async Task SaveAsync_Throws_WhenTaxApplicableWithoutRate()
    {
        _settingsRepo.GetAsync(Arg.Any<CancellationToken>()).Returns((Settings?)null);
        var svc = CreateService();

        var settings = ValidSettings();
        settings.IsTaxApplicable = true;
        settings.TaxRate = null;

        await Assert.ThrowsAsync<ValidationException>(() => svc.SaveAsync(settings, Ct));
        await _settingsRepo.DidNotReceive().SaveAsync(Arg.Any<Settings>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SaveAsync_Throws_WhenTaxApplicableWithNonPositiveRate()
    {
        _settingsRepo.GetAsync(Arg.Any<CancellationToken>()).Returns((Settings?)null);
        var svc = CreateService();

        var settings = ValidSettings();
        settings.IsTaxApplicable = true;
        settings.TaxRate = 0m;

        await Assert.ThrowsAsync<ValidationException>(() => svc.SaveAsync(settings, Ct));
        await _settingsRepo.DidNotReceive().SaveAsync(Arg.Any<Settings>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SaveAsync_Saves_WhenTaxNotApplicable()
    {
        _settingsRepo.GetAsync(Arg.Any<CancellationToken>()).Returns((Settings?)null);
        var svc = CreateService();

        var settings = ValidSettings();
        await svc.SaveAsync(settings, Ct); // must not throw

        await _settingsRepo.Received(1).SaveAsync(settings, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SaveAsync_Throws_WhenMinimumMemberAgeNegative()
    {
        _settingsRepo.GetAsync(Arg.Any<CancellationToken>()).Returns((Settings?)null);
        var svc = CreateService();

        var settings = ValidSettings();
        settings.MinimumMemberAge = -1;

        await Assert.ThrowsAsync<ValidationException>(() => svc.SaveAsync(settings, Ct));
        await _settingsRepo.DidNotReceive().SaveAsync(Arg.Any<Settings>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SaveAsync_Throws_WhenMaxAgeRangeYearsNegative()
    {
        _settingsRepo.GetAsync(Arg.Any<CancellationToken>()).Returns((Settings?)null);
        var svc = CreateService();

        var settings = ValidSettings();
        settings.MaxAgeRangeYears = -1;

        await Assert.ThrowsAsync<ValidationException>(() => svc.SaveAsync(settings, Ct));
        await _settingsRepo.DidNotReceive().SaveAsync(Arg.Any<Settings>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SaveAsync_Throws_WhenMinimumMemberAgeExceedsMaxAgeRangeYears()
    {
        _settingsRepo.GetAsync(Arg.Any<CancellationToken>()).Returns((Settings?)null);
        var svc = CreateService();

        var settings = ValidSettings();
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

        var settings = ValidSettings();
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

        var settings = ValidSettings();
        settings.MinimumMemberAge = 0;
        settings.MaxAgeRangeYears = 0;

        await svc.SaveAsync(settings, Ct); // must not throw
        await _settingsRepo.Received(1).SaveAsync(settings, Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(8)]
    public async Task SaveAsync_Throws_WhenAuditRetentionYearsOutOfRange(int years)
    {
        _settingsRepo.GetAsync(Arg.Any<CancellationToken>()).Returns((Settings?)null);
        var svc = CreateService();

        var settings = ValidSettings();
        settings.AuditRetentionYears = years;

        await Assert.ThrowsAsync<ValidationException>(() => svc.SaveAsync(settings, Ct));
        await _settingsRepo.DidNotReceive().SaveAsync(Arg.Any<Settings>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    public async Task SaveAsync_Saves_WhenAuditRetentionYearsAtBoundary(int years)
    {
        _settingsRepo.GetAsync(Arg.Any<CancellationToken>()).Returns((Settings?)null);
        var svc = CreateService();

        var settings = ValidSettings();
        settings.AuditRetentionYears = years;

        await svc.SaveAsync(settings, Ct); // must not throw
        await _settingsRepo.Received(1).SaveAsync(settings, Arg.Any<CancellationToken>());
    }

    // --- Currency immutability (spec 028, US1 / FR-002) ---

    [Fact]
    public async Task SaveAsync_Throws_WhenCurrencyCodeDiffersFromPersisted()
    {
        var persisted = ValidSettings();
        persisted.CurrencyCode = "AUD";
        _settingsRepo.GetAsync(Arg.Any<CancellationToken>()).Returns(persisted);
        var svc = CreateService();

        var settings = ValidSettings();
        settings.CurrencyCode = "USD";

        await Assert.ThrowsAsync<ValidationException>(() => svc.SaveAsync(settings, Ct));
        await _settingsRepo.DidNotReceive().SaveAsync(Arg.Any<Settings>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SaveAsync_Saves_WhenCurrencyCodeUnchanged()
    {
        var persisted = ValidSettings();
        persisted.CurrencyCode = "JPY";
        _settingsRepo.GetAsync(Arg.Any<CancellationToken>()).Returns(persisted);
        var svc = CreateService();

        var settings = ValidSettings();
        settings.CurrencyCode = "jpy"; // case-insensitive match, still allowed

        await svc.SaveAsync(settings, Ct); // must not throw
        await _settingsRepo.Received(1).SaveAsync(settings, Arg.Any<CancellationToken>());
    }
}
