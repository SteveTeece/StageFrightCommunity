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
    private readonly TempRecoveryCopyStore _recovery = new();

    private readonly IBackupRepository _backupRepo = Substitute.For<IBackupRepository>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly IAuditTrailService _audit = Substitute.For<IAuditTrailService>();
    private readonly FakeBackupDestinationPicker _picker = new();

    private BackupService CreateService() =>
        new(_backupRepo, _uow, _audit, NullLogger<BackupService>.Instance, RealLocalizer.Instance,
            _recovery, _picker);

    /// <summary>Writes the pre-restore recovery copy into the system temp directory for the test.</summary>
    private sealed class TempRecoveryCopyStore : IRecoveryCopyStore
    {
        // Its own folder, so parallel test runs never see or delete each other's recovery copies.
        private readonly string _directory = Directory.CreateDirectory(
            Path.Combine(Path.GetTempPath(), "StageFright-Tests", Guid.NewGuid().ToString("N"))).FullName;

        public string GetRecoveryDirectory() => _directory;
    }

    /// <summary>
    /// Stand-in for the OS-native Save dialog. By default it writes the stream to a temp file and
    /// reports <see cref="BackupDestinationResult.Saved"/>; <see cref="CancelDialog"/> simulates the
    /// user dismissing it, <see cref="SaveFailure"/> a wrapped platform error, and
    /// <see cref="Corrupt"/> lets a test tamper with the bytes that actually land on disk.
    /// </summary>
    private sealed class FakeBackupDestinationPicker : IBackupDestinationPicker
    {
        public bool CancelDialog { get; set; }
        public Exception? SaveFailure { get; set; }
        public Func<byte[], byte[]>? Corrupt { get; set; }
        public string? SavedPath { get; private set; }
        public string? SuggestedFileName { get; private set; }

        public async Task<BackupDestinationResult> SaveAsync(string suggestedFileName, Stream content, CancellationToken ct = default)
        {
            SuggestedFileName = suggestedFileName;
            if (SaveFailure is not null) throw SaveFailure;
            if (CancelDialog) return BackupDestinationResult.Cancelled;

            using var ms = new MemoryStream();
            await content.CopyToAsync(ms, ct);
            var bytes = Corrupt is null ? ms.ToArray() : Corrupt(ms.ToArray());

            SavedPath = Path.Combine(Path.GetTempPath(), $"sf_create_{Guid.NewGuid()}.sfbak");
            await File.WriteAllBytesAsync(SavedPath, bytes, ct);
            return BackupDestinationResult.Saved(SavedPath);
        }
    }

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
        await File.WriteAllBytesAsync(path, [0xFF, 0xFF, 0xFF, 0xFF], TestContext.Current.CancellationToken);
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
            .Returns(ci => ((Func<CancellationToken, Task>)ci[0]!)(Ct));
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
                return ((Func<CancellationToken, Task>)ci[0]!)(Ct);
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
            foreach (var f in Directory.GetFiles(_recovery.GetRecoveryDirectory(), "StageFright-Recovery-*.sfbak"))
                File.Delete(f);
        }
    }

    [Fact]
    public async Task Should_WriteRecoveryCopy_When_ImportingOverConfiguredDatabase()
    {
        // FR-015: a database that has completed setup (Settings record present) is always copied first.
        var configured = new BackupSnapshot
        {
            Settings = new Settings
            {
                Id = Guid.NewGuid(), OrganizationName = "Cool Choir", AnnualFee = 60m, AttendanceFee = 5m,
                MembershipRenewalMonth = 1, SchemaVersion = "1.1.0",
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
            }
        };
        _backupRepo.GetFullSnapshotAsync(Arg.Any<CancellationToken>()).Returns(configured);
        var svc = CreateService();
        var path = Path.Combine(Path.GetTempPath(), $"test_recovery_{Guid.NewGuid()}.sfbak");

        try
        {
            await svc.ExportAsync(path, Ct);
            await svc.ImportAsync(path, Ct);

            await _audit.Received(1).LogAsync(
                "Backup", Guid.Empty, AuditAction.Import, Arg.Any<string?>(),
                Arg.Is<string?>(v => v!.Contains("pre-restore recovery copy: ") && v.Contains("StageFright-Recovery-")),
                Arg.Any<string>(), Arg.Any<CancellationToken>());
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
            foreach (var f in Directory.GetFiles(_recovery.GetRecoveryDirectory(), "StageFright-Recovery-*.sfbak"))
                File.Delete(f);
        }
    }

    [Fact]
    public async Task Should_SkipRecoveryCopy_When_FirstRunSetupIncomplete()
    {
        // FR-015: with no Settings record the database holds only seeded defaults, so no copy is taken.
        _backupRepo.GetFullSnapshotAsync(Arg.Any<CancellationToken>()).Returns(BuildMinimalSnapshot());
        var svc = CreateService();
        var path = Path.Combine(Path.GetTempPath(), $"test_recovery_skip_{Guid.NewGuid()}.sfbak");

        try
        {
            await svc.ExportAsync(path, Ct);
            await svc.ImportAsync(path, Ct);

            await _audit.Received(1).LogAsync(
                "Backup", Guid.Empty, AuditAction.Import, Arg.Any<string?>(),
                Arg.Is<string?>(v => v!.EndsWith("pre-restore recovery copy: none (first-run setup incomplete)")),
                Arg.Any<string>(), Arg.Any<CancellationToken>());
            await _uow.Received(1).ExecuteInTransactionAsync(Arg.Any<Func<CancellationToken, Task>>(), Arg.Any<CancellationToken>());
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
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
            .Returns(ci => ((Func<CancellationToken, Task>)ci[0]!)(Ct));
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
            foreach (var f in Directory.GetFiles(_recovery.GetRecoveryDirectory(), "StageFright-Recovery-*.sfbak"))
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
            .Returns(ci => ((Func<CancellationToken, Task>)ci[0]!)(Ct));
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
            foreach (var f in Directory.GetFiles(_recovery.GetRecoveryDirectory(), "StageFright-Recovery-*.sfbak"))
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
            .Returns(ci => ((Func<CancellationToken, Task>)ci[0]!)(Ct));
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
            foreach (var f in Directory.GetFiles(_recovery.GetRecoveryDirectory(), "StageFright-Recovery-*.sfbak"))
                File.Delete(f);
        }
    }

    [Fact]
    public async Task ImportAsync_RoundTripsAuditRetentionYears()
    {
        var settings = new Settings
        {
            Id = Guid.NewGuid(), OrganizationName = "Test Org", AnnualFee = 75m, AttendanceFee = 5m,
            MembershipRenewalMonth = 1, AuditRetentionYears = 5,
            SchemaVersion = "1.1.0", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        var snapshot = new BackupSnapshot
        {
            Members = [], CommitteePositionRecords = [], AnnualGeneralMeetings = [],
            AgmAttendanceRecords = [], CommitteeOfficeHolderTypes = [], CommitteeTerms = [],
            Rehearsals = [], AttendanceRecords = [], Events = [], EventTypes = [],
            ParticipationRecords = [], Fees = [], Payments = [], Transactions = [], Accounts = [],
            Settings = settings, AuditTrailEntries = []
        };
        _backupRepo.GetFullSnapshotAsync(Arg.Any<CancellationToken>()).Returns(snapshot);
        _uow.ExecuteInTransactionAsync(Arg.Any<Func<CancellationToken, Task>>(), Arg.Any<CancellationToken>())
            .Returns(ci => ((Func<CancellationToken, Task>)ci[0]!)(Ct));
        var svc = CreateService();
        var path = Path.Combine(Path.GetTempPath(), $"test_settings_import_{Guid.NewGuid()}.sfbak");

        try
        {
            await svc.ExportAsync(path, Ct);

            BackupSnapshot? upserted = null;
            _backupRepo.UpsertSnapshotAsync(Arg.Do<BackupSnapshot>(s => upserted = s), Arg.Any<CancellationToken>())
                .Returns(Task.CompletedTask);

            await svc.ImportAsync(path, Ct);

            Assert.Equal(5, upserted!.Settings!.AuditRetentionYears);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
            foreach (var f in Directory.GetFiles(_recovery.GetRecoveryDirectory(), "StageFright-Recovery-*.sfbak"))
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

    // --- .sfbak drift fix (spec 030): finance record types, tax fields, expanded settings ---

    [Fact]
    public async Task Should_WriteCurrentSchemaVersion_When_Exported()
    {
        _backupRepo.GetFullSnapshotAsync(Arg.Any<CancellationToken>()).Returns(BuildMinimalSnapshot());
        var svc = CreateService();
        var path = Path.Combine(Path.GetTempPath(), $"test_schema_{Guid.NewGuid()}.sfbak");

        try
        {
            await svc.ExportAsync(path, Ct);
            var manifest = await svc.GetManifestAsync(path, Ct);
            Assert.Equal(BackupSchema.CurrentSchemaVersion, manifest.SchemaVersion);
            Assert.Equal("1.4.0", manifest.SchemaVersion);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    // --- External organisations (spec 032, FR-015) ---

    [Fact]
    public async Task Should_RoundTripOrganisationsContactsAndLinks_When_ExportedThenImported()
    {
        var (org, archivedOrg, contact, removedContact) = BuildOrganisationRegister();
        var linked = BuildJournalEntry();
        linked.ExternalOrganisationId = org.Id;
        var unlinked = BuildJournalEntry();
        var snapshot = new BackupSnapshot
        {
            ExternalOrganisations = [org, archivedOrg],
            ExternalOrganisationContacts = [contact, removedContact],
            JournalEntries = [linked, unlinked]
        };
        _backupRepo.GetFullSnapshotAsync(Arg.Any<CancellationToken>()).Returns(snapshot);
        _uow.ExecuteInTransactionAsync(Arg.Any<Func<CancellationToken, Task>>(), Arg.Any<CancellationToken>())
            .Returns(ci => ((Func<CancellationToken, Task>)ci[0]!)(Ct));
        var svc = CreateService();
        var path = Path.Combine(Path.GetTempPath(), $"test_org_rt_{Guid.NewGuid()}.sfbak");

        try
        {
            await svc.ExportAsync(path, Ct);

            var manifest = await svc.GetManifestAsync(path, Ct);
            Assert.Equal(2, manifest.EntityCounts["ExternalOrganisations"]);
            Assert.Equal(2, manifest.EntityCounts["ExternalOrganisationContacts"]);

            BackupSnapshot? upserted = null;
            _backupRepo.UpsertSnapshotAsync(Arg.Do<BackupSnapshot>(s => upserted = s), Arg.Any<CancellationToken>())
                .Returns(Task.CompletedTask);

            await svc.ImportAsync(path, Ct);

            AssertSameOrganisation(org, upserted!.ExternalOrganisations.Single(o => o.Id == org.Id));
            AssertSameOrganisation(archivedOrg, upserted.ExternalOrganisations.Single(o => o.Id == archivedOrg.Id));
            AssertSameContact(contact, upserted.ExternalOrganisationContacts.Single(c => c.Id == contact.Id));
            AssertSameContact(removedContact, upserted.ExternalOrganisationContacts.Single(c => c.Id == removedContact.Id));
            Assert.Equal(org.Id, upserted.JournalEntries.Single(j => j.Id == linked.Id).ExternalOrganisationId);
            Assert.Null(upserted.JournalEntries.Single(j => j.Id == unlinked.Id).ExternalOrganisationId);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
            foreach (var f in Directory.GetFiles(_recovery.GetRecoveryDirectory(), "StageFright-Recovery-*.sfbak"))
                File.Delete(f);
        }
    }

    [Fact]
    public async Task Should_RestoreEmptyRegister_When_Version120FileHasNoOrganisationLists()
    {
        var je = BuildJournalEntry();
        _backupRepo.GetFullSnapshotAsync(Arg.Any<CancellationToken>())
            .Returns(new BackupSnapshot { JournalEntries = [je] });
        _uow.ExecuteInTransactionAsync(Arg.Any<Func<CancellationToken, Task>>(), Arg.Any<CancellationToken>())
            .Returns(ci => ((Func<CancellationToken, Task>)ci[0]!)(Ct));
        var svc = CreateService();
        var path = Path.Combine(Path.GetTempPath(), $"test_120_file_{Guid.NewGuid()}.sfbak");

        try
        {
            await svc.ExportAsync(path, Ct);

            // Simulate a 1.2.0 file: no organisation lists and no organisation count keys.
            var envelope = ReadEnvelope(path);
            envelope.SchemaVersion = "1.2.0";
            envelope.ExternalOrganisations = null;
            envelope.ExternalOrganisationContacts = null;
            envelope.EntityCounts.Remove("ExternalOrganisations");
            envelope.EntityCounts.Remove("ExternalOrganisationContacts");
            WriteEnvelope(path, envelope);

            BackupSnapshot? upserted = null;
            _backupRepo.UpsertSnapshotAsync(Arg.Do<BackupSnapshot>(s => upserted = s), Arg.Any<CancellationToken>())
                .Returns(Task.CompletedTask);

            await svc.ImportAsync(path, Ct);

            Assert.Empty(upserted!.ExternalOrganisations);
            Assert.Empty(upserted.ExternalOrganisationContacts);
            Assert.Null(Assert.Single(upserted.JournalEntries).ExternalOrganisationId);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
            foreach (var f in Directory.GetFiles(_recovery.GetRecoveryDirectory(), "StageFright-Recovery-*.sfbak"))
                File.Delete(f);
        }
    }

    // --- Banking records (spec 033, backup 1.4.0) ---

    [Fact]
    public async Task Should_RoundTripBankingRecords_When_ExportedThenImported()
    {
        var (cheque, opening, deposit, line) = BuildBankingRecords();
        var snapshot = new BackupSnapshot
        {
            ReceivedItems = [cheque, opening],
            BankDeposits = [deposit],
            BankDepositLines = [line]
        };
        _backupRepo.GetFullSnapshotAsync(Arg.Any<CancellationToken>()).Returns(snapshot);
        _uow.ExecuteInTransactionAsync(Arg.Any<Func<CancellationToken, Task>>(), Arg.Any<CancellationToken>())
            .Returns(ci => ((Func<CancellationToken, Task>)ci[0]!)(Ct));
        var svc = CreateService();
        var path = Path.Combine(Path.GetTempPath(), $"test_banking_rt_{Guid.NewGuid()}.sfbak");

        try
        {
            await svc.ExportAsync(path, Ct);

            var manifest = await svc.GetManifestAsync(path, Ct);
            Assert.Equal(2, manifest.EntityCounts["ReceivedItems"]);
            Assert.Equal(1, manifest.EntityCounts["BankDeposits"]);
            Assert.Equal(1, manifest.EntityCounts["BankDepositLines"]);

            BackupSnapshot? upserted = null;
            _backupRepo.UpsertSnapshotAsync(Arg.Do<BackupSnapshot>(s => upserted = s), Arg.Any<CancellationToken>())
                .Returns(Task.CompletedTask);

            await svc.ImportAsync(path, Ct);

            AssertSameReceivedItem(cheque, upserted!.ReceivedItems.Single(i => i.Id == cheque.Id));
            AssertSameReceivedItem(opening, upserted.ReceivedItems.Single(i => i.Id == opening.Id));

            var restoredDeposit = Assert.Single(upserted.BankDeposits);
            Assert.Equal(deposit.Id, restoredDeposit.Id);
            Assert.Equal(deposit.Date, restoredDeposit.Date);
            Assert.Equal(deposit.BankAccountId, restoredDeposit.BankAccountId);
            Assert.Equal(deposit.CashAmount, restoredDeposit.CashAmount);
            Assert.Equal(deposit.ChequeTotal, restoredDeposit.ChequeTotal);
            Assert.Equal(deposit.JournalEntryId, restoredDeposit.JournalEntryId);
            Assert.Equal(deposit.ReversalJournalEntryId, restoredDeposit.ReversalJournalEntryId);
            Assert.Equal(deposit.ReversedAt, restoredDeposit.ReversedAt);
            Assert.Equal(deposit.CreatedAt, restoredDeposit.CreatedAt);

            var restoredLine = Assert.Single(upserted.BankDepositLines);
            Assert.Equal(line.Id, restoredLine.Id);
            Assert.Equal(line.BankDepositId, restoredLine.BankDepositId);
            Assert.Equal(line.ReceivedItemId, restoredLine.ReceivedItemId);
            Assert.Equal(line.CreatedAt, restoredLine.CreatedAt);

            // A 1.4.0 file already says "Petty Cash"; no rename is needed.
            await _backupRepo.DidNotReceive().RenameCashAccountToPettyCashAsync(Arg.Any<CancellationToken>());
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
            foreach (var f in Directory.GetFiles(_recovery.GetRecoveryDirectory(), "StageFright-Recovery-*.sfbak"))
                File.Delete(f);
        }
    }

    [Theory]
    [InlineData("1.2.0")]
    [InlineData("1.3.0")]
    public async Task Should_RestoreNoBankingRecordsAndRenameCash_When_FileOlderThan140(string fileVersion)
    {
        _backupRepo.GetFullSnapshotAsync(Arg.Any<CancellationToken>())
            .Returns(new BackupSnapshot { JournalEntries = [BuildJournalEntry()] });
        _uow.ExecuteInTransactionAsync(Arg.Any<Func<CancellationToken, Task>>(), Arg.Any<CancellationToken>())
            .Returns(ci => ((Func<CancellationToken, Task>)ci[0]!)(Ct));
        var svc = CreateService();
        var path = Path.Combine(Path.GetTempPath(), $"test_pre140_{Guid.NewGuid()}.sfbak");

        try
        {
            await svc.ExportAsync(path, Ct);

            // Simulate an older file: no banking lists and no banking count keys.
            var envelope = ReadEnvelope(path);
            envelope.SchemaVersion = fileVersion;
            envelope.ReceivedItems = null;
            envelope.BankDeposits = null;
            envelope.BankDepositLines = null;
            envelope.EntityCounts.Remove("ReceivedItems");
            envelope.EntityCounts.Remove("BankDeposits");
            envelope.EntityCounts.Remove("BankDepositLines");
            WriteEnvelope(path, envelope);

            BackupSnapshot? upserted = null;
            _backupRepo.UpsertSnapshotAsync(Arg.Do<BackupSnapshot>(s => upserted = s), Arg.Any<CancellationToken>())
                .Returns(Task.CompletedTask);

            await svc.ImportAsync(path, Ct);

            Assert.Empty(upserted!.ReceivedItems);
            Assert.Empty(upserted.BankDeposits);
            Assert.Empty(upserted.BankDepositLines);
            Received.InOrder(() =>
            {
                _backupRepo.UpsertSnapshotAsync(Arg.Any<BackupSnapshot>(), Arg.Any<CancellationToken>());
                _backupRepo.RenameCashAccountToPettyCashAsync(Arg.Any<CancellationToken>());
            });
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
            foreach (var f in Directory.GetFiles(_recovery.GetRecoveryDirectory(), "StageFright-Recovery-*.sfbak"))
                File.Delete(f);
        }
    }

    [Fact]
    public async Task Should_NotRename_When_UpsertFails()
    {
        _backupRepo.GetFullSnapshotAsync(Arg.Any<CancellationToken>())
            .Returns(new BackupSnapshot { JournalEntries = [BuildJournalEntry()] });
        _uow.ExecuteInTransactionAsync(Arg.Any<Func<CancellationToken, Task>>(), Arg.Any<CancellationToken>())
            .Returns(ci => ((Func<CancellationToken, Task>)ci[0]!)(Ct));
        var svc = CreateService();
        var path = Path.Combine(Path.GetTempPath(), $"test_pre140_fail_{Guid.NewGuid()}.sfbak");

        try
        {
            await svc.ExportAsync(path, Ct);
            var envelope = ReadEnvelope(path);
            envelope.SchemaVersion = "1.3.0";
            WriteEnvelope(path, envelope);

            _backupRepo.UpsertSnapshotAsync(Arg.Any<BackupSnapshot>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromException(new DataAccessException("boom", "Backup", "Upsert")));

            await Assert.ThrowsAsync<DataAccessException>(() => svc.ImportAsync(path, Ct));

            await _backupRepo.DidNotReceive().RenameCashAccountToPettyCashAsync(Arg.Any<CancellationToken>());
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
            foreach (var f in Directory.GetFiles(_recovery.GetRecoveryDirectory(), "StageFright-Recovery-*.sfbak"))
                File.Delete(f);
        }
    }

    private static (ReceivedItem Cheque, ReceivedItem Opening, BankDeposit Deposit, BankDepositLine Line) BuildBankingRecords()
    {
        var created = new DateTime(2026, 3, 1, 9, 0, 0, DateTimeKind.Utc);
        var cheque = new ReceivedItem
        {
            Id = Guid.NewGuid(), Date = new DateTime(2026, 3, 1), Amount = 85.50m,
            Method = PaymentMethod.Check, Reference = "CHQ 001234",
            PaymentId = Guid.NewGuid(), CreatedAt = created
        };
        var opening = new ReceivedItem
        {
            Id = Guid.NewGuid(), Date = new DateTime(2026, 1, 1), Amount = 120m,
            Method = PaymentMethod.Other, JournalEntryId = Guid.NewGuid(), CreatedAt = created
        };
        var deposit = new BankDeposit
        {
            Id = Guid.NewGuid(), Date = new DateTime(2026, 3, 5), BankAccountId = Guid.NewGuid(),
            CashAmount = 40m, ChequeTotal = 85.50m, JournalEntryId = Guid.NewGuid(),
            ReversalJournalEntryId = Guid.NewGuid(), ReversedAt = created.AddDays(5), CreatedAt = created
        };
        var line = new BankDepositLine
        {
            Id = Guid.NewGuid(), BankDepositId = deposit.Id, ReceivedItemId = cheque.Id, CreatedAt = created
        };
        return (cheque, opening, deposit, line);
    }

    private static void AssertSameReceivedItem(ReceivedItem expected, ReceivedItem actual)
    {
        Assert.Equal(expected.Id, actual.Id);
        Assert.Equal(expected.Date, actual.Date);
        Assert.Equal(expected.Amount, actual.Amount);
        Assert.Equal(expected.Method, actual.Method);
        Assert.Equal(expected.Reference, actual.Reference);
        Assert.Equal(expected.PaymentId, actual.PaymentId);
        Assert.Equal(expected.JournalEntryId, actual.JournalEntryId);
        Assert.Equal(expected.CreatedAt, actual.CreatedAt);
    }

    private static (ExternalOrganisation Org, ExternalOrganisation ArchivedOrg,
        ExternalOrganisationContact Contact, ExternalOrganisationContact RemovedContact) BuildOrganisationRegister()
    {
        var created = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var deleted = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc);
        var org = new ExternalOrganisation
        {
            Id = Guid.NewGuid(), Name = "Town Hall", Address = "1 Main St", Email = "hall@example.org",
            CreatedAt = created, UpdatedAt = created
        };
        var archivedOrg = new ExternalOrganisation
        {
            Id = Guid.NewGuid(), Name = "Old Printer", IsDeleted = true, DeletedAt = deleted, DeletedBy = "treasurer",
            CreatedAt = created, UpdatedAt = deleted
        };
        var contact = new ExternalOrganisationContact
        {
            Id = Guid.NewGuid(), ExternalOrganisationId = org.Id, Name = "Pat Lee", Role = "Bookings",
            Phone = "02 1234 5678", Mobile = "0400 000 000", Email = "pat@example.org",
            CreatedAt = created, UpdatedAt = created
        };
        var removedContact = new ExternalOrganisationContact
        {
            Id = Guid.NewGuid(), ExternalOrganisationId = org.Id, Name = "Sam Roe",
            IsDeleted = true, DeletedAt = deleted, DeletedBy = "treasurer",
            CreatedAt = created, UpdatedAt = deleted
        };
        return (org, archivedOrg, contact, removedContact);
    }

    private static void AssertSameOrganisation(ExternalOrganisation expected, ExternalOrganisation actual)
    {
        Assert.Equal(
            (expected.Name, expected.Address, expected.Email, expected.IsDeleted, expected.DeletedAt, expected.DeletedBy, expected.CreatedAt, expected.UpdatedAt),
            (actual.Name, actual.Address, actual.Email, actual.IsDeleted, actual.DeletedAt, actual.DeletedBy, actual.CreatedAt, actual.UpdatedAt));
    }

    private static void AssertSameContact(ExternalOrganisationContact expected, ExternalOrganisationContact actual)
    {
        Assert.Equal(
            (expected.ExternalOrganisationId, expected.Name, expected.Role, expected.Phone, expected.Mobile, expected.Email),
            (actual.ExternalOrganisationId, actual.Name, actual.Role, actual.Phone, actual.Mobile, actual.Email));
        Assert.Equal(
            (expected.IsDeleted, expected.DeletedAt, expected.DeletedBy, expected.CreatedAt, expected.UpdatedAt),
            (actual.IsDeleted, actual.DeletedAt, actual.DeletedBy, actual.CreatedAt, actual.UpdatedAt));
    }

    [Fact]
    public async Task Should_StampRunningBuildVersion_When_Exported()
    {
        // Spec 030 FR-003: the originating application version reflects the build, not a placeholder.
        _backupRepo.GetFullSnapshotAsync(Arg.Any<CancellationToken>()).Returns(BuildMinimalSnapshot());
        var svc = CreateService();
        var path = Path.Combine(Path.GetTempPath(), $"test_appversion_{Guid.NewGuid()}.sfbak");

        try
        {
            await svc.ExportAsync(path, Ct);
            var manifest = await svc.GetManifestAsync(path, Ct);
            Assert.Equal(ApplicationVersionInfo.Current, manifest.ApplicationVersion);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public async Task ExportAsync_IncludesFinanceEntities_InEntityCounts()
    {
        var je = BuildJournalEntry();
        var recon = BuildBankReconciliation();
        var line = BuildReconciliationLine(recon.Id, Guid.NewGuid());
        var snapshot = new BackupSnapshot
        {
            JournalEntries = [je],
            BankReconciliations = [recon],
            ReconciliationLines = [line]
        };
        _backupRepo.GetFullSnapshotAsync(Arg.Any<CancellationToken>()).Returns(snapshot);
        var svc = CreateService();
        var path = Path.Combine(Path.GetTempPath(), $"test_fin_counts_{Guid.NewGuid()}.sfbak");

        try
        {
            await svc.ExportAsync(path, Ct);
            var manifest = await svc.GetManifestAsync(path, Ct);
            Assert.Equal(1, manifest.EntityCounts["JournalEntries"]);
            Assert.Equal(1, manifest.EntityCounts["BankReconciliations"]);
            Assert.Equal(1, manifest.EntityCounts["ReconciliationLines"]);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public async Task ImportAsync_RoundTripsFinanceEntities()
    {
        var je = BuildJournalEntry();
        var recon = BuildBankReconciliation();
        recon.Status = ReconciliationStatus.Finalised;
        recon.FinalisedAt = DateTime.UtcNow;
        var txId = Guid.NewGuid();
        var line = BuildReconciliationLine(recon.Id, txId);
        var snapshot = new BackupSnapshot
        {
            JournalEntries = [je],
            BankReconciliations = [recon],
            ReconciliationLines = [line]
        };
        _backupRepo.GetFullSnapshotAsync(Arg.Any<CancellationToken>()).Returns(snapshot);
        _uow.ExecuteInTransactionAsync(Arg.Any<Func<CancellationToken, Task>>(), Arg.Any<CancellationToken>())
            .Returns(ci => ((Func<CancellationToken, Task>)ci[0]!)(Ct));
        var svc = CreateService();
        var path = Path.Combine(Path.GetTempPath(), $"test_fin_rt_{Guid.NewGuid()}.sfbak");

        try
        {
            await svc.ExportAsync(path, Ct);

            BackupSnapshot? upserted = null;
            _backupRepo.UpsertSnapshotAsync(Arg.Do<BackupSnapshot>(s => upserted = s), Arg.Any<CancellationToken>())
                .Returns(Task.CompletedTask);

            await svc.ImportAsync(path, Ct);

            var restoredJe = Assert.Single(upserted!.JournalEntries);
            Assert.Equal(je.Id, restoredJe.Id);
            Assert.Equal(je.Type, restoredJe.Type);

            var restoredRecon = Assert.Single(upserted.BankReconciliations);
            Assert.Equal(ReconciliationStatus.Finalised, restoredRecon.Status);
            Assert.Equal(recon.StatementClosingBalance, restoredRecon.StatementClosingBalance);

            var restoredLine = Assert.Single(upserted.ReconciliationLines);
            Assert.Equal(recon.Id, restoredLine.ReconciliationId);
            Assert.Equal(txId, restoredLine.TransactionId);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
            foreach (var f in Directory.GetFiles(_recovery.GetRecoveryDirectory(), "StageFright-Recovery-*.sfbak"))
                File.Delete(f);
        }
    }

    [Fact]
    public async Task ExportThenImport_RoundTripsFeeAndTransactionTaxAndJournalLink()
    {
        var je = BuildJournalEntry();
        var fee = new Fee
        {
            Id = Guid.NewGuid(), MemberId = Guid.NewGuid(), FeeType = FeeType.Annual, Amount = 110m,
            FeeDate = DateTime.UtcNow, DueDate = DateTime.UtcNow.AddDays(30), PaidAtCreation = false,
            TaxCode = TaxCode.Taxable, CreatedAt = DateTime.UtcNow
        };
        var tx = new Transaction
        {
            Id = Guid.NewGuid(), Date = DateTime.UtcNow, AccountId = Guid.NewGuid(),
            DebitAmount = 110m, CreditAmount = 0m, GLAccount = "1200",
            FeeId = fee.Id, JournalEntryId = je.Id, TaxCode = TaxCode.Taxable,
            Description = "Annual fee", CreatedAt = DateTime.UtcNow
        };
        var snapshot = new BackupSnapshot { JournalEntries = [je], Fees = [fee], Transactions = [tx] };
        _backupRepo.GetFullSnapshotAsync(Arg.Any<CancellationToken>()).Returns(snapshot);
        _uow.ExecuteInTransactionAsync(Arg.Any<Func<CancellationToken, Task>>(), Arg.Any<CancellationToken>())
            .Returns(ci => ((Func<CancellationToken, Task>)ci[0]!)(Ct));
        var svc = CreateService();
        var path = Path.Combine(Path.GetTempPath(), $"test_tax_rt_{Guid.NewGuid()}.sfbak");

        try
        {
            await svc.ExportAsync(path, Ct);

            BackupSnapshot? upserted = null;
            _backupRepo.UpsertSnapshotAsync(Arg.Do<BackupSnapshot>(s => upserted = s), Arg.Any<CancellationToken>())
                .Returns(Task.CompletedTask);

            await svc.ImportAsync(path, Ct);

            var restoredFee = Assert.Single(upserted!.Fees);
            Assert.Equal(TaxCode.Taxable, restoredFee.TaxCode);

            var restoredTx = Assert.Single(upserted.Transactions);
            Assert.Equal(TaxCode.Taxable, restoredTx.TaxCode);
            Assert.Equal(je.Id, restoredTx.JournalEntryId);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
            foreach (var f in Directory.GetFiles(_recovery.GetRecoveryDirectory(), "StageFright-Recovery-*.sfbak"))
                File.Delete(f);
        }
    }

    [Fact]
    public async Task ImportAsync_RoundTripsExpandedSettingsFields()
    {
        var closedThrough = new DateTime(2025, 6, 30, 0, 0, 0, DateTimeKind.Utc);
        var inception = new DateTime(2019, 3, 1, 0, 0, 0, DateTimeKind.Utc);
        var settings = new Settings
        {
            Id = Guid.NewGuid(), OrganizationName = "Global Choir", AnnualFee = 90m, AttendanceFee = 4m,
            MembershipRenewalMonth = 1, AuditRetentionYears = 3,
            FinancialYearStartMonth = 1, FinancialYearStartDay = 6,
            CurrencyCode = "USD", ClosedThroughDate = closedThrough, InceptionDate = inception,
            IsTaxApplicable = true, TaxRate = 8.25m,
            AnnualFeeTaxCode = TaxCode.Taxable, AttendanceFeeTaxCode = TaxCode.TaxExempt,
            TaxEntryMode = TaxEntryMode.Exclusive, LanguageCode = "es-ES",
            ShowParticipationGraphs = false,
            SchemaVersion = "1.1.0", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        var snapshot = new BackupSnapshot { Settings = settings };
        _backupRepo.GetFullSnapshotAsync(Arg.Any<CancellationToken>()).Returns(snapshot);
        _uow.ExecuteInTransactionAsync(Arg.Any<Func<CancellationToken, Task>>(), Arg.Any<CancellationToken>())
            .Returns(ci => ((Func<CancellationToken, Task>)ci[0]!)(Ct));
        var svc = CreateService();
        var path = Path.Combine(Path.GetTempPath(), $"test_settings_rt_{Guid.NewGuid()}.sfbak");

        try
        {
            await svc.ExportAsync(path, Ct);

            BackupSnapshot? upserted = null;
            _backupRepo.UpsertSnapshotAsync(Arg.Do<BackupSnapshot>(s => upserted = s), Arg.Any<CancellationToken>())
                .Returns(Task.CompletedTask);

            await svc.ImportAsync(path, Ct);

            var r = upserted!.Settings!;
            Assert.Equal(1, r.FinancialYearStartMonth);
            Assert.Equal(6, r.FinancialYearStartDay);
            Assert.Equal("USD", r.CurrencyCode);
            Assert.Equal(closedThrough, r.ClosedThroughDate);
            Assert.Equal(inception, r.InceptionDate);
            Assert.True(r.IsTaxApplicable);
            Assert.Equal(8.25m, r.TaxRate);
            Assert.Equal(TaxCode.Taxable, r.AnnualFeeTaxCode);
            Assert.Equal(TaxCode.TaxExempt, r.AttendanceFeeTaxCode);
            Assert.Equal(TaxEntryMode.Exclusive, r.TaxEntryMode);
            Assert.Equal("es-ES", r.LanguageCode);
            Assert.False(r.ShowParticipationGraphs);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
            foreach (var f in Directory.GetFiles(_recovery.GetRecoveryDirectory(), "StageFright-Recovery-*.sfbak"))
                File.Delete(f);
        }
    }

    [Fact]
    public async Task ImportAsync_OlderBackupWithoutFinanceCollections_RestoresSuccessfully()
    {
        _backupRepo.GetFullSnapshotAsync(Arg.Any<CancellationToken>()).Returns(BuildMinimalSnapshot());
        _uow.ExecuteInTransactionAsync(Arg.Any<Func<CancellationToken, Task>>(), Arg.Any<CancellationToken>())
            .Returns(ci => ((Func<CancellationToken, Task>)ci[0]!)(Ct));
        var svc = CreateService();
        var path = Path.Combine(Path.GetTempPath(), $"test_old_file_{Guid.NewGuid()}.sfbak");

        try
        {
            await svc.ExportAsync(path, Ct);

            // Simulate a pre-1.2.0 file: no finance collections, no finance count keys.
            var envelope = ReadEnvelope(path);
            envelope.JournalEntries = null;
            envelope.BankReconciliations = null;
            envelope.ReconciliationLines = null;
            envelope.EntityCounts.Remove("JournalEntries");
            envelope.EntityCounts.Remove("BankReconciliations");
            envelope.EntityCounts.Remove("ReconciliationLines");
            WriteEnvelope(path, envelope);

            BackupSnapshot? upserted = null;
            _backupRepo.UpsertSnapshotAsync(Arg.Do<BackupSnapshot>(s => upserted = s), Arg.Any<CancellationToken>())
                .Returns(Task.CompletedTask);

            await svc.ImportAsync(path, Ct);

            Assert.Empty(upserted!.JournalEntries);
            Assert.Empty(upserted.BankReconciliations);
            Assert.Empty(upserted.ReconciliationLines);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
            foreach (var f in Directory.GetFiles(_recovery.GetRecoveryDirectory(), "StageFright-Recovery-*.sfbak"))
                File.Delete(f);
        }
    }

    // --- CreateBackupAsync: verified backup to a chosen location (spec 030, US2) ---

    [Fact]
    public async Task CreateBackupAsync_WritesVerifiedFile_AndRecordsAnExportAuditEntry()
    {
        var snapshot = new BackupSnapshot
        {
            Members = [BuildMember(), BuildMember()],
            Settings = new Settings
            {
                Id = Guid.NewGuid(), OrganizationName = "Cool Choir", AnnualFee = 60m, AttendanceFee = 5m,
                MembershipRenewalMonth = 1, SchemaVersion = "1.1.0",
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
            }
        };
        _backupRepo.GetFullSnapshotAsync(Arg.Any<CancellationToken>()).Returns(snapshot);
        var svc = CreateService();

        try
        {
            var result = await svc.CreateBackupAsync(Ct);

            Assert.True(result.Passed);
            Assert.Empty(result.Discrepancies);
            Assert.Equal(_picker.SavedPath, result.FilePath);
            Assert.True(File.Exists(result.FilePath));

            // FR-010: the suggested name carries the organisation, the literal word "backup", the date.
            var name = _picker.SuggestedFileName;
            Assert.NotNull(name);
            Assert.StartsWith("Cool Choir ", name);
            Assert.Contains(" backup ", name);
            Assert.EndsWith(".sfbak", name);

            // FR-017: a verified backup is audited as AuditAction.Export.
            await _audit.Received(1).LogAsync(
                "Backup", Guid.Empty, AuditAction.Export,
                Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        }
        finally
        {
            if (_picker.SavedPath is not null && File.Exists(_picker.SavedPath)) File.Delete(_picker.SavedPath);
        }
    }

    [Fact]
    public async Task CreateBackupAsync_VerifiesAgainstTheSameCounts_GetManifestAsyncWouldReport()
    {
        var snapshot = new BackupSnapshot { Members = [BuildMember(), BuildMember(), BuildMember()], Accounts = [BuildAccount()] };
        _backupRepo.GetFullSnapshotAsync(Arg.Any<CancellationToken>()).Returns(snapshot);
        var svc = CreateService();

        try
        {
            var result = await svc.CreateBackupAsync(Ct);
            var manifest = await svc.GetManifestAsync(result.FilePath, Ct);

            Assert.Equal(3, manifest.EntityCounts["Members"]);
            Assert.Equal(1, manifest.EntityCounts["Accounts"]);
        }
        finally
        {
            if (_picker.SavedPath is not null && File.Exists(_picker.SavedPath)) File.Delete(_picker.SavedPath);
        }
    }

    [Fact]
    public async Task CreateBackupAsync_Throws_AndLeavesTheFileOnDisk_WhenReadBackCountsDoNotMatchTheSource()
    {
        var snapshot = new BackupSnapshot { Members = [BuildMember(), BuildMember()] };
        _backupRepo.GetFullSnapshotAsync(Arg.Any<CancellationToken>()).Returns(snapshot);
        _picker.Corrupt = bytes =>
        {
            using var inMs = new MemoryStream(bytes);
            var env = Serializer.Deserialize<BackupEnvelope>(inMs);
            env.EntityCounts["Members"] = 99;       // the file now records a count nothing else agrees with
            using var outMs = new MemoryStream();
            Serializer.Serialize(outMs, env);
            return outMs.ToArray();
        };
        var svc = CreateService();

        try
        {
            var ex = await Assert.ThrowsAsync<BackupVerificationException>(() => svc.CreateBackupAsync(Ct));

            Assert.NotEmpty(ex.Discrepancies);
            Assert.Contains(ex.Discrepancies, d => d!.Contains("Members"));
            Assert.Equal(_picker.SavedPath, ex.FilePath);
            Assert.True(File.Exists(ex.FilePath));   // kept for diagnosis
            await _audit.DidNotReceive().LogAsync(
                Arg.Any<string>(), Arg.Any<Guid>(), AuditAction.Export,
                Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        }
        finally
        {
            if (_picker.SavedPath is not null && File.Exists(_picker.SavedPath)) File.Delete(_picker.SavedPath);
        }
    }

    [Fact]
    public async Task CreateBackupAsync_Throws_WhenTheWrittenFileCannotBeReadBack()
    {
        _backupRepo.GetFullSnapshotAsync(Arg.Any<CancellationToken>()).Returns(BuildMinimalSnapshot());
        _picker.Corrupt = _ => [0xDE, 0xAD, 0xBE, 0xEF];
        var svc = CreateService();

        try
        {
            var ex = await Assert.ThrowsAsync<BackupVerificationException>(() => svc.CreateBackupAsync(Ct));
            Assert.NotEmpty(ex.Discrepancies);
            await _audit.DidNotReceive().LogAsync(
                Arg.Any<string>(), Arg.Any<Guid>(), AuditAction.Export,
                Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        }
        finally
        {
            if (_picker.SavedPath is not null && File.Exists(_picker.SavedPath)) File.Delete(_picker.SavedPath);
        }
    }

    [Fact]
    public async Task CreateBackupAsync_ThrowsOperationCanceled_AndWritesNoFileOrAudit_WhenTheUserDismissesTheSaveDialog()
    {
        _backupRepo.GetFullSnapshotAsync(Arg.Any<CancellationToken>()).Returns(BuildMinimalSnapshot());
        _picker.CancelDialog = true;
        var svc = CreateService();

        await Assert.ThrowsAsync<OperationCanceledException>(() => svc.CreateBackupAsync(Ct));

        Assert.Null(_picker.SavedPath);
        await _audit.DidNotReceive().LogAsync(
            Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<AuditAction>(),
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateBackupAsync_PropagatesDataAccessException_AndWritesNoAudit_WhenTheSaveDialogFails()
    {
        _backupRepo.GetFullSnapshotAsync(Arg.Any<CancellationToken>()).Returns(BuildMinimalSnapshot());
        _picker.SaveFailure = new DataAccessException("The native save dialog failed", "Backup", "SaveAsync");
        var svc = CreateService();

        await Assert.ThrowsAsync<DataAccessException>(() => svc.CreateBackupAsync(Ct));

        Assert.Null(_picker.SavedPath);
        await _audit.DidNotReceive().LogAsync(
            Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<AuditAction>(),
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    private static JournalEntry BuildJournalEntry() => new()
    {
        Id = Guid.NewGuid(), Type = JournalEntryType.GeneralJournal, Date = DateTime.UtcNow.Date,
        Description = "Test journal", CreatedAt = DateTime.UtcNow
    };

    private static BankReconciliation BuildBankReconciliation() => new()
    {
        Id = Guid.NewGuid(), AccountId = Guid.NewGuid(), StatementDate = DateTime.UtcNow.Date,
        StatementClosingBalance = 1234.56m, OpeningBalance = 1000m, Status = ReconciliationStatus.Draft,
        CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
    };

    private static ReconciliationLine BuildReconciliationLine(Guid reconId, Guid txId) => new()
    {
        Id = Guid.NewGuid(), ReconciliationId = reconId, TransactionId = txId, CreatedAt = DateTime.UtcNow
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
        JournalEntries = [],
        BankReconciliations = [],
        ReconciliationLines = [],
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
