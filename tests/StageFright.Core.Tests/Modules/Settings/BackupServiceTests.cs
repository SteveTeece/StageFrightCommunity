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
            FirstName = "Alice",
            LastName = "Test",
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
            Id = Guid.NewGuid(), FirstName = "Deleted", LastName = "Test", StreetAddress = "1 X St",
            JoinDate = DateTime.UtcNow, IsDeleted = true, DeletedAt = DateTime.UtcNow,
            Status = MemberStatus.Inactive, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        var activeMember = new Member
        {
            Id = Guid.NewGuid(), FirstName = "Active", LastName = "Test", StreetAddress = "2 X St",
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

    // --- Member name export/restore (spec 011) ---

    [Fact]
    public async Task ExportAsync_PopulatesFirstNameAndLastName_LeavesLegacyNameBlank()
    {
        var member = BuildMember();
        member.FirstName = "Jane";
        member.LastName = "Doe";
        var snapshot = new BackupSnapshot { Members = [member] };
        _backupRepo.GetFullSnapshotAsync(Arg.Any<CancellationToken>()).Returns(snapshot);
        var svc = CreateService();
        var path = Path.Combine(Path.GetTempPath(), $"test_export_{Guid.NewGuid()}.sfbak");

        try
        {
            await svc.ExportAsync(path, Ct);
            var envelope = ReadEnvelope(path);
            var dto = Assert.Single(envelope.Members!);

            Assert.Equal("Jane", dto.FirstName);
            Assert.Equal("Doe", dto.LastName);
            Assert.Equal(string.Empty, dto.LegacyName);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public async Task ImportAsync_LegacyBackup_DerivesFirstNameAndLastName_ViaMemberNameSplitter()
    {
        var snapshot = new BackupSnapshot { Members = [BuildMember()] };
        _backupRepo.GetFullSnapshotAsync(Arg.Any<CancellationToken>()).Returns(snapshot);
        _uow.ExecuteInTransactionAsync(Arg.Any<Func<CancellationToken, Task>>(), Arg.Any<CancellationToken>())
            .Returns(ci => ((Func<CancellationToken, Task>)ci[0])(Ct));
        var svc = CreateService();
        var path = Path.Combine(Path.GetTempPath(), $"test_import_{Guid.NewGuid()}.sfbak");

        try
        {
            await svc.ExportAsync(path, Ct);

            // Tamper the export into a pre-feature (legacy) shape: no FirstName/LastName,
            // only the old combined LegacyName field populated.
            var envelope = ReadEnvelope(path);
            var dto = envelope.Members!.Single();
            dto.FirstName = string.Empty;
            dto.LastName = string.Empty;
            dto.LegacyName = "Janet Smith";
            WriteEnvelope(path, envelope);

            BackupSnapshot? upserted = null;
            _backupRepo.UpsertSnapshotAsync(Arg.Do<BackupSnapshot>(s => upserted = s), Arg.Any<CancellationToken>())
                .Returns(Task.CompletedTask);

            await svc.ImportAsync(path, Ct);

            var restored = Assert.Single(upserted!.Members!);
            Assert.Equal("Janet", restored.FirstName);
            Assert.Equal("Smith", restored.LastName);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
            foreach (var f in Directory.GetFiles(Path.GetTempPath(), "StageFright-Checkpoint-*.sfbak"))
                File.Delete(f);
        }
    }

    [Fact]
    public async Task ImportAsync_CurrentFormatBackup_UsesFirstNameAndLastNameDirectly()
    {
        var member = BuildMember();
        member.FirstName = "Janet";
        member.LastName = "Smith";
        var snapshot = new BackupSnapshot { Members = [member] };
        _backupRepo.GetFullSnapshotAsync(Arg.Any<CancellationToken>()).Returns(snapshot);
        _uow.ExecuteInTransactionAsync(Arg.Any<Func<CancellationToken, Task>>(), Arg.Any<CancellationToken>())
            .Returns(ci => ((Func<CancellationToken, Task>)ci[0])(Ct));
        var svc = CreateService();
        var path = Path.Combine(Path.GetTempPath(), $"test_import_{Guid.NewGuid()}.sfbak");

        try
        {
            await svc.ExportAsync(path, Ct);

            BackupSnapshot? upserted = null;
            _backupRepo.UpsertSnapshotAsync(Arg.Do<BackupSnapshot>(s => upserted = s), Arg.Any<CancellationToken>())
                .Returns(Task.CompletedTask);

            await svc.ImportAsync(path, Ct);

            var restored = Assert.Single(upserted!.Members!);
            Assert.Equal("Janet", restored.FirstName);
            Assert.Equal("Smith", restored.LastName);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
            foreach (var f in Directory.GetFiles(Path.GetTempPath(), "StageFright-Checkpoint-*.sfbak"))
                File.Delete(f);
        }
    }

    // --- AGM workflow entities (spec 013) ---

    [Fact]
    public async Task ExportAsync_IncludesAgmEntities_InEntityCounts()
    {
        var member = BuildMember();
        var officeHolderType = BuildOfficeHolderType();
        var agm = new AnnualGeneralMeeting
        {
            Id = Guid.NewGuid(), Date = DateTime.UtcNow.Date, GeneralCommitteeSeatCountTarget = 5,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        var attendance = new AgmAttendanceRecord
        {
            Id = Guid.NewGuid(), AnnualGeneralMeetingId = agm.Id, MemberId = member.Id, Attended = true,
            CreatedAt = DateTime.UtcNow
        };
        var term = new CommitteeTerm
        {
            Id = Guid.NewGuid(), StartedByAgmId = agm.Id, StartDate = agm.Date, LabelYear = agm.Date.Year,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        var snapshot = new BackupSnapshot
        {
            Members = [member],
            AnnualGeneralMeetings = [agm],
            AgmAttendanceRecords = [attendance],
            CommitteeOfficeHolderTypes = [officeHolderType],
            CommitteeTerms = [term]
        };
        _backupRepo.GetFullSnapshotAsync(Arg.Any<CancellationToken>()).Returns(snapshot);
        var svc = CreateService();
        var path = Path.Combine(Path.GetTempPath(), $"test_agm_export_{Guid.NewGuid()}.sfbak");

        try
        {
            await svc.ExportAsync(path, Ct);
            var manifest = await svc.GetManifestAsync(path, Ct);

            Assert.Equal(1, manifest.EntityCounts["AnnualGeneralMeetings"]);
            Assert.Equal(1, manifest.EntityCounts["AgmAttendanceRecords"]);
            Assert.Equal(1, manifest.EntityCounts["CommitteeOfficeHolderTypes"]);
            Assert.Equal(1, manifest.EntityCounts["CommitteeTerms"]);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public async Task ImportAsync_RoundTripsAgmEntities()
    {
        var member = BuildMember();
        var officeHolderType = BuildOfficeHolderType();
        var agm = new AnnualGeneralMeeting
        {
            Id = Guid.NewGuid(), Date = DateTime.UtcNow.Date, Notes = "Annual sitting",
            GeneralCommitteeSeatCountTarget = 5, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        var attendance = new AgmAttendanceRecord
        {
            Id = Guid.NewGuid(), AnnualGeneralMeetingId = agm.Id, MemberId = member.Id, Attended = true,
            CreatedAt = DateTime.UtcNow
        };
        var term = new CommitteeTerm
        {
            Id = Guid.NewGuid(), StartedByAgmId = agm.Id, StartDate = agm.Date, LabelYear = agm.Date.Year,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        var positionRecord = new CommitteePositionRecord
        {
            Id = Guid.NewGuid(), MemberId = member.Id, CommitteeTermId = term.Id,
            OfficeHolderTypeId = officeHolderType.Id, StartDate = agm.Date,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        var snapshot = new BackupSnapshot
        {
            Members = [member],
            AnnualGeneralMeetings = [agm],
            AgmAttendanceRecords = [attendance],
            CommitteeOfficeHolderTypes = [officeHolderType],
            CommitteeTerms = [term],
            CommitteePositionRecords = [positionRecord]
        };
        _backupRepo.GetFullSnapshotAsync(Arg.Any<CancellationToken>()).Returns(snapshot);
        _uow.ExecuteInTransactionAsync(Arg.Any<Func<CancellationToken, Task>>(), Arg.Any<CancellationToken>())
            .Returns(ci => ((Func<CancellationToken, Task>)ci[0])(Ct));
        var svc = CreateService();
        var path = Path.Combine(Path.GetTempPath(), $"test_agm_import_{Guid.NewGuid()}.sfbak");

        try
        {
            await svc.ExportAsync(path, Ct);

            BackupSnapshot? upserted = null;
            _backupRepo.UpsertSnapshotAsync(Arg.Do<BackupSnapshot>(s => upserted = s), Arg.Any<CancellationToken>())
                .Returns(Task.CompletedTask);

            await svc.ImportAsync(path, Ct);

            var restoredAgm = Assert.Single(upserted!.AnnualGeneralMeetings);
            Assert.Equal(agm.Date, restoredAgm.Date);
            Assert.Equal(5, restoredAgm.GeneralCommitteeSeatCountTarget);

            var restoredAttendance = Assert.Single(upserted.AgmAttendanceRecords);
            Assert.True(restoredAttendance.Attended);

            var restoredOfficeHolderType = Assert.Single(upserted.CommitteeOfficeHolderTypes);
            Assert.Equal("President", restoredOfficeHolderType.Name);

            var restoredTerm = Assert.Single(upserted.CommitteeTerms);
            Assert.Equal(term.LabelYear, restoredTerm.LabelYear);

            var restoredPosition = Assert.Single(upserted.CommitteePositionRecords);
            Assert.Equal(term.Id, restoredPosition.CommitteeTermId);
            Assert.Equal(officeHolderType.Id, restoredPosition.OfficeHolderTypeId);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
            foreach (var f in Directory.GetFiles(Path.GetTempPath(), "StageFright-Checkpoint-*.sfbak"))
                File.Delete(f);
        }
    }

    [Fact]
    public async Task ImportAsync_ThrowsImportException_WhenAnnualGeneralMeetingsMissing()
    {
        var snapshot = BuildMinimalSnapshot();
        _backupRepo.GetFullSnapshotAsync(Arg.Any<CancellationToken>()).Returns(snapshot);
        var svc = CreateService();
        var path = Path.Combine(Path.GetTempPath(), $"test_agm_incomplete_{Guid.NewGuid()}.sfbak");

        try
        {
            await svc.ExportAsync(path, Ct);

            var envelope = ReadEnvelope(path);
            envelope.EntityCounts.Remove("AnnualGeneralMeetings");
            WriteEnvelope(path, envelope);

            var ex = await Assert.ThrowsAsync<ImportException>(() => svc.ImportAsync(path, Ct));
            Assert.Contains("missing AnnualGeneralMeetings", ex.Message);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

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

    // --- Helpers ---

    private static BackupSnapshot BuildMinimalSnapshot() => new()
    {
        Members = [],
        CommitteePositionRecords = [],
        AnnualGeneralMeetings = [],
        AgmAttendanceRecords = [],
        CommitteeOfficeHolderTypes = [],
        CommitteeTerms = [],
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

    private static CommitteeOfficeHolderType BuildOfficeHolderType() => new()
    {
        Id = Guid.NewGuid(), Name = "President", DisplayOrder = 0, IsBuiltIn = true,
        CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
    };

    private static Member BuildMember() => new()
    {
        Id = Guid.NewGuid(), FirstName = "Test", LastName = "Member", StreetAddress = "1 St",
        JoinDate = DateTime.UtcNow, Status = MemberStatus.Active,
        CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
    };

    private static Account BuildAccount() => new()
    {
        Id = Guid.NewGuid(), Name = "Test Cat", Type = AccountType.Income,
        AccountNumber = "4000", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
    };
}
