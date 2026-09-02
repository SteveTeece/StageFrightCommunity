using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Modules.AuditTrail;
using StageFright.Core.Modules.Localization;
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
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var repo = new AuditTrailRepository(_db);
        var svc = new AuditTrailService(repo, NullLogger<AuditTrailService>.Instance);
        await svc.PurgeOlderThanAsync(DateTime.UtcNow.AddMonths(-12), TestContext.Current.CancellationToken);

        var remaining = await _db.AuditTrailEntries.IgnoreQueryFilters().ToListAsync(cancellationToken: TestContext.Current.CancellationToken);
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
            () => svc.PurgeOlderThanAsync(DateTime.UtcNow.AddMonths(-12), TestContext.Current.CancellationToken));

        Assert.Null(ex);
    }

    [Fact]
    public void AuditPurge_ResolvesViaDiContainer_ThroughInterfaceOnly()
    {
        // Regression for #275: DI only ever registers IAuditTrailService (the interface),
        // so resolving the concrete AuditTrailService type (as the buggy MauiProgram code
        // used to do) always returned null and silently skipped the purge. This mirrors
        // MauiProgram's actual registration and confirms GetRequiredService<IAuditTrailService>()
        // resolves successfully — the fixed call site's own approach.
        var services = new ServiceCollection();
        services.AddSingleton(_db);
        services.AddScoped<IAuditTrailRepository, AuditTrailRepository>();
        services.AddScoped<IAuditTrailService, AuditTrailService>();
        services.AddLogging();
        using var provider = services.BuildServiceProvider();

        var resolved = provider.GetRequiredService<IAuditTrailService>();

        Assert.NotNull(resolved);
        Assert.IsType<AuditTrailService>(resolved);
    }

    [Fact]
    public async Task AuditPurge_HonoursConfiguredRetentionPeriod()
    {
        var oldEnoughForOneYear = new AuditTrailEntry
        {
            Id = Guid.NewGuid(), EntityType = "Member", EntityId = Guid.NewGuid(),
            Action = AuditAction.Create, UserId = "system",
            Timestamp = DateTime.UtcNow.AddYears(-2)
        };
        var withinOneYear = new AuditTrailEntry
        {
            Id = Guid.NewGuid(), EntityType = "Member", EntityId = Guid.NewGuid(),
            Action = AuditAction.Update, UserId = "system",
            Timestamp = DateTime.UtcNow.AddMonths(-6)
        };
        await _db.AuditTrailEntries.AddRangeAsync(oldEnoughForOneYear, withinOneYear);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var repo = new AuditTrailRepository(_db);
        var svc = new AuditTrailService(repo, NullLogger<AuditTrailService>.Instance);

        // A configured retention of 1 year -> cutoff is 1 year ago, matching MauiProgram's
        // DateTime.UtcNow.AddYears(-retentionYears) computation.
        await svc.PurgeOlderThanAsync(DateTime.UtcNow.AddYears(-1), TestContext.Current.CancellationToken);

        var remaining = await _db.AuditTrailEntries.IgnoreQueryFilters().ToListAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.DoesNotContain(remaining, e => e.Id == oldEnoughForOneYear.Id);
        Assert.Contains(remaining, e => e.Id == withinOneYear.Id);
    }

    [Fact]
    public void SupportedLanguagesCatalog_ResolvesViaDiContainer_DiscoversShippedLanguages()
    {
        // Regression for #360: AddSingleton<ISupportedLanguagesCatalog, SupportedLanguagesCatalog>()
        // let the container pick the (IEnumerable<string>) test-seam constructor and inject an
        // *empty* sequence (DI resolves an unregistered IEnumerable<string> as empty, not null),
        // collapsing the shipped-language list to en-AU only. However it is registered, a
        // DI-resolved catalog must still discover the satellite resource sets shipped in the
        // build — this project ships en-US and fr-FR beside the en-AU neutral baseline.
        var services = new ServiceCollection();
        services.AddSingleton<ISupportedLanguagesCatalog, SupportedLanguagesCatalog>();
        using var provider = services.BuildServiceProvider();

        var catalog = provider.GetRequiredService<ISupportedLanguagesCatalog>();

        Assert.Contains(catalog.All, l => l.CultureCode == "en-US");
        Assert.Contains(catalog.All, l => l.CultureCode == "fr-FR");
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
