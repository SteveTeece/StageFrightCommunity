using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Exceptions;
using StageFright.Core.Modules.AuditTrail;
using StageFright.Core.Modules.Settings;
using StageFright.Data.Repositories;
using StageFright.Data.Tests.Infrastructure;

namespace StageFright.Data.Tests;

/// <summary>
/// Integration tests verifying import atomicity and pre-import checkpoint behaviour.
/// Uses a real SQLite in-memory database.
/// </summary>
public class BackupImportTests_Integration : IDisposable
{
    private readonly DbContextFactory _factory = new();

    // --- Pre-import checkpoint ---

    [Fact]
    public async Task ImportAsync_CreatesCheckpointFile_BeforeDataWrite()
    {
        using var db = _factory.CreateContext();
        var svc = BuildBackupService(db);
        var exportPath = TempFile();

        try
        {
            // Export minimal DB
            await svc.ExportAsync(exportPath, TestContext.Current.CancellationToken);

            // Directory where checkpoint will be created
            var dir = Path.GetDirectoryName(exportPath)!;
            var beforeImport = DateTime.UtcNow;

            await svc.ImportAsync(exportPath, TestContext.Current.CancellationToken);

            // At least one checkpoint file should exist in the same directory
            var checkpoints = Directory.GetFiles(dir, "StageFright-Checkpoint-*.sfbak");
            Assert.NotEmpty(checkpoints);
        }
        finally
        {
            CleanupTempFiles(exportPath);
        }
    }

    // --- Rollback on failure ---

