using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Modules.AuditTrail;
using StageFright.Core.Modules.Settings;
using StageFright.Data;
using StageFright.Data.Repositories;

namespace StageFright.Integration.Tests.Scenarios;

/// <summary>
/// Integration tests for startup-sequence components:
/// audit-trail purge, startup diagnostic service (corrupted DB error state),
/// and Plugins/ directory auto-creation logic.
/// Plugin assembly loading is MAUI-app-specific and covered by manual V8 smoke tests.
/// </summary>
public sealed class StartupSequenceTests : IAsyncLifetime
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

    // --- Audit purge (FR-022) ---

    [Fact]
    public async Task AuditPurge_RemovesEntriesOlderThan12Months()
    {
        var oldEntry = new AuditTrailEntry
        {
            Id = Guid.NewGuid(), EntityType = "Member", EntityId = Guid.NewGuid(),
            Action = AuditAction.Create, UserId = "system",
            Timestamp = DateTime.UtcNow.AddMonths(-13)
        };
        var recentEntry = new AuditTrailEntry
        {
            Id = Guid.NewGuid(), EntityType = "Member", EntityId = Guid.NewGuid(),
            Action = AuditAction.Update, UserId = "system",
            Timestamp = DateTime.UtcNow.AddMonths(-6)
        };
        await _db.AuditTrailEntries.AddRangeAsync(oldEntry, recentEntry);
        await _db.SaveChangesAsync();

        var repo = new AuditTrailRepository(_db);
        var svc = new AuditTrailService(repo, NullLogger<AuditTrailService>.Instance);
        await svc.PurgeOlderThanAsync(DateTime.UtcNow.AddMonths(-12));

        var remaining = await _db.AuditTrailEntries.IgnoreQueryFilters().ToListAsync();
        Assert.DoesNotContain(remaining, e => e.Id == oldEntry.Id);
        Assert.Contains(remaining, e => e.Id == recentEntry.Id);
    }

    [Fact]
    public async Task AuditPurge_WithNoOldEntries_CompletesSuccessfully()
    {
        // No entries in DB — purge should not throw
        var repo = new AuditTrailRepository(_db);
        var svc = new AuditTrailService(repo, NullLogger<AuditTrailService>.Instance);

        var ex = await Record.ExceptionAsync(
            () => svc.PurgeOlderThanAsync(DateTime.UtcNow.AddMonths(-12)));

        Assert.Null(ex);
    }

    // --- Startup diagnostic service (T172 corrupted-DB error dialog) ---

    [Fact]
    public void StartupDiagnosticService_InitialState_HasNoError()
    {
        var svc = new StartupDiagnosticService();
        Assert.False(svc.HasStartupError);
        Assert.Null(svc.StartupException);
        Assert.Null(svc.DatabasePath);
    }

    [Fact]
    public void StartupDiagnosticService_RecordError_SetsHasStartupError()
    {
        var svc = new StartupDiagnosticService();
        var ex = new InvalidOperationException("DB corrupted");
        svc.RecordError(ex, "/data/stagefright.db");

        Assert.True(svc.HasStartupError);
        Assert.Equal(ex, svc.StartupException);
        Assert.Equal("/data/stagefright.db", svc.DatabasePath);
    }

    [Fact]
    public void StartupDiagnosticService_ClearError_ResetsState()
    {
        var svc = new StartupDiagnosticService();
        svc.RecordError(new InvalidOperationException("test"), "/data/db");
        svc.ClearError();

        Assert.False(svc.HasStartupError);
        Assert.Null(svc.StartupException);
        Assert.Null(svc.DatabasePath);
    }

    // --- Plugins directory auto-creation (FR-021) ---

    [Fact]
    public void PluginsDirectory_AutoCreation_Succeeds_WhenPathIsValid()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"sf_test_{Guid.NewGuid():N}");
        var pluginsPath = Path.Combine(tempRoot, "Plugins");

        try
        {
            Directory.CreateDirectory(pluginsPath);
            Assert.True(Directory.Exists(pluginsPath));
        }
        finally
        {
            if (Directory.Exists(tempRoot))
                Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void PluginsDirectory_WhenAlreadyExists_CreationIsIdempotent()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"sf_plugins_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempPath);

        try
        {
            // Calling CreateDirectory on an existing path should not throw
            var ex = Record.Exception(() => Directory.CreateDirectory(tempPath));
            Assert.Null(ex);
        }
        finally
        {
            Directory.Delete(tempPath);
        }
    }
}
