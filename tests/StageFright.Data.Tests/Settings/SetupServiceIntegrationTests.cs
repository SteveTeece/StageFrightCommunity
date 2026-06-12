using StageFright.Core.Entities;
using StageFright.Core.Modules.AuditTrail;
using StageFright.Core.Modules.Settings;
using StageFright.Data.Repositories;
using StageFright.Data.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace StageFright.Data.Tests.Setup;

/// <summary>
/// Integration tests for SetupService using an in-memory SQLite database.
/// Verifies Settings persistence, system category seeding, and zero Fee records post-setup.
/// </summary>
public class SetupServiceIntegrationTests : IDisposable
{
    private readonly DbContextFactory _factory = new();

    [Fact]
    public async Task InitializeAsync_PersistsSettingsWithCorrectValues()
    {
        using var db = _factory.CreateContext();
        var settingsRepo = new SettingsRepository(db);
        var categoryRepo = new CategoryRepository(db);
        var auditRepo = new AuditTrailRepository(db);
        var auditService = new AuditTrailService(auditRepo, NullLogger<AuditTrailService>.Instance);
        var svc = new SetupService(settingsRepo, categoryRepo, auditService);

        var request = new SetupRequest("My Choir", 80m, 6m, 3);
        await svc.InitializeAsync(request);

        var settings = await settingsRepo.GetAsync();
        Assert.NotNull(settings);
        Assert.Equal("My Choir", settings!.OrganizationName);
        Assert.Equal(80m, settings.AnnualFee);
        Assert.Equal(6m, settings.AttendanceFee);
        Assert.Equal(3, settings.MembershipRenewalMonth);
    }

    [Fact]
    public async Task InitializeAsync_SeedsSystemCategories()
    {
        using var db = _factory.CreateContext();
        var settingsRepo = new SettingsRepository(db);
        var categoryRepo = new CategoryRepository(db);
        var auditRepo = new AuditTrailRepository(db);
        var auditService = new AuditTrailService(auditRepo, NullLogger<AuditTrailService>.Instance);
        var svc = new SetupService(settingsRepo, categoryRepo, auditService);

        await svc.InitializeAsync(new SetupRequest("Org", 50m, 5m, 1));

        var all = await categoryRepo.GetAllAsync();
        Assert.Contains(all, c => c.GLAccount == "0100" && c.Name == "Cash" && c.IsSystem);
        Assert.Contains(all, c => c.GLAccount == "0101" && c.IsSystem);
        Assert.Contains(all, c => c.GLAccount == "9900" && c.IsSystem);
    }

    [Fact]
    public async Task InitializeAsync_CreatesZeroFeeRecords()
    {
        using var db = _factory.CreateContext();
        var settingsRepo = new SettingsRepository(db);
        var categoryRepo = new CategoryRepository(db);
        var auditRepo = new AuditTrailRepository(db);
        var auditService = new AuditTrailService(auditRepo, NullLogger<AuditTrailService>.Instance);
        var svc = new SetupService(settingsRepo, categoryRepo, auditService);

        await svc.InitializeAsync(new SetupRequest("Org", 50m, 5m, 1));

        var feeCount = await db.Fees.CountAsync();
        Assert.Equal(0, feeCount);
    }

    [Fact]
    public async Task IsSetupCompleteAsync_ReturnsFalse_OnEmptyDatabase()
    {
        using var db = _factory.CreateContext();
        var settingsRepo = new SettingsRepository(db);
        var categoryRepo = new CategoryRepository(db);
        var auditRepo = new AuditTrailRepository(db);
        var auditService = new AuditTrailService(auditRepo, NullLogger<AuditTrailService>.Instance);
        var svc = new SetupService(settingsRepo, categoryRepo, auditService);

        Assert.False(await svc.IsSetupCompleteAsync());
    }

    public void Dispose() => _factory.Dispose();
}