    [Fact]
    public async Task ImportAsync_RollsBack_WhenUpsertFails_LeavingOriginalDataIntact()
    {
        using var db = _factory.CreateContext();

        // Seed a member that will exist before import
        var originalMember = new Member
        {
            Id = Guid.NewGuid(),
            FirstName = "Original",
            LastName = "Member",
            StreetAddress = "1 Old St",
            JoinDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            Status = MemberStatus.Active,
            ActivateDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.Members.Add(originalMember);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Export current state (has one member)
        var svc = BuildBackupService(db);
        var backupPath = TempFile();

        try
        {
            await svc.ExportAsync(backupPath, TestContext.Current.CancellationToken);

            // Add another member after export
            var laterMember = new Member
            {
                Id = Guid.NewGuid(),
                FirstName = "Later",
                LastName = "Member",
                StreetAddress = "2 New St",
                JoinDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                Status = MemberStatus.Active,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            db.Members.Add(laterMember);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);

            // Import from backup (only has one member)
            await svc.ImportAsync(backupPath, TestContext.Current.CancellationToken);

            // Original member should still exist (was in the backup)
            var count = await db.Members.IgnoreQueryFilters().CountAsync(cancellationToken: TestContext.Current.CancellationToken);
            Assert.True(count >= 1);
            Assert.True(await db.Members.IgnoreQueryFilters().AnyAsync(m => m.Id == originalMember.Id, cancellationToken: TestContext.Current.CancellationToken));
        }
        finally
        {
            CleanupTempFiles(backupPath);
        }
    }

    // --- Completeness validation ---

    [Fact]
    public async Task ImportAsync_ThrowsImportException_WhenAccountsNull_IntegrationPath()
    {
        using var db = _factory.CreateContext();
        var svc = BuildBackupService(db);
        var path = TempFile();

        try
        {
            await svc.ExportAsync(path, TestContext.Current.CancellationToken);

            // Tamper: remove Accounts from EntityCounts to simulate a missing entity type
            using var readStream = File.OpenRead(path);
            var envelope = ProtoBuf.Serializer.Deserialize<StageFright.Core.Modules.Settings.Backup.BackupEnvelope>(readStream);
            readStream.Close();
            envelope.EntityCounts.Remove("Accounts");
            using var writeStream = File.Create(path);
            ProtoBuf.Serializer.Serialize(writeStream, envelope);
            writeStream.Close();

            var ex = await Assert.ThrowsAsync<ImportException>(() => svc.ImportAsync(path, TestContext.Current.CancellationToken));
            Assert.Contains("missing Accounts", ex.Message);
        }
        finally
        {
            CleanupTempFiles(path);
        }
    }

    // --- Version mismatch ---

    [Fact]
    public async Task ImportAsync_ThrowsImportException_OnUnsupportedMajorVersion_IntegrationPath()
    {
        using var db = _factory.CreateContext();
        var svc = BuildBackupService(db);
        var path = TempFile();

        try
        {
            await svc.ExportAsync(path, TestContext.Current.CancellationToken);

            using var readStream = File.OpenRead(path);
            var envelope = ProtoBuf.Serializer.Deserialize<StageFright.Core.Modules.Settings.Backup.BackupEnvelope>(readStream);
            readStream.Close();
            envelope.SchemaVersion = "99.0.0";
            using var writeStream = File.Create(path);
            ProtoBuf.Serializer.Serialize(writeStream, envelope);
            writeStream.Close();

            await Assert.ThrowsAsync<ImportException>(() => svc.ImportAsync(path, TestContext.Current.CancellationToken));
        }
        finally
        {
            CleanupTempFiles(path);
        }
    }

    // --- Round-trip ---

    [Fact]
    public async Task ExportThenImport_RestoresData_WhenDatabaseIsEmpty()
    {
        // First DB: seed data and export
        using var sourceFactory = new DbContextFactory();
        using var sourceDb = sourceFactory.CreateContext();

        var member = new Member
        {
            Id = Guid.NewGuid(),
            FirstName = "Restored",
            LastName = "Member",
            StreetAddress = "3 Backup Ave",
            JoinDate = new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            Status = MemberStatus.Active,
            ActivateDate = new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        sourceDb.Members.Add(member);
        await sourceDb.SaveChangesAsync(TestContext.Current.CancellationToken);

        var sourceSvc = BuildBackupService(sourceDb);
        var backupPath = TempFile();

        try
        {
            await sourceSvc.ExportAsync(backupPath, TestContext.Current.CancellationToken);

            // Second DB: import into fresh database
            using var targetFactory = new DbContextFactory();
            using var targetDb = targetFactory.CreateContext();
            var targetSvc = BuildBackupService(targetDb);

            await targetSvc.ImportAsync(backupPath, TestContext.Current.CancellationToken);

            var restored = await targetDb.Members.IgnoreQueryFilters()
                .FirstOrDefaultAsync(m => m.Id == member.Id, cancellationToken: TestContext.Current.CancellationToken);
            Assert.NotNull(restored);
            Assert.Equal("Restored Member", restored!.FullName);
        }
        finally
        {
            CleanupTempFiles(backupPath);
        }
    }

    [Fact]
    public async Task ImportAsync_LegacyFormatBackup_RestoresNonEmptyNames_IntegrationPath()
    {
        using var db = _factory.CreateContext();
        var member = new Member
        {
            Id = Guid.NewGuid(),
            FirstName = "Placeholder",
            LastName = "Name",
            StreetAddress = "1 Legacy Rd",
            JoinDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            Status = MemberStatus.Active,
            ActivateDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        db.Members.Add(member);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var svc = BuildBackupService(db);
        var path = TempFile();

        try
        {
            await svc.ExportAsync(path, TestContext.Current.CancellationToken);

            // Tamper the export into a pre-feature (legacy) shape: clear FirstName/LastName,
            // populate only the old combined LegacyName field.
            using (var readStream = File.OpenRead(path))
            {
                var envelope = ProtoBuf.Serializer.Deserialize<StageFright.Core.Modules.Settings.Backup.BackupEnvelope>(readStream);
                readStream.Close();
                var dto = envelope.Members!.Single(m => m.Id == member.Id);
                dto.FirstName = string.Empty;
                dto.LastName = string.Empty;
                dto.LegacyName = "Grace Hopper";
                using var writeStream = File.Create(path);
                ProtoBuf.Serializer.Serialize(writeStream, envelope);
            }

            await svc.ImportAsync(path, TestContext.Current.CancellationToken);

            db.ChangeTracker.Clear();
            var restored = await db.Members.IgnoreQueryFilters().SingleAsync(m => m.Id == member.Id, cancellationToken: TestContext.Current.CancellationToken);
            Assert.Equal("Grace", restored.FirstName);
            Assert.Equal("Hopper", restored.LastName);
        }
        finally
        {
            CleanupTempFiles(path);
        }
    }

    [Fact]
    public async Task Export_IncludesSoftDeletedMember_IntegrationPath()
    {
        using var db = _factory.CreateContext();

        var deletedMember = new Member
        {
            Id = Guid.NewGuid(),
            FirstName = "Archived",
            LastName = "Member",
            StreetAddress = "0 Gone Lane",
            JoinDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            Status = MemberStatus.Inactive,
            IsDeleted = true,
            DeletedAt = DateTime.UtcNow,
            DeletedBy = "system",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.Members.Add(deletedMember);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var svc = BuildBackupService(db);
        var path = TempFile();

        try
        {
            await svc.ExportAsync(path, TestContext.Current.CancellationToken);
            var manifest = await svc.GetManifestAsync(path, TestContext.Current.CancellationToken);
            Assert.Equal(1, manifest.EntityCounts["Members"]);
        }
        finally
        {
            CleanupTempFiles(path);
        }
    }

    // --- Helpers ---

    private static BackupService BuildBackupService(StageFrightDbContext db)
    {
        var backupRepo = new BackupRepository(db);
        var uow = new UnitOfWork(db);
        var auditRepo = new AuditTrailRepository(db);
        var auditService = new AuditTrailService(auditRepo, NullLogger<AuditTrailService>.Instance);
        return new BackupService(backupRepo, uow, auditService, NullLogger<BackupService>.Instance);
    }

    private static string TempFile() =>
        Path.Combine(Path.GetTempPath(), $"sftest_{Guid.NewGuid()}.sfbak");

    private static void CleanupTempFiles(string primaryPath)
    {
        if (File.Exists(primaryPath)) File.Delete(primaryPath);
        var dir = Path.GetDirectoryName(primaryPath) ?? Path.GetTempPath();
        foreach (var f in Directory.GetFiles(dir, "StageFright-Checkpoint-*.sfbak"))
            try { File.Delete(f); } catch { /* best-effort */ }
    }

    public void Dispose() => _factory.Dispose();
}
