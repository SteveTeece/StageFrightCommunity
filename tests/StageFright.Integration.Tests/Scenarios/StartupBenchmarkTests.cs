using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using StageFright.Core.Modules.AuditTrail;
using StageFright.Core.Modules.Settings;
using StageFright.Data;
using StageFright.Data.Repositories;
using Xunit.Abstractions;

namespace StageFright.Integration.Tests.Scenarios;

/// <summary>
/// Advisory startup-performance benchmark tests per SC-002.
/// Measures the time taken for key startup operations (database migration and
/// first-run detection) and logs results via <see cref="ITestOutputHelper"/> for
/// CI observability. No hard timing assertions are made; the 3-second advisory
/// target is noted in comments only, as required by NFR-003.
/// </summary>
/// <remarks>
/// SC-002: Dashboard displays all four core tiles within 3 seconds of application
/// startup on a typical development machine (advisory benchmark, not an SLA).
/// NFR-003: No numeric SLAs are mandated for MVP acceptance.
/// </remarks>
public sealed class StartupBenchmarkTests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private StageFrightDbContext _db = null!;

    public StartupBenchmarkTests(ITestOutputHelper output)
    {
        _output = output;
    }

    public async Task InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<StageFrightDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;
        _db = new StageFrightDbContext(options);
        await _db.Database.OpenConnectionAsync();
        await _db.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await _db.Database.CloseConnectionAsync();
        await _db.DisposeAsync();
    }

    private SetupService BuildSetupService(StageFrightDbContext ctx)
    {
        var settingsRepo = new SettingsRepository(ctx);
        var categoryRepo = new CategoryRepository(ctx);
        var eventTypeRepo = new EventTypeRepository(ctx);
        var auditRepo = new AuditTrailRepository(ctx);
        var auditSvc = new AuditTrailService(auditRepo, NullLogger<AuditTrailService>.Instance);
        return new SetupService(settingsRepo, categoryRepo, eventTypeRepo, auditSvc);
    }

    /// <summary>
    /// Measures the time for EF Core migration on a fresh in-memory database,
    /// representing the database-initialisation portion of the startup sequence.
    /// Advisory target per SC-002: completes well within the 3-second total budget.
    /// </summary>
    [Fact]
    public async Task Should_CompleteDbMigration_WithinAdvisoryStartupBudget()
    {
        var options = new DbContextOptionsBuilder<StageFrightDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        var sw = Stopwatch.StartNew();

        // Simulate the database-initialisation phase of MauiProgram startup
        await using var ctx = new StageFrightDbContext(options);
        await ctx.Database.OpenConnectionAsync();
        await ctx.Database.MigrateAsync();

        sw.Stop();

        // Log advisory result — no hard gate per NFR-003
        _output.WriteLine(
            $"[SC-002 Advisory] DB migration elapsed: {sw.ElapsedMilliseconds} ms " +
            $"(advisory budget: ≤3000 ms total startup; migration is one sub-component)");

        // Timing is advisory only; assert only that measurement is valid
        Assert.True(sw.ElapsedMilliseconds >= 0,
            "Stopwatch must return a non-negative elapsed time.");
    }

    /// <summary>
    /// Measures the time for first-run detection (<see cref="SetupService.IsSetupCompleteAsync"/>),
    /// the first application-logic step executed after migration in the startup sequence.
    /// Advisory target per SC-002: sub-millisecond on a typical development machine.
    /// </summary>
    [Fact]
    public async Task Should_CompleteFirstRunDetection_WithinAdvisoryStartupBudget()
    {
        var setupSvc = BuildSetupService(_db);

        var sw = Stopwatch.StartNew();

        // Simulate the first-run detection phase executed in App.razor OnInitializedAsync
        var isComplete = await setupSvc.IsSetupCompleteAsync();

        sw.Stop();

        // Log advisory result — no hard gate per NFR-003
        _output.WriteLine(
            $"[SC-002 Advisory] First-run detection elapsed: {sw.ElapsedMilliseconds} ms " +
            $"(setup complete: {isComplete}; advisory budget: ≤3000 ms total startup)");

        Assert.True(sw.ElapsedMilliseconds >= 0,
            "Stopwatch must return a non-negative elapsed time.");
    }

    /// <summary>
    /// Measures the combined startup critical path — migration + first-run detection —
    /// as a composite observable baseline for the SC-002 3-second advisory target.
    /// Results are written to the test output for CI inspection without enforcing a hard SLA.
    /// </summary>
    [Fact]
    public async Task Should_LogCombinedStartupCriticalPath_AsAdvisoryBaseline()
    {
        var options = new DbContextOptionsBuilder<StageFrightDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        var totalSw = Stopwatch.StartNew();

        // Phase 1: DB migration (mirrors MauiProgram.cs startup sequence)
        var migrationSw = Stopwatch.StartNew();
        await using var ctx = new StageFrightDbContext(options);
        await ctx.Database.OpenConnectionAsync();
        await ctx.Database.MigrateAsync();
        migrationSw.Stop();

        // Phase 2: first-run detection (mirrors App.razor OnInitializedAsync)
        var detectionSw = Stopwatch.StartNew();
        var setupSvc = BuildSetupService(ctx);
        var isComplete = await setupSvc.IsSetupCompleteAsync();
        detectionSw.Stop();

        totalSw.Stop();

        // Log breakdown (advisory only — SC-002 / NFR-003)
        _output.WriteLine("=== SC-002 Startup Benchmark (Advisory Only — NFR-003) ===");
        _output.WriteLine($"  DB migration:      {migrationSw.ElapsedMilliseconds,6} ms");
        _output.WriteLine($"  First-run detect:  {detectionSw.ElapsedMilliseconds,6} ms");
        _output.WriteLine($"  Combined total:    {totalSw.ElapsedMilliseconds,6} ms");
        _output.WriteLine($"  Advisory budget:  ≤3000 ms (full startup including MAUI/Blazor init)");
        _output.WriteLine($"  Setup complete:    {isComplete}");
        _output.WriteLine("=== End Advisory Benchmark ===");

        // No hard assertion on timing per NFR-003 ("no numeric SLAs are mandated").
        Assert.True(totalSw.ElapsedMilliseconds >= 0,
            "Benchmark must produce a non-negative elapsed time.");
    }
}
