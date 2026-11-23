using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ProtoBuf;
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
/// Acceptance tests for spec 030 US2 — the Settings backup path, driven end-to-end with a real
/// <see cref="BackupService"/> + real SQLite and a fake <see cref="IBackupDestinationPicker"/> in
/// place of the OS-native Save dialog:
/// <list type="bullet">
///   <item><see cref="IBackupService.CreateBackupAsync"/> writes the file to the chosen path under
///     the <see cref="BackupFileNameBuilder"/> default name, reads it back, verifies the counts, and
///     records an <see cref="AuditAction.Export"/> audit entry (FR-009, FR-010, FR-017,
///     FR-021–FR-024);</item>
///   <item>a corrupted written file fails verification — the same failure state the Settings tab
///     renders as "do not rely on this file" (FR-023; the <c>.backup-verify-result</c> markup and
///     the post-restore <c>/restart-required</c> navigation are covered by
///     <c>BackupRestoreTabTests</c>);</item>
///   <item>a valid <c>.sfbak</c> restores into a fresh database (FR-018).</item>
/// </list>
/// </summary>
public sealed class SettingsBackupJourneyTests : IAsyncLifetime
{
    private StageFrightDbContext _db = null!;

    public async ValueTask InitializeAsync() => _db = await NewDatabaseAsync();

    public async ValueTask DisposeAsync()
    {
        await _db.Database.CloseConnectionAsync();
        await _db.DisposeAsync();
    }

    [Fact]
    public async Task CreateBackup_WritesAVerifiedFileToTheChosenPath_AndAuditsAnExport_Integration()
    {
        SeedSettings("Journey Choir");
        _db.Members.Add(SeedMember("Alto One"));
        _db.Members.Add(SeedMember("Bass Two"));
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var picker = new FakeDestinationPicker();
        var svc = BuildService(_db, picker);

        try
        {
            var result = await svc.CreateBackupAsync(TestContext.Current.CancellationToken);

            Assert.True(result.Passed);
            Assert.Empty(result.Discrepancies);
            Assert.Equal(picker.SavedPath, result.FilePath);
            Assert.True(File.Exists(result.FilePath));

            // FR-010 default name: "<org> backup <yyyy-MM-dd>.sfbak".
            Assert.StartsWith("Journey Choir backup ", picker.SuggestedFileName);
            Assert.Matches(@"\d{4}-\d{2}-\d{2}\.sfbak$", picker.SuggestedFileName!);

            // FR-017: a verified backup is audited as an Export.
            var exportEntries = await _db.AuditTrailEntries
                .Where(a => a.Action == AuditAction.Export)
                .ToListAsync(TestContext.Current.CancellationToken);
            Assert.Single(exportEntries);
        }
        finally
        {
            picker.Cleanup();
        }
    }

    [Fact]
    public async Task CreateBackup_WhenTheWrittenFileIsCorrupted_ReportsFailureWithDiscrepancies_Integration()
    {
        SeedSettings("Journey Choir");
        _db.Members.Add(SeedMember("Alto One"));
        _db.Members.Add(SeedMember("Bass Two"));
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var picker = new FakeDestinationPicker
        {
            // The file that lands on disk records a member count nothing else agrees with.
            Corrupt = bytes =>
            {
                using var inMs = new MemoryStream(bytes);
                var env = Serializer.Deserialize<BackupEnvelope>(inMs);
                env.EntityCounts["Members"] = 999;
                using var outMs = new MemoryStream();
                Serializer.Serialize(outMs, env);
                return outMs.ToArray();
            },
        };
        var svc = BuildService(_db, picker);

        try
        {
            var ex = await Assert.ThrowsAsync<BackupVerificationException>(
                () => svc.CreateBackupAsync(TestContext.Current.CancellationToken));

            Assert.NotEmpty(ex.Discrepancies);
            Assert.Contains(ex.Discrepancies, d => d!.Contains("Members"));
            Assert.True(File.Exists(ex.FilePath));   // left on disk for diagnosis

            var exportEntries = await _db.AuditTrailEntries
                .Where(a => a.Action == AuditAction.Export)
                .ToListAsync(TestContext.Current.CancellationToken);
            Assert.Empty(exportEntries);
        }
        finally
        {
            picker.Cleanup();
        }
    }

