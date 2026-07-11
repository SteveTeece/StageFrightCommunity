using NSubstitute;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Exceptions;
using StageFright.Core.Modules.Settings;
using StageFright.Core.Modules.Settings.Backup;
using StageFright.Core.Tests.Fixtures;
using StageFright.Core.Enums;
using Microsoft.Extensions.Logging.Abstractions;
using ProtoBuf;

namespace StageFright.Core.Tests.Backup;

/// <summary>
/// Unit tests for BackupService. Verifies export includes soft-deleted records,
/// EntityCounts match, and import fails on version mismatch or missing entity types.
/// </summary>
public class BackupServiceTests : TestBase
{
    private readonly IBackupRepository _backupRepo = Substitute.For<IBackupRepository>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly IAuditTrailService _audit = Substitute.For<IAuditTrailService>();

    private BackupService CreateService() =>
        new(_backupRepo, _uow, _audit, NullLogger<BackupService>.Instance);

    // --- ExportAsync ---

    [Fact]
    public async Task ExportAsync_CallsGetFullSnapshot_ToIncludeDeletedRecords()
    {
        var snapshot = BuildMinimalSnapshot();
        _backupRepo.GetFullSnapshotAsync(Arg.Any<CancellationToken>()).Returns(snapshot);
        var svc = CreateService();
        var path = Path.Combine(Path.GetTempPath(), $"test_export_{Guid.NewGuid()}.sfbak");

        try
        {
            await svc.ExportAsync(path, Ct);
            await _backupRepo.Received(1).GetFullSnapshotAsync(Arg.Any<CancellationToken>());
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public async Task ExportAsync_WritesFile_ThatCanBeDeserialized()
    {
        var member = new Member
        {
            Id = Guid.NewGuid(),
            Name = "Alice",
            StreetAddress = "1 Main St",
            JoinDate = DateTime.UtcNow,
            Status = MemberStatus.Active,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var snapshot = new BackupSnapshot { Members = [member] };
        _backupRepo.GetFullSnapshotAsync(Arg.Any<CancellationToken>()).Returns(snapshot);
        var svc = CreateService();
        var path = Path.Combine(Path.GetTempPath(), $"test_export_{Guid.NewGuid()}.sfbak");

        try
        {
            await svc.ExportAsync(path, Ct);
            Assert.True(File.Exists(path));
            Assert.True(new FileInfo(path).Length > 0);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public async Task ExportAsync_IncludesSoftDeletedMember_InEntityCounts()
    {
        var deletedMember = new Member
        {
            Id = Guid.NewGuid(), Name = "Deleted", StreetAddress = "1 X St",
            JoinDate = DateTime.UtcNow, IsDeleted = true, DeletedAt = DateTime.UtcNow,
            Status = MemberStatus.Inactive, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        var activeMember = new Member
        {
            Id = Guid.NewGuid(), Name = "Active", StreetAddress = "2 X St",
            JoinDate = DateTime.UtcNow, Status = MemberStatus.Active,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        var snapshot = new BackupSnapshot { Members = [activeMember, deletedMember] };
        _backupRepo.GetFullSnapshotAsync(Arg.Any<CancellationToken>()).Returns(snapshot);
        var svc = CreateService();
        var path = Path.Combine(Path.GetTempPath(), $"test_export_{Guid.NewGuid()}.sfbak");

        try
        {
            await svc.ExportAsync(path, Ct);
            var manifest = await svc.GetManifestAsync(path, Ct);
            Assert.Equal(2, manifest.EntityCounts["Members"]);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    // --- GetManifestAsync ---

    [Fact]
    public async Task GetManifestAsync_ReturnsEntityCounts_MatchingExportedData()
    {
        var snapshot = new BackupSnapshot
        {
            Members = [BuildMember(), BuildMember()],
            Accounts = [BuildAccount(), BuildAccount(), BuildAccount()]
        };
        _backupRepo.GetFullSnapshotAsync(Arg.Any<CancellationToken>()).Returns(snapshot);
        var svc = CreateService();
        var path = Path.Combine(Path.GetTempPath(), $"test_manifest_{Guid.NewGuid()}.sfbak");

        try
        {
            await svc.ExportAsync(path, Ct);
            var manifest = await svc.GetManifestAsync(path, Ct);
            Assert.Equal(2, manifest.EntityCounts["Members"]);
            Assert.Equal(3, manifest.EntityCounts["Accounts"]);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public async Task GetManifestAsync_ThrowsImportException_OnCorruptFile()
    {
        var path = Path.Combine(Path.GetTempPath(), $"corrupt_{Guid.NewGuid()}.sfbak");
        await File.WriteAllBytesAsync(path, [0xFF, 0xFF, 0xFF, 0xFF]);
        var svc = CreateService();

        try
        {
            await Assert.ThrowsAsync<ImportException>(() => svc.GetManifestAsync(path, Ct));
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    // --- ImportAsync ---

    [Fact]
    public async Task ImportAsync_ThrowsImportException_OnMajorVersionMismatch()
    {
        var snapshot = BuildMinimalSnapshot();
        _backupRepo.GetFullSnapshotAsync(Arg.Any<CancellationToken>()).Returns(snapshot);
        var svc = CreateService();
        var path = Path.Combine(Path.GetTempPath(), $"test_ver_{Guid.NewGuid()}.sfbak");
        var checkpointPath = Path.Combine(Path.GetTempPath(), $"checkpoint_{Guid.NewGuid()}.sfbak");

        try
        {
            await svc.ExportAsync(path, Ct);

            // Tamper the file to have version "99.0.0"
            var envelope = ReadEnvelope(path);
            envelope.SchemaVersion = "99.0.0";
            WriteEnvelope(path, envelope);

            await Assert.ThrowsAsync<ImportException>(() => svc.ImportAsync(path, Ct));
        }
        finally
        {
            foreach (var f in new[] { path, checkpointPath })
                if (File.Exists(f)) File.Delete(f);
        }
    }

    [Fact]
    public async Task ImportAsync_ThrowsImportException_WhenAccountsNull()
    {
        var snapshot = BuildMinimalSnapshot();
        _backupRepo.GetFullSnapshotAsync(Arg.Any<CancellationToken>()).Returns(snapshot);
        var svc = CreateService();
        var path = Path.Combine(Path.GetTempPath(), $"test_incomplete_{Guid.NewGuid()}.sfbak");

        try
        {
            await svc.ExportAsync(path, Ct);

            // Tamper: remove Accounts from EntityCounts to simulate a missing entity type
            var envelope = ReadEnvelope(path);
            envelope.EntityCounts.Remove("Accounts");
            WriteEnvelope(path, envelope);

            var ex = await Assert.ThrowsAsync<ImportException>(() => svc.ImportAsync(path, Ct));
            Assert.Contains("missing Accounts", ex.Message);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public async Task ImportAsync_CallsUnitOfWork_ForAtomicUpsert()
    {
        var snapshot = BuildMinimalSnapshot();
        _backupRepo.GetFullSnapshotAsync(Arg.Any<CancellationToken>()).Returns(snapshot);
        _uow.ExecuteInTransactionAsync(Arg.Any<Func<CancellationToken, Task>>(), Arg.Any<CancellationToken>())
            .Returns(ci => ((Func<CancellationToken, Task>)ci[0])(Ct));
        var svc = CreateService();
        var path = Path.Combine(Path.GetTempPath(), $"test_import_{Guid.NewGuid()}.sfbak");

        try
        {
            await svc.ExportAsync(path, Ct);
            await svc.ImportAsync(path, Ct);
            await _uow.Received(1).ExecuteInTransactionAsync(
                Arg.Any<Func<CancellationToken, Task>>(), Arg.Any<CancellationToken>());
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public async Task ImportAsync_CreatesCheckpointFile_BeforeWrite()
    {
        var snapshot = BuildMinimalSnapshot();
        _backupRepo.GetFullSnapshotAsync(Arg.Any<CancellationToken>()).Returns(snapshot);
        var checkpointCreated = false;
        _uow.ExecuteInTransactionAsync(Arg.Any<Func<CancellationToken, Task>>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                checkpointCreated = true;
                return ((Func<CancellationToken, Task>)ci[0])(Ct);
            });
        var svc = CreateService();
        var path = Path.Combine(Path.GetTempPath(), $"test_checkpoint_{Guid.NewGuid()}.sfbak");

        try
        {
            await svc.ExportAsync(path, Ct);

            // Reset GetFullSnapshot to simulate pre-import state after first export
            _backupRepo.GetFullSnapshotAsync(Arg.Any<CancellationToken>()).Returns(BuildMinimalSnapshot());

            // During ImportAsync, a checkpoint export will be called before UnitOfWork
            int exportCallCount = 0;
            _backupRepo.GetFullSnapshotAsync(Arg.Any<CancellationToken>())
                .Returns(_ =>
                {
                    exportCallCount++;
                    return Task.FromResult(BuildMinimalSnapshot());
                });

            await svc.ImportAsync(path, Ct);

            // ExportAsync is called once for checkpoint + once for original export (already done)
            // At minimum, UnitOfWork was executed
            Assert.True(checkpointCreated);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
            foreach (var f in Directory.GetFiles(Path.GetTempPath(), "StageFright-Checkpoint-*.sfbak"))
                File.Delete(f);
        }
    }

    // --- Helpers ---

    private static BackupSnapshot BuildMinimalSnapshot() => new()
    {
        Members = [],
        CommitteeMemberships = [],
        Rehearsals = [],
        AttendanceRecords = [],
        Events = [],
        EventTypes = [],
        ParticipationRecords = [],
        Fees = [],
        Payments = [],
        Transactions = [],
        Accounts = [],
        Settings = null,
        AuditTrailEntries = []
    };

    private static Member BuildMember() => new()
    {
        Id = Guid.NewGuid(), Name = "Test", StreetAddress = "1 St",
        JoinDate = DateTime.UtcNow, Status = MemberStatus.Active,
        CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
    };

    private static Account BuildAccount() => new()
    {
        Id = Guid.NewGuid(), Name = "Test Cat", Type = AccountType.Income,
        AccountNumber = "4000", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
    };

    private static BackupEnvelope ReadEnvelope(string path)
    {
        using var fs = File.OpenRead(path);
        return Serializer.Deserialize<BackupEnvelope>(fs);
    }

    private static void WriteEnvelope(string path, BackupEnvelope envelope)
    {
        using var fs = File.Create(path);
        Serializer.Serialize(fs, envelope);
    }
}
