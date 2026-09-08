using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Exceptions;
using StageFright.Core.Modules.AuditTrail;
using StageFright.Core.Modules.Localization;
using StageFright.Core.Modules.Settings;
using StageFright.Core.Modules.Settings.Backup;
using StageFright.Data;
using StageFright.Data.Repositories;

namespace StageFright.Integration.Tests.Scenarios;

/// <summary>
/// Acceptance tests for spec 030 US1 — a confirmed first-run restore populates a clean install and,
/// after a restart, the app opens configured rather than into the wizard. Uses a real
/// <see cref="BackupService"/> and real SQLite:
/// <list type="bullet">
///   <item>after a successful restore a <c>Settings</c> row exists — the exact signal
///     <see cref="ISetupService.IsSetupCompleteAsync"/> and <c>App.razor.cs</c> route on, so the
///     next launch goes to <c>/dashboard</c>, not <c>/setup</c> (FR-006);</item>
///   <item>the restore is all-or-nothing — a file that fails validation leaves the fresh database
///     byte-for-byte as it was seeded (FR-005; the atomic PK-upsert transaction is the guarantee
///     for a mid-write fault — see <see cref="V9_BackupRestoreTests"/>);</item>
///   <item>the restored <see cref="Settings.LanguageCode"/> outranks the pre-setup no-database
///     language preference (spec Edge Case "language recorded outside the database").</item>
/// </list>
/// </summary>
public sealed class FirstRunRestoreJourneyTests : IAsyncLifetime
{
    private StageFrightDbContext _target = null!; // the clean install being restored into

    public async ValueTask InitializeAsync() => _target = await NewDatabaseAsync();

    public async ValueTask DisposeAsync()
    {
        await _target.Database.CloseConnectionAsync();
        await _target.DisposeAsync();
    }

    [Fact]
    public async Task ConfirmedFirstRunRestore_MakesSetupComplete_AndRestoredLanguageWins_Integration()
    {
        // --- a populated "outgoing treasurer" database, exported to a .sfbak file ---
        await using var source = await NewDatabaseAsync();
        source.Settings.Add(NewSettings(languageCode: "es-ES", currencyCode: "USD"));
        source.Members.Add(NewMember("Ada"));
        source.Members.Add(NewMember("Archived", deleted: true));
        await source.SaveChangesAsync(TestContext.Current.CancellationToken);

        var path = TempPath();
        try
        {
            await BuildService(source).ExportAsync(path, TestContext.Current.CancellationToken);

            // The clean install: no Settings row yet -> App.razor.cs would route to first-run.
            Assert.Null(await new SettingsRepository(_target).GetAsync(TestContext.Current.CancellationToken));

            await BuildService(_target).ImportAsync(path, TestContext.Current.CancellationToken);
            _target.ChangeTracker.Clear();

            // FR-006: a Settings row now exists -> IsSetupCompleteAsync() is true -> /dashboard, not /setup.
            Assert.NotNull(await new SettingsRepository(_target).GetAsync(TestContext.Current.CancellationToken));

            // FR-005 / FR-016: every source row present, archived state intact.
            Assert.Equal(2, await _target.Members.IgnoreQueryFilters().CountAsync(TestContext.Current.CancellationToken));
            Assert.True(await _target.Members.IgnoreQueryFilters()
                .AnyAsync(m => m.FirstName == "Archived" && m.IsDeleted, TestContext.Current.CancellationToken));

            // Edge Case "language recorded outside the database": a different no-database preference
            // was recorded during the aborted manual setup, but the restored Settings.LanguageCode wins.
            var preferenceStore = Substitute.For<ILanguagePreferenceStore>();
            preferenceStore.Get().Returns("fr-FR");
            var systemCulture = Substitute.For<ISystemCultureProvider>();
            systemCulture.GetUiCulture().Returns(CultureInfo.GetCultureInfo("de-DE"));

            var languageProvider = new LanguageProvider(
                new SettingsService(new SettingsRepository(_target), BuildAudit(_target), RealLocalizer.Instance),
                new SupportedLanguagesCatalog(), systemCulture, preferenceStore);

            var resolved = await languageProvider.ResolveStartupCultureAsync(TestContext.Current.CancellationToken);
            Assert.Equal("es-ES", resolved.Name);
        }
        finally
        {
            CleanupFiles(path);
        }
    }

