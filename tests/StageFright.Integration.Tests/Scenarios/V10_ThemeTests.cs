using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using StageFright.Core.Enums;
using StageFright.Core.Modules.AuditTrail;
using StageFright.Core.Modules.Finance;
using StageFright.Core.Modules.Settings;
using StageFright.Data;
using StageFright.Data.Repositories;

namespace StageFright.Integration.Tests.Scenarios;

/// <summary>
/// Acceptance tests for V10 — Dark/Light Theme Support.
/// Covers: theme persisted in Settings; toggle updates DB; restart restores preference;
/// WCAG AA contrast verified for all app-defined colour tokens.
/// </summary>
public sealed class V10_ThemeTests : IAsyncLifetime
{
    private StageFrightDbContext _db = null!;

    public async ValueTask InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<StageFrightDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;
        _db = new StageFrightDbContext(options);
        await _db.Database.OpenConnectionAsync();
        await _db.Database.MigrateAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _db.Database.CloseConnectionAsync();
        await _db.DisposeAsync();
    }

    // --- Theme persistence ---

    [Theory]
    [InlineData(Theme.Light)]
    [InlineData(Theme.Dark)]
    public async Task DefaultTheme_MatchesRequestedTheme_AfterFirstRunSetup(Theme requestedTheme)
    {
        var svc = BuildSetupService();
        var request = new SetupRequest("Test Choir", 60m, 5m, 1, false, null, null, null, requestedTheme);

        await svc.InitializeAsync(request, TestContext.Current.CancellationToken);

        var settings = await new SettingsRepository(_db).GetAsync(TestContext.Current.CancellationToken);
        Assert.Equal(requestedTheme, settings!.Theme);
    }

    [Fact]
    public async Task SaveSettings_WithDarkTheme_PersistsTheme()
    {
        var settings = await SeedSettingsAsync(Theme.Light);
        settings.Theme = Theme.Dark;

        await BuildSettingsService().SaveAsync(settings, TestContext.Current.CancellationToken);

        _db.ChangeTracker.Clear();
        var reloaded = await _db.Settings.FirstAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(Theme.Dark, reloaded.Theme);
    }

    [Fact]
    public async Task SaveSettings_WithLightTheme_PersistsTheme()
    {
        var settings = await SeedSettingsAsync(Theme.Dark);
        settings.Theme = Theme.Light;

        await BuildSettingsService().SaveAsync(settings, TestContext.Current.CancellationToken);

        _db.ChangeTracker.Clear();
        var reloaded = await _db.Settings.FirstAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(Theme.Light, reloaded.Theme);
    }

    [Fact]
    public async Task Toggle_Light_To_Dark_PersistsAndReloads()
    {
        var settings = await SeedSettingsAsync(Theme.Light);

        // Simulate toggle: update Theme and save
        settings.Theme = Theme.Dark;
        var svc = BuildSettingsService();
        await svc.SaveAsync(settings, TestContext.Current.CancellationToken);

        // Simulate restart: read from fresh DbContext
        _db.ChangeTracker.Clear();
        var afterRestart = await svc.GetAsync(TestContext.Current.CancellationToken);
        Assert.Equal(Theme.Dark, afterRestart!.Theme);
    }

    [Fact]
    public async Task Toggle_Dark_To_Light_PersistsAndReloads()
    {
        var settings = await SeedSettingsAsync(Theme.Dark);
        settings.Theme = Theme.Light;

        var svc = BuildSettingsService();
        await svc.SaveAsync(settings, TestContext.Current.CancellationToken);

        _db.ChangeTracker.Clear();
        var afterRestart = await svc.GetAsync(TestContext.Current.CancellationToken);
        Assert.Equal(Theme.Light, afterRestart!.Theme);
    }

    // --- WCAG AA contrast validation ---

    [Theory]
    [InlineData(120, 35, 70)]  // Finance green HSL(120,35%,70%)
    [InlineData(0,   35, 70)]  // Finance red   HSL(0,35%,70%)
    [InlineData(120, 40, 70)]  // Committee badge light HSL(120,40%,70%)
    [InlineData(120, 35, 55)]  // Committee badge dark  HSL(120,35%,55%)
    public void AppColourTokens_PassWcagAA_WithDarkText(double h, double s, double l)
    {
        const double darkR = 26, darkG = 26, darkB = 26; // #1a1a1a

        var bg = HslToRgb(h, s, l);
        var dark = (R: (int)darkR, G: (int)darkG, B: (int)darkB);
        var ratio = ContrastRatio(bg, dark);

        Assert.True(ratio >= 4.5,
            $"HSL({h},{s}%,{l}%) with dark text: contrast {ratio:F2} < 4.5");
    }

    // --- Helpers ---

    private async Task<Core.Entities.Settings> SeedSettingsAsync(Theme theme)
    {
        var settings = new Core.Entities.Settings
        {
            Id = Guid.NewGuid(),
            OrganizationName = "Test Choir",
            AnnualFee = 60m,
            AttendanceFee = 5m,
            MembershipRenewalMonth = 1,
            CommitteeRenewalMonth = 1,
            MaxAgeRangeYears = 150,
            MinimumMemberAge = 0,
            Theme = theme,
            SchemaVersion = "1.0.0",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.Settings.Add(settings);
        await _db.SaveChangesAsync();
        return settings;
    }

    private SettingsService BuildSettingsService()
    {
        var repo = new SettingsRepository(_db);
        var auditRepo = new AuditTrailRepository(_db);
        var auditSvc = new AuditTrailService(auditRepo, NullLogger<AuditTrailService>.Instance);
        return new SettingsService(repo, auditSvc, RealLocalizer.Instance);
    }

    private SetupService BuildSetupService()
    {
        var settingsRepo = new SettingsRepository(_db);
        var accountRepo = new AccountRepository(_db);
        var eventTypeRepo = new EventTypeRepository(_db);
        var auditRepo = new AuditTrailRepository(_db);
        var auditService = new AuditTrailService(auditRepo, NullLogger<AuditTrailService>.Instance);
        var officeHolderTypeRepo = new CommitteeOfficeHolderTypeRepository(_db);
        var officeHolderTypeService = new StageFright.Core.Modules.Members.CommitteeOfficeHolderTypeService(officeHolderTypeRepo, auditService, RealLocalizer.Instance);
        var glAssignment = new AccountNumberAssignmentService(accountRepo);
        var reconciliationRepo = new BankReconciliationRepository(_db);
        var accountService = new AccountService(accountRepo, glAssignment, auditService, reconciliationRepo, RealLocalizer.Instance);
        var glRepo = new GLRepository(_db);
        var journalRepo = new JournalEntryRepository(_db);
        var unitOfWork = new UnitOfWork(_db);
        var openingBalanceService = new OpeningBalanceService(accountRepo, glRepo, journalRepo, auditService, unitOfWork, RealLocalizer.Instance);
        return new SetupService(settingsRepo, accountRepo, eventTypeRepo, officeHolderTypeService, accountService, openingBalanceService, auditService, RealLocalizer.Instance);
    }

    private static (int R, int G, int B) HslToRgb(double h, double s, double l)
    {
        s /= 100.0;
        l /= 100.0;
        double c = (1.0 - Math.Abs(2.0 * l - 1.0)) * s;
        double x = c * (1.0 - Math.Abs(h / 60.0 % 2.0 - 1.0));
        double m = l - c / 2.0;
        double r1, g1, b1;
        if      (h < 60)  { r1 = c; g1 = x; b1 = 0; }
        else if (h < 120) { r1 = x; g1 = c; b1 = 0; }
        else if (h < 180) { r1 = 0; g1 = c; b1 = x; }
        else if (h < 240) { r1 = 0; g1 = x; b1 = c; }
        else if (h < 300) { r1 = x; g1 = 0; b1 = c; }
        else              { r1 = c; g1 = 0; b1 = x; }
        return (
            (int)Math.Round((r1 + m) * 255.0),
            (int)Math.Round((g1 + m) * 255.0),
            (int)Math.Round((b1 + m) * 255.0)
        );
    }

    private static double RelativeLuminance((int R, int G, int B) color)
    {
        static double Lin(int c)
        {
            double v = c / 255.0;
            return v <= 0.04045 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4);
        }
        return 0.2126 * Lin(color.R) + 0.7152 * Lin(color.G) + 0.0722 * Lin(color.B);
    }

    private static double ContrastRatio(
        (int R, int G, int B) c1,
        (int R, int G, int B) c2)
    {
        double l1 = RelativeLuminance(c1);
        double l2 = RelativeLuminance(c2);
        return (Math.Max(l1, l2) + 0.05) / (Math.Min(l1, l2) + 0.05);
    }
}
