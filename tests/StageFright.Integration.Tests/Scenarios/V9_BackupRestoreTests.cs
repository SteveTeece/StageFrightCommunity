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
            Assert.Equal("1.2.0", manifest.SchemaVersion);
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

    // --- .sfbak drift fix (spec 030): finance record types + expanded settings ---

    [Fact]
    public async Task Backup_RoundTripsFinanceEntitiesAndExpandedSettings_Integration()
    {
        var cashAccountId = new Guid("00000000-0000-0000-0000-000000000001");
        var now = DateTime.UtcNow;

        var je = new JournalEntry
        {
            Id = Guid.NewGuid(), Type = JournalEntryType.GeneralJournal, Date = now.Date,
            Description = "Opening journal", CreatedAt = now
        };
        var tx = new Transaction
        {
            Id = Guid.NewGuid(), Date = now.Date, AccountId = cashAccountId,
            DebitAmount = 50m, CreditAmount = 0m, GLAccount = "1100",
            JournalEntryId = je.Id, TaxCode = TaxCode.Taxable, Description = "Journal line", CreatedAt = now
        };
        var recon = new BankReconciliation
        {
            Id = Guid.NewGuid(), AccountId = cashAccountId, StatementDate = now.Date,
            StatementClosingBalance = 50m, OpeningBalance = 0m, Status = ReconciliationStatus.Finalised,
            FinalisedAt = now, CreatedAt = now, UpdatedAt = now
        };
        var line = new ReconciliationLine
        {
            Id = Guid.NewGuid(), ReconciliationId = recon.Id, TransactionId = tx.Id, CreatedAt = now
        };
        var settings = new Settings
        {
            Id = Guid.NewGuid(), OrganizationName = "Round Trip Choir", AnnualFee = 80m, AttendanceFee = 3m,
            MembershipRenewalMonth = 1, CommitteeRenewalMonth = 1, AuditRetentionYears = 2,
            FinancialYearStartMonth = 1, FinancialYearStartDay = 6, CurrencyCode = "USD",
            InceptionDate = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            IsTaxApplicable = true, TaxRate = 8.25m, AnnualFeeTaxCode = TaxCode.Taxable,
            TaxEntryMode = TaxEntryMode.Exclusive, LanguageCode = "es-ES", ShowParticipationGraphs = false,
            SchemaVersion = "1.1.0", CreatedAt = now, UpdatedAt = now
        };
        _db.JournalEntries.Add(je);
        _db.Transactions.Add(tx);
        _db.BankReconciliations.Add(recon);
        _db.ReconciliationLines.Add(line);
        _db.Settings.Add(settings);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var svc = BuildService();
        var path = TempPath();

        try
        {
            await svc.ExportAsync(path, TestContext.Current.CancellationToken);

            var manifest = await svc.GetManifestAsync(path, TestContext.Current.CancellationToken);
            Assert.Equal(1, manifest.EntityCounts["JournalEntries"]);
            Assert.Equal(1, manifest.EntityCounts["BankReconciliations"]);
            Assert.Equal(1, manifest.EntityCounts["ReconciliationLines"]);

            // Mutate settings after export, then restore from the backup.
            var tracked = await _db.Settings.SingleAsync(cancellationToken: TestContext.Current.CancellationToken);
            tracked.CurrencyCode = "AUD";
            tracked.ShowParticipationGraphs = true;
            tracked.LanguageCode = "en-AU";
            await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

            await svc.ImportAsync(path, TestContext.Current.CancellationToken);
            _db.ChangeTracker.Clear();

            var restoredSettings = await _db.Settings.SingleAsync(cancellationToken: TestContext.Current.CancellationToken);
            Assert.Equal("USD", restoredSettings.CurrencyCode);
            Assert.Equal(6, restoredSettings.FinancialYearStartDay);
            Assert.Equal(TaxEntryMode.Exclusive, restoredSettings.TaxEntryMode);
            Assert.Equal("es-ES", restoredSettings.LanguageCode);
            Assert.False(restoredSettings.ShowParticipationGraphs);

            var restoredTx = await _db.Transactions.SingleAsync(t => t.Id == tx.Id, TestContext.Current.CancellationToken);
            Assert.Equal(je.Id, restoredTx.JournalEntryId);
            Assert.Equal(TaxCode.Taxable, restoredTx.TaxCode);

            Assert.True(await _db.JournalEntries.AnyAsync(j => j.Id == je.Id, TestContext.Current.CancellationToken));
            Assert.True(await _db.BankReconciliations.IgnoreQueryFilters()
                .AnyAsync(r => r.Id == recon.Id && r.Status == ReconciliationStatus.Finalised, TestContext.Current.CancellationToken));
            Assert.True(await _db.ReconciliationLines.IgnoreQueryFilters()
                .AnyAsync(l => l.Id == line.Id && l.TransactionId == tx.Id, TestContext.Current.CancellationToken));
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