    [Fact]
    public async Task FailedFirstRunRestore_LeavesTheFreshDatabaseExactlyAsSeeded_Integration()
    {
        // The clean install has only its freshly-seeded default state (one member stands in for it);
        // an aborted restore must leave it byte-for-byte unchanged and setup still incomplete (FR-005).
        _target.Members.Add(NewMember("Seed"));
        await _target.SaveChangesAsync(TestContext.Current.CancellationToken);
        var before = await SnapshotAsync(_target);

        await using var source = await NewDatabaseAsync();
        source.Settings.Add(NewSettings(languageCode: "es-ES", currencyCode: "USD"));
        source.Members.Add(NewMember("FromBackup"));
        await source.SaveChangesAsync(TestContext.Current.CancellationToken);

        var path = TempPath();
        try
        {
            await BuildService(source).ExportAsync(path, TestContext.Current.CancellationToken);

            // Tamper the file so completeness validation fails *before* any write (stands in for a
            // mid-restore fault — the atomic PK-upsert transaction is the guarantee either way).
            var envelope = ReadEnvelope(path);
            envelope.EntityCounts.Remove("Accounts");
            WriteEnvelope(path, envelope);

            await Assert.ThrowsAsync<ImportException>(
                () => BuildService(_target).ImportAsync(path, TestContext.Current.CancellationToken));
            _target.ChangeTracker.Clear();

            Assert.Equal(before, await SnapshotAsync(_target));
            Assert.Null(await new SettingsRepository(_target).GetAsync(TestContext.Current.CancellationToken));
        }
        finally
        {
            CleanupFiles(path);
        }
    }

    // --- helpers ---------------------------------------------------------------

    private static async Task<StageFrightDbContext> NewDatabaseAsync()
    {
        var options = new DbContextOptionsBuilder<StageFrightDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;
        var db = new StageFrightDbContext(options);
        await db.Database.OpenConnectionAsync();
        await db.Database.MigrateAsync();
        return db;
    }

    private static BackupService BuildService(StageFrightDbContext db) =>
        new(new BackupRepository(db), new UnitOfWork(db), BuildAudit(db),
            NullLogger<BackupService>.Instance, RealLocalizer.Instance, new TempRecoveryCopyStore());

    private static AuditTrailService BuildAudit(StageFrightDbContext db) =>
        new(new AuditTrailRepository(db), NullLogger<AuditTrailService>.Instance);

    /// <summary>Writes the pre-restore recovery copy into the system temp directory for the test.</summary>
    private sealed class TempRecoveryCopyStore : IRecoveryCopyStore
    {
        public string GetRecoveryDirectory() => Path.GetTempPath();
    }

    private static Settings NewSettings(string languageCode, string currencyCode)
    {
        var now = DateTime.UtcNow;
        return new Settings
        {
            Id = Guid.NewGuid(),
            OrganizationName = "Handover Choir",
            AnnualFee = 60m,
            AttendanceFee = 5m,
            MembershipRenewalMonth = 1,
            CommitteeRenewalMonth = 1,
            FinancialYearStartMonth = 7,
            MaxAgeRangeYears = 150,
            MinimumMemberAge = 0,
            Theme = Theme.Dark,
            LanguageCode = languageCode,
            CurrencyCode = currencyCode,
            SchemaVersion = "1.1.0",
            CreatedAt = now,
            UpdatedAt = now,
        };
    }

    private static Member NewMember(string name, bool deleted = false)
    {
        var now = DateTime.UtcNow;
        return new Member
        {
            Id = Guid.NewGuid(),
            FirstName = name,
            StreetAddress = "1 Test St",
            JoinDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            Status = deleted ? MemberStatus.Inactive : MemberStatus.Active,
            ActivateDate = deleted ? null : now,
            IsDeleted = deleted,
            DeletedAt = deleted ? now : null,
            DeletedBy = deleted ? "system" : null,
            CreatedAt = now,
            UpdatedAt = now,
        };
    }

    private static async Task<string> SnapshotAsync(StageFrightDbContext db)
    {
        var members = await db.Members.IgnoreQueryFilters().AsNoTracking()
            .OrderBy(m => m.FirstName).ToListAsync(TestContext.Current.CancellationToken);
        var counts = new Dictionary<string, int>
        {
            ["Members"] = members.Count,
            ["Accounts"] = await db.Accounts.IgnoreQueryFilters().CountAsync(TestContext.Current.CancellationToken),
            ["Settings"] = await db.Settings.CountAsync(TestContext.Current.CancellationToken),
            ["Transactions"] = await db.Transactions.CountAsync(TestContext.Current.CancellationToken),
        };
        return JsonSerializer.Serialize(new { members, counts });
    }

    private static string TempPath() =>
        Path.Combine(Path.GetTempPath(), $"sf_firstrun_{Guid.NewGuid()}.sfbak");

    private static BackupEnvelope ReadEnvelope(string path)
    {
        using var fs = File.OpenRead(path);
        return ProtoBuf.Serializer.Deserialize<BackupEnvelope>(fs);
    }

    private static void WriteEnvelope(string path, BackupEnvelope envelope)
    {
        using var fs = File.Create(path);
        ProtoBuf.Serializer.Serialize(fs, envelope);
    }

    private static void CleanupFiles(string primaryPath)
    {
        if (File.Exists(primaryPath)) File.Delete(primaryPath);
        var dir = Path.GetDirectoryName(primaryPath) ?? Path.GetTempPath();
        foreach (var f in Directory.GetFiles(dir, "StageFright-Recovery-*.sfbak"))
            try { File.Delete(f); } catch { /* best-effort */ }
    }
}