    [Fact]
    public async Task SettingsPathRestore_OfAValidBackup_RestoresTheData_Integration()
    {
        SeedSettings("Journey Choir");
        var member = SeedMember("Soprano Lead");
        _db.Members.Add(member);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var picker = new FakeDestinationPicker();
        await BuildService(_db, picker).CreateBackupAsync(TestContext.Current.CancellationToken);
        var backupPath = picker.SavedPath!;

        var target = await NewDatabaseAsync();
        try
        {
            await BuildService(target, new FakeDestinationPicker())
                .ImportAsync(backupPath, TestContext.Current.CancellationToken);

            target.ChangeTracker.Clear();
            var restoredSettings = await target.Settings.SingleAsync(TestContext.Current.CancellationToken);
            Assert.Equal("Journey Choir", restoredSettings.OrganizationName);
            Assert.True(await target.Members.IgnoreQueryFilters()
                .AnyAsync(m => m.Id == member.Id, TestContext.Current.CancellationToken));
        }
        finally
        {
            await target.Database.CloseConnectionAsync();
            await target.DisposeAsync();
            picker.Cleanup();
        }
    }

    // --- Helpers ---

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

    private static BackupService BuildService(StageFrightDbContext db, IBackupDestinationPicker picker) =>
        new(new BackupRepository(db), new UnitOfWork(db),
            new AuditTrailService(new AuditTrailRepository(db), NullLogger<AuditTrailService>.Instance),
            NullLogger<BackupService>.Instance, RealLocalizer.Instance, new TempRecoveryCopyStore(), picker);

    private void SeedSettings(string organisationName) => _db.Settings.Add(new Settings
    {
        Id = Guid.NewGuid(), OrganizationName = organisationName, AnnualFee = 60m, AttendanceFee = 5m,
        MembershipRenewalMonth = 1, SchemaVersion = "1.1.0",
        CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
    });

    private static Member SeedMember(string firstName) => new()
    {
        Id = Guid.NewGuid(), FirstName = firstName, StreetAddress = "1 Test St",
        JoinDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc), Status = MemberStatus.Active,
        CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
    };

    /// <summary>Writes the pre-restore recovery copy into the system temp directory for the test.</summary>
    private sealed class TempRecoveryCopyStore : IRecoveryCopyStore
    {
        public string GetRecoveryDirectory() => Path.GetTempPath();
    }

    /// <summary>Stands in for the OS-native Save dialog: writes the backup to the temp directory.</summary>
    private sealed class FakeDestinationPicker : IBackupDestinationPicker
    {
        public Func<byte[], byte[]>? Corrupt { get; set; }
        public string? SavedPath { get; private set; }
        public string? SuggestedFileName { get; private set; }

        public async Task<BackupDestinationResult> SaveAsync(string suggestedFileName, Stream content, CancellationToken ct = default)
        {
            SuggestedFileName = suggestedFileName;

            using var ms = new MemoryStream();
            await content.CopyToAsync(ms, ct);
            var bytes = Corrupt is null ? ms.ToArray() : Corrupt(ms.ToArray());

            SavedPath = Path.Combine(Path.GetTempPath(), $"sf_settings_journey_{Guid.NewGuid()}.sfbak");
            await File.WriteAllBytesAsync(SavedPath, bytes, ct);
            return BackupDestinationResult.Saved(SavedPath);
        }

        public void Cleanup()
        {
            if (SavedPath is not null && File.Exists(SavedPath)) File.Delete(SavedPath);
            foreach (var f in Directory.GetFiles(Path.GetTempPath(), "StageFright-Recovery-*.sfbak"))
                try { File.Delete(f); } catch { /* best-effort */ }
        }
    }
}
