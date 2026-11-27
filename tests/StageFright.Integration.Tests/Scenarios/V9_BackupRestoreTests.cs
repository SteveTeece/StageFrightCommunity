using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Exceptions;
using StageFright.Core.Modules.AuditTrail;
using StageFright.Core.Modules.Finance;
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
    private readonly TempRecoveryCopyStore _recovery = new();

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
            Assert.Equal(StageFright.Core.BackupSchema.CurrentSchemaVersion, manifest.SchemaVersion);
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
    public async Task Import_NewerSchemaVersion_ThrowsImportException_WithUpdateGuidance()
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
            Assert.Contains("update", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            CleanupFiles(path);
        }
    }

    [Fact]
    public async Task Import_CreatesCheckpoint_BeforeWrite()
    {
        // FR-015: a recovery copy is taken once setup is complete (Settings record present).
        _db.Settings.Add(new Settings
        {
            Id = Guid.NewGuid(), OrganizationName = "Test Choir",
            AnnualFee = 50m, AttendanceFee = 10m, MembershipRenewalMonth = 1,
            MaxAgeRangeYears = 150, MinimumMemberAge = 0, SchemaVersion = "1.1.0",
            FinancialYearStartMonth = 7, FinancialYearStartDay = 1,
            CurrencyCode = "AUD", IsTaxApplicable = false,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var svc = BuildService();
        var path = TempPath();

        try
        {
            await svc.ExportAsync(path, TestContext.Current.CancellationToken);
            var dir = Path.GetDirectoryName(path)!;

            await svc.ImportAsync(path, TestContext.Current.CancellationToken);

            var checkpoints = Directory.GetFiles(_recovery.GetRecoveryDirectory(), "StageFright-Recovery-*.sfbak");
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

    // --- spec 030 US4 / T035: older files restore and are accepted by startup;
    //     a current file round-trips every configured setting across a restart ---

    [Fact]
    public async Task Import_SyntheticPre120Envelope_Succeeds_NewCollectionsEmpty_SettingsTakeEntityDefaults_AndRoutesToDashboard_Integration()
    {
        // A real pre-1.2.0 .sfbak carries no JournalEntries / BankReconciliations / ReconciliationLines
        // members and none of the schema-1.2.0 Settings fields. The create path can no longer emit an
        // old version, so build the envelope by hand (FR-019 — an older file must still restore).
        var now = DateTime.UtcNow;
        var envelope = new BackupEnvelope
        {
            SchemaVersion = "1.1.0",
            GeneratedAt = now,
            ApplicationVersion = "0.9.0",
            Members = [],
            CommitteePositionRecords = [],
            Rehearsals = [],
            Events = [],
            Fees = [],
            Payments = [],
            Transactions = [],
            Accounts = [],
            AuditTrailEntries = [],
            AnnualGeneralMeetings = [],
            AgmAttendanceRecords = [],
            CommitteeOfficeHolderTypes = [],
            CommitteeTerms = [],
            // JournalEntries / BankReconciliations / ReconciliationLines intentionally omitted.
            Settings = new SettingsBackupDto
            {
                Id = Guid.NewGuid(), OrganizationName = "Legacy Choir",
                AnnualFee = 40m, AttendanceFee = 2m,
                MembershipRenewalMonth = 1, CommitteeRenewalMonth = 1,
                MaxAgeRangeYears = 150, MinimumMemberAge = 0, Theme = Theme.Dark,
                SchemaVersion = "1.1.0", CreatedAt = now, UpdatedAt = now
                // FinancialYearStartDay / CurrencyCode / TaxEntryMode / LanguageCode /
                // ShowParticipationGraphs / IsTaxApplicable left unset — a pre-1.2.0 file carries none.
            },
            EntityCounts = new Dictionary<string, int>
            {
                ["Members"] = 0, ["CommitteePositionRecords"] = 0, ["Rehearsals"] = 0,
                ["AnnualGeneralMeetings"] = 0, ["CommitteeOfficeHolderTypes"] = 0, ["CommitteeTerms"] = 0,
                ["Events"] = 0, ["Fees"] = 0, ["Payments"] = 0, ["Transactions"] = 0,
                ["Accounts"] = 0, ["AuditTrailEntries"] = 0, ["Settings"] = 1
            }
        };

        var svc = BuildService();
        var path = TempPath();

        try
        {
            WriteEnvelope(path, envelope);

            // (a) An older file restores without error.
            await svc.ImportAsync(path, TestContext.Current.CancellationToken);
            _db.ChangeTracker.Clear();

            // The three schema-1.2.0 collections come back empty.
            Assert.Equal(0, await _db.JournalEntries.CountAsync(TestContext.Current.CancellationToken));
            Assert.Equal(0, await _db.BankReconciliations.IgnoreQueryFilters().CountAsync(TestContext.Current.CancellationToken));
            Assert.Equal(0, await _db.ReconciliationLines.CountAsync(TestContext.Current.CancellationToken));

            // The schema-1.2.0 Settings fields take their entity defaults.
            var restored = await _db.Settings.SingleAsync(cancellationToken: TestContext.Current.CancellationToken);
            Assert.Equal(1, restored.FinancialYearStartDay);
            Assert.Equal("AUD", restored.CurrencyCode);
            Assert.Equal(TaxEntryMode.Inclusive, restored.TaxEntryMode);
            Assert.Null(restored.LanguageCode);
            Assert.True(restored.ShowParticipationGraphs);
            Assert.False(restored.IsTaxApplicable);

            // FR-012 acceptance 4 / FR-019: a restored Settings row is present, so the next App
            // routing decision (App.razor.cs -> ISetupService.IsSetupCompleteAsync) targets /dashboard,
            // not the first-run flow.
            Assert.NotNull(await new SettingsRepository(_db).GetAsync(TestContext.Current.CancellationToken));
        }
        finally
        {
            CleanupFiles(path);
        }
    }

    [Fact]
    public async Task Import_Current120File_WithNonDefaultConfig_AllPresentAfterSimulatedRestart_Integration()
    {
        var cashAccountId = new Guid("00000000-0000-0000-0000-000000000001"); // migration-seeded Cash
        var now = DateTime.UtcNow;

        var je = new JournalEntry
        {
            Id = Guid.NewGuid(), Type = JournalEntryType.GeneralJournal, Date = now.Date,
            Description = "Opening journal", CreatedAt = now
        };
        var tx = new Transaction
        {
            Id = Guid.NewGuid(), Date = now.Date, AccountId = cashAccountId,
            DebitAmount = 125m, CreditAmount = 0m, GLAccount = "1100",
            JournalEntryId = je.Id, TaxCode = TaxCode.Taxable, Description = "Journal line", CreatedAt = now
        };
        var recon = new BankReconciliation
        {
            Id = Guid.NewGuid(), AccountId = cashAccountId, StatementDate = now.Date,
            StatementClosingBalance = 125m, OpeningBalance = 0m, Status = ReconciliationStatus.Finalised,
            FinalisedAt = now, CreatedAt = now, UpdatedAt = now
        };
        var line = new ReconciliationLine
        {
            Id = Guid.NewGuid(), ReconciliationId = recon.Id, TransactionId = tx.Id, CreatedAt = now
        };
        var settings = new Settings
        {
            Id = Guid.NewGuid(), OrganizationName = "Cross-Version Choir", AnnualFee = 90m, AttendanceFee = 4m,
            MembershipRenewalMonth = 1, CommitteeRenewalMonth = 1,
            FinancialYearStartMonth = 3, FinancialYearStartDay = 15, CurrencyCode = "USD",
            IsTaxApplicable = true, TaxRate = 8.25m, AnnualFeeTaxCode = TaxCode.Taxable,
            TaxEntryMode = TaxEntryMode.Exclusive, LanguageCode = "es-ES",
            SchemaVersion = "1.1.0", CreatedAt = now, UpdatedAt = now
        };
        _db.JournalEntries.Add(je);
        _db.Transactions.Add(tx);
        _db.BankReconciliations.Add(recon);
        _db.ReconciliationLines.Add(line);
        _db.Settings.Add(settings);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var path = TempPath();
        StageFrightDbContext? target = null;

        try
        {
            await BuildService().ExportAsync(path, TestContext.Current.CancellationToken);

            var manifest = await BuildService().GetManifestAsync(path, TestContext.Current.CancellationToken);
            Assert.Equal(StageFright.Core.BackupSchema.CurrentSchemaVersion, manifest.SchemaVersion); // the create path always writes the current schema

            // Restore into a fresh database, then clear the change tracker to simulate an app restart.
            target = await NewDatabaseAsync();
            await BuildService(target).ImportAsync(path, TestContext.Current.CancellationToken);
            target.ChangeTracker.Clear();

            // FR-012 acceptance 1 & 4: every configured setting is reproduced exactly, and the presence
            // of the restored Settings row routes startup to /dashboard rather than the first-run flow.
            var restored = await new SettingsRepository(target).GetAsync(TestContext.Current.CancellationToken);
            Assert.NotNull(restored);
            Assert.Equal("USD", restored!.CurrencyCode);
            Assert.Equal("es-ES", restored.LanguageCode);
            Assert.Equal(3, restored.FinancialYearStartMonth);
            Assert.Equal(15, restored.FinancialYearStartDay);
            Assert.True(restored.IsTaxApplicable);
            Assert.Equal(8.25m, restored.TaxRate);
            Assert.Equal(TaxCode.Taxable, restored.AnnualFeeTaxCode);
            Assert.Equal(TaxEntryMode.Exclusive, restored.TaxEntryMode);

            // FR-016: journal entries and a finalised reconciliation (with its line) come back identical.
            var restoredJe = await target.JournalEntries.SingleAsync(j => j.Id == je.Id, TestContext.Current.CancellationToken);
            Assert.Equal(JournalEntryType.GeneralJournal, restoredJe.Type);
            Assert.Equal("Opening journal", restoredJe.Description);

            var restoredTx = await target.Transactions.SingleAsync(t => t.Id == tx.Id, TestContext.Current.CancellationToken);
            Assert.Equal(je.Id, restoredTx.JournalEntryId);
            Assert.Equal(TaxCode.Taxable, restoredTx.TaxCode);

            var restoredRecon = await target.BankReconciliations.IgnoreQueryFilters()
                .SingleAsync(r => r.Id == recon.Id, TestContext.Current.CancellationToken);
            Assert.Equal(ReconciliationStatus.Finalised, restoredRecon.Status);
            Assert.NotNull(restoredRecon.FinalisedAt);

            Assert.True(await target.ReconciliationLines.IgnoreQueryFilters()
                .AnyAsync(l => l.Id == line.Id && l.TransactionId == tx.Id, TestContext.Current.CancellationToken));
        }
        finally
        {
            if (target is not null)
            {
                await target.Database.CloseConnectionAsync();
                await target.DisposeAsync();
            }
            CleanupFiles(path);
        }
    }

    // --- spec 033: backup 1.4.0 banking records, and the Petty Cash rename for older files ---

    [Fact]
    public async Task Should_RoundTripBankingRecords_When_Version140FileRestoredIntoFreshDatabase_Integration()
    {
        var now = DateTime.UtcNow;
        var bank = new Account
        {
            Id = Guid.NewGuid(), Name = "Cheque Account", Type = AccountType.Asset, AccountNumber = "1010",
            SortOrder = 5, IsBankAccount = true, CreatedAt = now, UpdatedAt = now
        };
        var member = SeedMember("Carol", active: true);
        var payment = new Payment
        {
            Id = Guid.NewGuid(), MemberId = member.Id, Date = now.Date.AddDays(-3), Amount = 85.50m,
            PaymentMethod = PaymentMethod.Check, PaymentType = PaymentType.Annual, CreatedAt = now, UpdatedAt = now
        };
        var openingJe = new JournalEntry
        {
            Id = Guid.NewGuid(), Type = JournalEntryType.OpeningBalance, Date = now.Date.AddDays(-10),
            Description = "Opening balances", CreatedAt = now
        };
        var depositJe = new JournalEntry
        {
            Id = Guid.NewGuid(), Type = JournalEntryType.BankDeposit, Date = now.Date.AddDays(-2),
            Description = "Deposit", CreatedAt = now
        };
        var reversalJe = new JournalEntry
        {
            Id = Guid.NewGuid(), Type = JournalEntryType.BankDepositReversal, Date = depositJe.Date,
            Description = "Deposit reversal", CreatedAt = now
        };
        var redepositJe = new JournalEntry
        {
            Id = Guid.NewGuid(), Type = JournalEntryType.BankDeposit, Date = now.Date.AddDays(-1),
            Description = "Deposit again", CreatedAt = now
        };
        var cheque = new ReceivedItem
        {
            Id = Guid.NewGuid(), Date = payment.Date, Amount = payment.Amount, Method = PaymentMethod.Check,
            Reference = "CHQ 001234", PaymentId = payment.Id, CreatedAt = now
        };
        var opening = new ReceivedItem
        {
            Id = Guid.NewGuid(), Date = openingJe.Date, Amount = 120m, Method = PaymentMethod.Other,
            JournalEntryId = openingJe.Id, CreatedAt = now
        };
        var reversedDeposit = new BankDeposit
        {
            Id = Guid.NewGuid(), Date = depositJe.Date, BankAccountId = bank.Id, CashAmount = 40m,
            ChequeTotal = 85.50m, JournalEntryId = depositJe.Id, ReversalJournalEntryId = reversalJe.Id,
            ReversedAt = now, CreatedAt = now
        };
        var redeposit = new BankDeposit
        {
            Id = Guid.NewGuid(), Date = redepositJe.Date, BankAccountId = bank.Id, CashAmount = 0m,
            ChequeTotal = 85.50m, JournalEntryId = redepositJe.Id, CreatedAt = now
        };
        // The same cheque sits in the reversed deposit and in the re-recorded one.
        var reversedLine = new BankDepositLine
        {
            Id = Guid.NewGuid(), BankDepositId = reversedDeposit.Id, ReceivedItemId = cheque.Id, CreatedAt = now
        };
        var redepositLine = new BankDepositLine
        {
            Id = Guid.NewGuid(), BankDepositId = redeposit.Id, ReceivedItemId = cheque.Id, CreatedAt = now
        };
        _db.Accounts.Add(bank);
        _db.Members.Add(member);
        _db.Payments.Add(payment);
        _db.JournalEntries.AddRange(openingJe, depositJe, reversalJe, redepositJe);
        _db.ReceivedItems.AddRange(cheque, opening);
        _db.BankDeposits.AddRange(reversedDeposit, redeposit);
        _db.BankDepositLines.AddRange(reversedLine, redepositLine);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var path = TempPath();
        StageFrightDbContext? target = null;

        try
        {
            await BuildService().ExportAsync(path, TestContext.Current.CancellationToken);

            var manifest = await BuildService().GetManifestAsync(path, TestContext.Current.CancellationToken);
            Assert.Equal("1.4.0", manifest.SchemaVersion);
            Assert.Equal(2, manifest.EntityCounts["ReceivedItems"]);
            Assert.Equal(2, manifest.EntityCounts["BankDeposits"]);
            Assert.Equal(2, manifest.EntityCounts["BankDepositLines"]);

            target = await NewDatabaseAsync();
            await BuildService(target).ImportAsync(path, TestContext.Current.CancellationToken);
            target.ChangeTracker.Clear();

            var items = await target.ReceivedItems.OrderBy(i => i.Amount).ToListAsync(TestContext.Current.CancellationToken);
            Assert.Equal(2, items.Count);
            Assert.Equal(cheque.Id, items[0].Id);
            Assert.Equal("CHQ 001234", items[0].Reference);
            Assert.Equal(payment.Id, items[0].PaymentId);
            Assert.Null(items[0].JournalEntryId);
            Assert.Equal(opening.Id, items[1].Id);
            Assert.Equal(openingJe.Id, items[1].JournalEntryId);
            Assert.Equal(PaymentMethod.Other, items[1].Method);

            var restoredReversed = await target.BankDeposits.SingleAsync(d => d.Id == reversedDeposit.Id, TestContext.Current.CancellationToken);
            Assert.Equal(reversalJe.Id, restoredReversed.ReversalJournalEntryId);
            Assert.NotNull(restoredReversed.ReversedAt);
            Assert.Equal(40m, restoredReversed.CashAmount);
            Assert.Equal(125.50m, restoredReversed.Total);

            var restoredRedeposit = await target.BankDeposits.SingleAsync(d => d.Id == redeposit.Id, TestContext.Current.CancellationToken);
            Assert.Null(restoredRedeposit.ReversalJournalEntryId);
            Assert.Equal(bank.Id, restoredRedeposit.BankAccountId);

            var lines = await target.BankDepositLines.ToListAsync(TestContext.Current.CancellationToken);
            Assert.Equal(2, lines.Count);
            Assert.All(lines, l => Assert.Equal(cheque.Id, l.ReceivedItemId));

            // A current file leaves the chart as migrated: 1100 is Petty Cash, 1105 is present.
            var cash = await target.Accounts.SingleAsync(a => a.Id == SystemAccounts.CashId, TestContext.Current.CancellationToken);
            Assert.Equal("Petty Cash", cash.Name);
            Assert.True(await target.Accounts.AnyAsync(a => a.Id == SystemAccounts.UndepositedFundsId, TestContext.Current.CancellationToken));
        }
        finally
        {
            if (target is not null)
            {
                await target.Database.CloseConnectionAsync();
                await target.DisposeAsync();
            }
            CleanupFiles(path);
        }
    }

    [Theory]
    [InlineData("1.2.0")]
    [InlineData("1.3.0")]
    public async Task Should_RestoreWithNoBankingRecordsAndRenamePettyCash_When_FileOlderThan140_Integration(string fileVersion)
    {
        // An older file still names account 1100 "Cash on Hand", and the organisation may already
        // have its own account called "petty cash" — which must become "Petty Cash (own)".
        var now = DateTime.UtcNow;
        var cash = await _db.Accounts.SingleAsync(a => a.Id == SystemAccounts.CashId, TestContext.Current.CancellationToken);
        cash.Name = "Cash on Hand";
        var ownPettyCash = new Account
        {
            Id = Guid.NewGuid(), Name = "petty cash", Type = AccountType.Asset, AccountNumber = "1150",
            SortOrder = 6, CreatedAt = now, UpdatedAt = now
        };
        var cashTx = new Transaction
        {
            Id = Guid.NewGuid(), Date = now.Date, AccountId = SystemAccounts.CashId,
            DebitAmount = 30m, GLAccount = SystemAccounts.CashNumber, Description = "Raffle takings", CreatedAt = now
        };
        _db.Accounts.Add(ownPettyCash);
        _db.Transactions.Add(cashTx);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var path = TempPath();
        StageFrightDbContext? target = null;

        try
        {
            await BuildService().ExportAsync(path, TestContext.Current.CancellationToken);

            // Rewrite the export as an older file: no banking lists or count keys (and, for 1.2.0,
            // no organisation lists either).
            var envelope = ReadEnvelope(path);
            envelope.SchemaVersion = fileVersion;
            envelope.ReceivedItems = null;
            envelope.BankDeposits = null;
            envelope.BankDepositLines = null;
            envelope.EntityCounts.Remove("ReceivedItems");
            envelope.EntityCounts.Remove("BankDeposits");
            envelope.EntityCounts.Remove("BankDepositLines");
            if (fileVersion == "1.2.0")
            {
                envelope.ExternalOrganisations = null;
                envelope.ExternalOrganisationContacts = null;
                envelope.EntityCounts.Remove("ExternalOrganisations");
                envelope.EntityCounts.Remove("ExternalOrganisationContacts");
            }
            Assert.Contains(envelope.Accounts!, a => a.Id == SystemAccounts.CashId && a.Name == "Cash on Hand");
            WriteEnvelope(path, envelope);

            target = await NewDatabaseAsync();
            await BuildService(target).ImportAsync(path, TestContext.Current.CancellationToken);
            target.ChangeTracker.Clear();

            Assert.Equal(0, await target.ReceivedItems.CountAsync(TestContext.Current.CancellationToken));
            Assert.Equal(0, await target.BankDeposits.CountAsync(TestContext.Current.CancellationToken));
            Assert.Equal(0, await target.BankDepositLines.CountAsync(TestContext.Current.CancellationToken));

            // 1100 is renamed, keeping its number and its history.
            var restoredCash = await target.Accounts.SingleAsync(a => a.Id == SystemAccounts.CashId, TestContext.Current.CancellationToken);
            Assert.Equal("Petty Cash", restoredCash.Name);
            Assert.Equal(SystemAccounts.CashNumber, restoredCash.AccountNumber);
            Assert.True(await target.Transactions.AnyAsync(t => t.Id == cashTx.Id && t.AccountId == SystemAccounts.CashId, TestContext.Current.CancellationToken));

            var restoredOwn = await target.Accounts.SingleAsync(a => a.Id == ownPettyCash.Id, TestContext.Current.CancellationToken);
            Assert.Equal("Petty Cash (own)", restoredOwn.Name);

            var undeposited = await target.Accounts.SingleAsync(a => a.Id == SystemAccounts.UndepositedFundsId, TestContext.Current.CancellationToken);
            Assert.Equal(SystemAccounts.UndepositedFundsNumber, undeposited.AccountNumber);
        }
        finally
        {
            if (target is not null)
            {
                await target.Database.CloseConnectionAsync();
                await target.DisposeAsync();
            }
            CleanupFiles(path);
        }
    }

    // --- Helpers ---

    private BackupService BuildService() => BuildService(_db);

    private BackupService BuildService(StageFrightDbContext db)
    {
        var backupRepo = new BackupRepository(db);
        var uow = new UnitOfWork(db);
        var auditRepo = new AuditTrailRepository(db);
        var auditSvc = new AuditTrailService(auditRepo, NullLogger<AuditTrailService>.Instance);
        return new BackupService(backupRepo, uow, auditSvc, NullLogger<BackupService>.Instance, RealLocalizer.Instance,
            _recovery, new TempBackupDestinationPicker(_recovery.GetRecoveryDirectory()));
    }

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

    /// <summary>Writes the pre-restore recovery copy into the system temp directory for the test.</summary>
    private sealed class TempRecoveryCopyStore : IRecoveryCopyStore
    {
        // Its own folder, so parallel test runs never see or delete each other's recovery copies.
        private readonly string _directory = Directory.CreateDirectory(
            Path.Combine(Path.GetTempPath(), "StageFright-Tests", Guid.NewGuid().ToString("N"))).FullName;

        public string GetRecoveryDirectory() => _directory;
    }

    /// <summary>Stands in for the native Save dialog — writes the backup straight to the temp directory.</summary>
    private sealed class TempBackupDestinationPicker(string directory) : IBackupDestinationPicker
    {
        public async Task<BackupDestinationResult> SaveAsync(string suggestedFileName, Stream content, CancellationToken ct = default)
        {
            var path = Path.Combine(directory, $"sf_dest_{Guid.NewGuid()}.sfbak");
            await using var dest = File.Create(path);
            await content.CopyToAsync(dest, ct);
            return BackupDestinationResult.Saved(path);
        }
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

    private void CleanupFiles(string primaryPath)
    {
        if (File.Exists(primaryPath)) File.Delete(primaryPath);
        try { Directory.Delete(_recovery.GetRecoveryDirectory(), recursive: true); } catch { /* best-effort */ }
    }
}
