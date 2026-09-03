using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using StageFright.Core.Contracts;
using StageFright.Core.Exceptions;
using StageFright.Core.Modules.AuditTrail;
using StageFright.Core.Modules.Settings;
using StageFright.Data;
using StageFright.Data.Repositories;

namespace StageFright.Integration.Tests.InternationalAccounting;

/// <summary>
/// Spec 028 US8 (FR-025, SC — "logged AND surfaced, never silently discarded"): a failed
/// startup audit-trail purge must not be swallowed. <see cref="AuditTrailService.PurgeOlderThanAsync"/>
/// now lets the failure propagate to the startup sequence, which records it into the retrievable
/// <see cref="IStartupDiagnosticService"/> state as a NON-fatal warning — the app still starts and
/// the user is not sent to the blocking recovery page.
/// </summary>
public sealed class PurgeFailureSurfacedTests : IAsyncLifetime
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

    private AuditTrailService CreateService() =>
        new(new AuditTrailRepository(_db), NullLogger<AuditTrailService>.Instance);

    [Fact]
    public async Task PurgeOlderThanAsync_PropagatesTheFailure_When_TheDatabaseErrors_Integration()
    {
        // Force a real data-access failure: remove the table the purge reads.
        await _db.Database.ExecuteSqlRawAsync("DROP TABLE AuditTrailEntries", TestContext.Current.CancellationToken);
        var svc = CreateService();

        // FR-025: the failure must NOT be silently discarded — it reaches the caller.
        await Assert.ThrowsAsync<DataAccessException>(
            () => svc.PurgeOlderThanAsync(DateTime.UtcNow.AddYears(-5), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task StartupPurgeFailure_IsRecordedIntoTheDiagnosticState_AsANonFatalWarning_Integration()
    {
        await _db.Database.ExecuteSqlRawAsync("DROP TABLE AuditTrailEntries", TestContext.Current.CancellationToken);
        var svc = CreateService();
        var diagnostics = new StartupDiagnosticService();

        // Mirror MauiProgram's startup purge block: on failure, log (omitted here) and surface.
        try
        {
            await svc.PurgeOlderThanAsync(DateTime.UtcNow.AddYears(-5), TestContext.Current.CancellationToken);
        }
        catch (Exception ex)
        {
            diagnostics.RecordWarning($"Audit trail purge failed during startup: {ex.Message}");
        }

        Assert.True(diagnostics.HasStartupWarning);
        Assert.False(string.IsNullOrWhiteSpace(diagnostics.StartupWarning));
        // Non-fatal: a purge failure must not trip the blocking startup-error recovery page.
        Assert.False(diagnostics.HasStartupError);
    }

    [Fact]
    public async Task PurgeOlderThanAsync_DoesNotThrow_When_TheDatabaseIsHealthy_Integration()
    {
        var svc = CreateService();

        var ex = await Record.ExceptionAsync(
            () => svc.PurgeOlderThanAsync(DateTime.UtcNow.AddYears(-5), TestContext.Current.CancellationToken));

        Assert.Null(ex);
    }

    [Fact]
    public void StartupDiagnosticService_ClearsTheWarning_When_ClearErrorIsCalled()
    {
        var diagnostics = new StartupDiagnosticService();
        diagnostics.RecordWarning("purge failed");

        Assert.True(diagnostics.HasStartupWarning);

        diagnostics.ClearError();

        Assert.False(diagnostics.HasStartupWarning);
        Assert.Null(diagnostics.StartupWarning);
    }
}
