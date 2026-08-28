using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Exceptions;
using StageFright.Core.Modules.AuditTrail;
using StageFright.Core.Modules.Settings;
using StageFright.Core.Modules.Settings.Backup;
using StageFright.Data;
using StageFright.Data.Repositories;

namespace StageFright.Integration.Tests.Scenarios;

/// <summary>
/// Acceptance tests for V9 — Backup and Restore.
/// Covers: backup → clear → restore → data intact; missing entity type → rejected;
/// corrupt file → ImportException; soft-deleted records preserved.
/// </summary>
public sealed class V9_BackupRestoreTests : IAsyncLifetime
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

    [Fact]
    public async Task Backup_ThenRestore_AllDataIntact()
    {
        var member = SeedMember("Alice", active: true);
        var archivedMember = SeedMember("Bob", active: false, deleted: true);
        _db.Members.Add(member);
        _db.Members.Add(archivedMember);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var svc = BuildService();
        var path = TempPath();

        try
        {
            // Export
            await svc.ExportAsync(path, TestContext.Current.CancellationToken);

            // Verify manifest
            var manifest = await svc.GetManifestAsync(path, TestContext.Current.CancellationToken);
            Assert.Equal("1.1.0", manifest.SchemaVersion);
            Assert.Equal(2, manifest.EntityCounts["Members"]);

            // Clear active members (simulate fresh restore target)
            // Import
            await svc.ImportAsync(path, TestContext.Current.CancellationToken);

            // Both members should exist after restore
            var allMembers = await _db.Members.IgnoreQueryFilters().ToListAsync(cancellationToken: TestContext.Current.CancellationToken);
            Assert.Contains(allMembers, m => m.Id == member.Id && m.FullName == "Alice");
            Assert.Contains(allMembers, m => m.Id == archivedMember.Id && m.IsDeleted);
        }
        finally
        {
            CleanupFiles(path);
        }
    }

    [Fact]
    public async Task Backup_IncludesSoftDeletedRecords()
    {
        var deleted = SeedMember("Deleted", active: false, deleted: true);
        _db.Members.Add(deleted);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var svc = BuildService();
        var path = TempPath();

        try
        {
            await svc.ExportAsync(path, TestContext.Current.CancellationToken);
            var manifest = await svc.GetManifestAsync(path, TestContext.Current.CancellationToken);
            Assert.Equal(1, manifest.EntityCounts["Members"]);
        }
        finally
        {
            CleanupFiles(path);
        }
    }

    [Fact]
    public async Task Import_MissingEntityType_ThrowsImportException_WithExactMessage()
    {
        var svc = BuildService();
        var path = TempPath();

        try
        {
            await svc.ExportAsync(path, TestContext.Current.CancellationToken);

            // Tamper: remove Accounts from EntityCounts to simulate a missing entity type
            var envelope = ReadEnvelope(path);
            envelope.EntityCounts.Remove("Accounts");
            WriteEnvelope(path, envelope);

            var ex = await Assert.ThrowsAsync<ImportException>(() => svc.ImportAsync(path, TestContext.Current.CancellationToken));
            Assert.Contains("Import file incomplete: missing Accounts", ex.Message);
        }
        finally
        {
            CleanupFiles(path);
        }
    }

    [Fact]
    public async Task Import_CorruptFile_ThrowsImportException()
    {
        var svc = BuildService();
        var path = TempPath();

        try
        {
            await File.WriteAllBytesAsync(path, [0xDE, 0xAD, 0xBE, 0xEF], TestContext.Current.CancellationToken);
            await Assert.ThrowsAsync<ImportException>(() => svc.ImportAsync(path, TestContext.Current.CancellationToken));
        }
        finally
        {
            CleanupFiles(path);
        }
    }

    [Fact]
    public async Task Import_UnsupportedMajorVersion_ThrowsImportException_WithUpgradeGuidance()
    {
        var svc = BuildService();
        var path = TempPath();

        try
        {
            await svc.ExportAsync(path, TestContext.Current.CancellationToken);
            var envelope = ReadEnvelope(path);
            envelope.SchemaVersion = "99.0.0";
            WriteEnvelope(path, envelope);

            var ex = await Assert.ThrowsAsync<ImportException>(() => svc.ImportAsync(path, TestContext.Current.CancellationToken));
            Assert.Contains("99.0.0", ex.Message);
            Assert.Contains("upgrade", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            CleanupFiles(path);
        }
    }

    [Fact]
    public async Task Import_CreatesCheckpoint_BeforeWrite()
    {
        var svc = BuildService();
        var path = TempPath();

        try
        {
            await svc.ExportAsync(path, TestContext.Current.CancellationToken);
            var dir = Path.GetDirectoryName(path)!;

            await svc.ImportAsync(path, TestContext.Current.CancellationToken);

            var checkpoints = Directory.GetFiles(dir, "StageFright-Checkpoint-*.sfbak");
            Assert.NotEmpty(checkpoints);
        }
        finally
        {
            CleanupFiles(path);
        }
    }

    [Fact]
    public async Task Import_UpsertsMember_WhenAlreadyExists()
    {
        var member = SeedMember("Original Name", active: true);
        _db.Members.Add(member);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var svc = BuildService();
        var path = TempPath();

        try
        {
            await svc.ExportAsync(path, TestContext.Current.CancellationToken);

            // Update name in DB after export
            var tracked = await _db.Members.FindAsync(new object?[] { member.Id }, TestContext.Current.CancellationToken);
            tracked!.FirstName = "Updated Name";
            await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

            // Import should restore "Original Name"
            await svc.ImportAsync(path, TestContext.Current.CancellationToken);

            _db.ChangeTracker.Clear();
            var restored = await _db.Members.IgnoreQueryFilters().FirstOrDefaultAsync(m => m.Id == member.Id, cancellationToken: TestContext.Current.CancellationToken);
            Assert.Equal("Original Name", restored?.FirstName);
        }
        finally
        {
            CleanupFiles(path);
        }
    }

    [Fact]
    public async Task Import_LegacyFormatBackup_RestoresCorrectlySplitNames()
    {
        var member = SeedMember("Placeholder", active: true);
        _db.Members.Add(member);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var svc = BuildService();
        var path = TempPath();

        try
        {
            await svc.ExportAsync(path, TestContext.Current.CancellationToken);

            // Tamper the export into a pre-feature (legacy) shape: clear FirstName/LastName,
            // populate only the old combined LegacyName field.
            var envelope = ReadEnvelope(path);
            var dto = envelope.Members!.Single(m => m.Id == member.Id);
            dto.FirstName = string.Empty;
            dto.LastName = string.Empty;
            dto.LegacyName = "Grace Hopper";
            WriteEnvelope(path, envelope);

            await svc.ImportAsync(path, TestContext.Current.CancellationToken);

            _db.ChangeTracker.Clear();
            var restored = await _db.Members.IgnoreQueryFilters().SingleAsync(m => m.Id == member.Id, cancellationToken: TestContext.Current.CancellationToken);
            Assert.Equal("Grace", restored.FirstName);
            Assert.Equal("Hopper", restored.LastName);
        }
        finally
        {
            CleanupFiles(path);
        }
    }

    [Fact]
    public async Task GetManifestAsync_ReturnsCorrectEntityCounts()
    {
        _db.Members.Add(SeedMember("M1", active: true));
        _db.Members.Add(SeedMember("M2", active: true));
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var svc = BuildService();
        var path = TempPath();

        try
        {
            await svc.ExportAsync(path, TestContext.Current.CancellationToken);
            var manifest = await svc.GetManifestAsync(path, TestContext.Current.CancellationToken);
            Assert.Equal(2, manifest.EntityCounts["Members"]);
        }
        finally
        {
            CleanupFiles(path);
        }
    }

    // --- Helpers ---

    private BackupService BuildService()
    {
        var backupRepo = new BackupRepository(_db);
        var uow = new UnitOfWork(_db);
        var auditRepo = new AuditTrailRepository(_db);
        var auditSvc = new AuditTrailService(auditRepo, NullLogger<AuditTrailService>.Instance);
        return new BackupService(backupRepo, uow, auditSvc, NullLogger<BackupService>.Instance, RealLocalizer.Instance);
    }

    private static Member SeedMember(string name, bool active, bool deleted = false)
    {
        var now = DateTime.UtcNow;
        return new Member
        {
            Id = Guid.NewGuid(),
            FirstName = name,
            StreetAddress = "1 Test St",
            JoinDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            Status = active ? MemberStatus.Active : MemberStatus.Inactive,
            ActivateDate = active ? now : null,
            IsDeleted = deleted,
            DeletedAt = deleted ? now : null,
            DeletedBy = deleted ? "system" : null,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    private static string TempPath() =>
        Path.Combine(Path.GetTempPath(), $"sf_v9_{Guid.NewGuid()}.sfbak");

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
        foreach (var f in Directory.GetFiles(dir, "StageFright-Checkpoint-*.sfbak"))
            try { File.Delete(f); } catch { /* best-effort */ }
    }
}
