using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Modules.AuditTrail;
using StageFright.Core.Modules.Finance;
using StageFright.Core.Modules.Settings;
using StageFright.Data;
using StageFright.Data.Repositories;
using SettingsEntity = StageFright.Core.Entities.Settings;

namespace StageFright.Integration.Tests.Scenarios;

/// <summary>
/// Spec 030 US3 — a <c>.sfbak</c> create → restore round trip is byte-faithful across operating
/// system, regional format and time zone (FR-013, FR-014, FR-016; SC-002, SC-003). The format
/// work landed in commit <c>7d2cc93</c>; this is the correctness guarantee on it and ships with
/// <b>no new production code</b>. If it fails, the offending DTO member's protobuf wire form is
/// the defect — fix it in <c>src/StageFright.Core/Modules/Settings/Backup/*BackupDto.cs</c>, not
/// the assertion.
///
/// <para><b>Locale.</b> The restore half runs with <see cref="CultureInfo.CurrentCulture"/> /
/// <see cref="CultureInfo.CurrentUICulture"/> swapped to <c>de-DE</c> (comma decimal separator,
/// day-month-year order) — restored in <see cref="DisposeAsync"/>. The class joins the
/// process-global format-state collection so the swap never races a parallel test (see
/// <see cref="MoneyFormatterStateCollection"/>).</para>
///
/// <para><b>Time zone.</b> <see cref="TimeZoneInfo.Local"/> has no setter, so the time-zone
/// dimension is proven structurally: every restored <see cref="DateTime"/> keeps its exact
/// <see cref="DateTime.Ticks"/> and <see cref="DateTime.Kind"/>, denotes the same
/// <see cref="DateTime.ToUniversalTime"/> instant, and shows the same wall-clock reading when
/// converted into an arbitrary far-from-UTC zone (<see cref="FarZone"/>) — which is exactly what a
/// differently-zoned host applies. protobuf-net's default <c>DateTime</c> wire form
/// (<c>bcl.DateTime</c>: scaled ticks + kind) never consults the ambient zone on write or read.</para>
///
/// <para>No timing assertion — the dataset is representative (a few hundred members, thousands of
/// fee / payment / GL rows), and the spec sets no wall-clock target (Clarifications 2026-09-07).</para>
/// </summary>
[Collection("MoneyFormatterState")]
public sealed class CrossPlatformRoundTripTests : IAsyncLifetime
{
    private readonly CultureInfo _originalCulture = CultureInfo.CurrentCulture;
    private readonly CultureInfo _originalUiCulture = CultureInfo.CurrentUICulture;

    /// <summary>An arbitrary far-from-UTC zone standing in for "the other host's time zone".</summary>
    private static readonly TimeZoneInfo FarZone =
        TimeZoneInfo.CreateCustomTimeZone("SF-RoundTrip+13", TimeSpan.FromHours(13), "SF +13", "SF +13");

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private StageFrightDbContext _source = null!;   // the "outgoing treasurer" database
    private StageFrightDbContext _target = null!;   // the fresh install being restored into

    public async ValueTask InitializeAsync()
    {
        _source = await NewDatabaseAsync();
        _target = await NewDatabaseAsync();
    }

    public async ValueTask DisposeAsync()
    {
        CultureInfo.CurrentCulture = _originalCulture;
        CultureInfo.CurrentUICulture = _originalUiCulture;
        await _source.Database.CloseConnectionAsync();
        await _source.DisposeAsync();
        await _target.Database.CloseConnectionAsync();
        await _target.DisposeAsync();
    }

    [Fact]
    public async Task CreateThenRestore_UnderASwappedLocaleAndTimeZone_ReproducesTheSourceExactly_Integration()
    {
        var ct = TestContext.Current.CancellationToken;

        SeedRepresentativeDataset(_source);
        await _source.SaveChangesAsync(ct);
        var sourceSnapshot = await SnapshotAsync(_source, ct);

        var path = TempPath();
        try
        {
            // Create the backup under the source host's normal (test-runner) culture.
            await BuildService(_source).ExportAsync(path, ct);

            // ...then the restore runs on "the other machine": comma-decimal, d.M.yyyy order.
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("de-DE");

            await BuildService(_target).ImportAsync(path, ct);
            _target.ChangeTracker.Clear();

            var targetSnapshot = await SnapshotAsync(_target, ct);

            // FR-016 / SC-002 — field-for-field identical for every backed-up record type: records,
            // values, DateTime kind/instants, archived flags, and per-type counts (list lengths).
            // AuditTrailEntries is checked separately (the restore adds its own AuditAction.Import row).
            foreach (var type in sourceSnapshot.Keys.Where(k => k != "AuditTrailEntries").OrderBy(k => k))
                AssertJsonEqual(type, sourceSnapshot[type], targetSnapshot[type]);

            // FR-017 — every source audit row survives, plus exactly one new Import entry.
            await AssertAuditTrailPlusRestoreEntryAsync(ct);

            // FR-014 / SC-003 — same instants/days regardless of the two hosts' time zones.
            await AssertDateTimeInstantsPreservedAsync(ct);
        }
        finally
        {
            CleanupFiles(path);
        }
    }

    // --- representative dataset --------------------------------------------------

    private static void SeedRepresentativeDataset(StageFrightDbContext db)
    {
        var rng = new Random(30_028);
        var utc = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        // Organisation settings — every value deliberately non-default (FR-012).
        db.Settings.Add(new SettingsEntity
        {
            Id = Guid.NewGuid(),
            OrganizationName = "Öresund Sångförening",              // non-ASCII text, to stress encoding
            AnnualFee = 123.45m,
            AttendanceFee = 6.70m,
            MembershipRenewalMonth = 9,
            CommitteeRenewalMonth = 4,
            GeneralCommitteeSeatCountTarget = 9,
            FinancialYearStartMonth = 4,
            FinancialYearStartDay = 6,
            CurrencyCode = "JPY",                                   // 0-minor-digit currency
            ClosedThroughDate = new DateTime(2022, 3, 31, 0, 0, 0, DateTimeKind.Utc),
            InceptionDate = new DateTime(2011, 5, 20),              // Unspecified kind
            IsTaxApplicable = true,
            TaxRate = 8.25m,
            AnnualFeeTaxCode = TaxCode.Taxable,
            AttendanceFeeTaxCode = TaxCode.Excluded,
            TaxEntryMode = TaxEntryMode.Exclusive,
            MaxAgeRangeYears = 120,
            MinimumMemberAge = 16,
            Theme = Theme.Light,
            LanguageCode = "ja-JP",
            ShowParticipationGraphs = false,
            SchemaVersion = "1.1.0",
            AuditRetentionYears = 3,
            CreatedAt = utc,
            UpdatedAt = new DateTime(2024, 7, 1, 8, 30, 0, DateTimeKind.Utc),
        });

        // Members across several join years; ~1 in 7 archived.
        var memberIds = new List<Guid>();
        for (var i = 0; i < 200; i++)
        {
            var archived = i % 7 == 0;
            var inactive = archived || i % 11 == 0;
            var id = Guid.NewGuid();
            memberIds.Add(id);
            db.Members.Add(new Member
            {
                Id = id,
                FirstName = $"Given{i:D3}",
                LastName = i % 5 == 0 ? string.Empty : $"Surname{i:D3}",
                StreetAddress = $"{i + 1} Rehearsal Way",
                Phone = i % 3 == 0 ? null : $"+61 400 000 {i:D3}",
                Email = i % 4 == 0 ? null : $"member{i:D3}@example.org",
                JoinDate = new DateTime(2016 + i % 9, 1 + i % 12, 1 + i % 28),   // Unspecified kind
                DateOfBirth = i % 6 == 0 ? null : new DateTime(1960 + i % 45, 1 + i % 12, 1 + i % 28),
                Status = inactive ? MemberStatus.Inactive : MemberStatus.Active,
                ActivateDate = archived ? null : utc.AddDays(i),
                InactivateDate = i % 11 == 0 ? utc.AddDays(i + 400) : null,
                IsDeleted = archived,
                DeletedAt = archived ? utc.AddDays(i + 900) : null,
                DeletedBy = archived ? "system" : null,
                CreatedAt = utc.AddDays(i),
                UpdatedAt = utc.AddDays(i + 5),
            });
        }

        // Event types + events + participation.
        var eventTypeIds = new List<Guid>();
        for (var i = 0; i < 2; i++)
        {
            var id = Guid.NewGuid();
            eventTypeIds.Add(id);
            db.EventTypes.Add(new EventType
            {
                Id = id, Name = i == 0 ? "Concert" : "Workshop", IsSystemDefault = i == 0,
                IsDeleted = false, CreatedAt = utc, UpdatedAt = utc,
            });
        }
        var eventIds = new List<Guid>();
        for (var i = 0; i < 5; i++)
        {
            var id = Guid.NewGuid();
            eventIds.Add(id);
            db.Events.Add(new Event
            {
                Id = id,
                Date = new DateTime(2021 + i % 4, 6, 1 + i, 0, 0, 0, DateTimeKind.Utc),
                EventTypeId = eventTypeIds[i % eventTypeIds.Count],
                Notes = i % 2 == 0 ? $"Event {i} notes" : null,
                StoredParticipationRate = i % 3 == 0 ? null : Math.Round(0.5m + i * 0.07m, 4),
                IsDeleted = i == 4,
                DeletedAt = i == 4 ? utc.AddDays(1200) : null,
                DeletedBy = i == 4 ? "system" : null,
                CreatedAt = utc.AddDays(300 + i), UpdatedAt = utc.AddDays(305 + i),
            });
            for (var m = 0; m < 6; m++)
                db.ParticipationRecords.Add(new ParticipationRecord
                {
                    Id = Guid.NewGuid(), EventId = id, MemberId = memberIds[(i * 6 + m) % memberIds.Count],
                    Participated = (i + m) % 2 == 0, CreatedAt = utc.AddDays(300 + i),
                    IsDeleted = false,
                });
        }

        // Rehearsals + attendance.
        var rehearsalIds = new List<Guid>();
        for (var i = 0; i < 6; i++)
        {
            var id = Guid.NewGuid();
            rehearsalIds.Add(id);
            db.Rehearsals.Add(new Rehearsal
            {
                Id = id,
                Date = new DateTime(2022 + i % 3, 2, 1 + i, 0, 0, 0, DateTimeKind.Utc),
                Time = new TimeSpan(19, 30, 0),
                Notes = i % 2 == 0 ? null : $"Rehearsal {i}",
                StoredAttendanceRate = i % 2 == 0 ? Math.Round(0.4m + i * 0.05m, 4) : null,
                IsDeleted = i >= 4,
                DeletedAt = i >= 4 ? utc.AddDays(1300 + i) : null,
                DeletedBy = i >= 4 ? "system" : null,
                CreatedAt = utc.AddDays(500 + i), UpdatedAt = utc.AddDays(505 + i),
            });
            for (var m = 0; m < 6; m++)
                db.AttendanceRecords.Add(new AttendanceRecord
                {
                    Id = Guid.NewGuid(), RehearsalId = id, MemberId = memberIds[(i * 6 + m) % memberIds.Count],
                    Attended = (i + m) % 3 != 0, CreatedAt = utc.AddDays(500 + i), IsDeleted = false,
                });
        }

        // AGMs + AGM attendance.
        var agmIds = new List<Guid>();
        for (var i = 0; i < 3; i++)
        {
            var id = Guid.NewGuid();
            agmIds.Add(id);
            db.AnnualGeneralMeetings.Add(new AnnualGeneralMeeting
            {
                Id = id,
                Date = new DateTime(2021 + i, 4, 6, 0, 0, 0, DateTimeKind.Utc),
                Notes = i == 1 ? "Quorum met" : null,
                GeneralCommitteeSeatCountTarget = 9,
                IsRecorded = i < 2,
                IsDeleted = i == 2,
                DeletedAt = i == 2 ? utc.AddDays(1400) : null,
                DeletedBy = i == 2 ? "system" : null,
                CreatedAt = utc.AddDays(700 + i), UpdatedAt = utc.AddDays(705 + i),
            });
            for (var m = 0; m < 6; m++)
                db.AgmAttendanceRecords.Add(new AgmAttendanceRecord
                {
                    Id = Guid.NewGuid(), AnnualGeneralMeetingId = id, MemberId = memberIds[(i * 6 + m) % memberIds.Count],
                    Attended = (i + m) % 2 == 0, CreatedAt = utc.AddDays(700 + i), IsDeleted = false,
                });
        }

        // Committee terms + position records. Office-holder types are migration-seeded
        // (President/Secretary/Treasurer, GUIDs …0101/…0102/…0103; Name is UNIQUE), so
        // reference those rather than inserting our own.
        var officeHolderIds = new List<Guid>
        {
            new("00000000-0000-0000-0000-000000000101"),
            new("00000000-0000-0000-0000-000000000103"),
        };
        var termIds = new List<Guid>();
        for (var i = 0; i < 2; i++)
        {
            var id = Guid.NewGuid();
            termIds.Add(id);
            db.CommitteeTerms.Add(new CommitteeTerm
            {
                Id = id,
                StartedByAgmId = agmIds[i],                          // required FK
                StartDate = new DateTime(2021 + i, 4, 6, 0, 0, 0, DateTimeKind.Utc),
                EndDate = i == 0 ? new DateTime(2022, 4, 5, 0, 0, 0, DateTimeKind.Utc) : null,
                LabelYear = 2021 + i,
                CreatedAt = utc.AddDays(700 + i), UpdatedAt = utc.AddDays(705 + i),
            });
        }
        // Distinct (term, office) named roles + several general-committee members (null office).
        // Both filtered unique indexes want (term, office) and (term, member) unique among open,
        // non-archived rows — every pair below is unique and every member is used once.
        var positionCombos = new List<(Guid Term, Guid? Office)>();
        foreach (var term in termIds)
        {
            foreach (var office in officeHolderIds)
                positionCombos.Add((term, office));         // 2 terms × 2 offices = 4 named roles
            for (var g = 0; g < 5; g++)
                positionCombos.Add((term, null));           // general committee members
        }
        for (var i = 0; i < positionCombos.Count; i++)
        {
            var (term, office) = positionCombos[i];
            var archived = i == positionCombos.Count - 1;
            db.CommitteePositionRecords.Add(new CommitteePositionRecord
            {
                Id = Guid.NewGuid(),
                MemberId = memberIds[i],
                Year = null,
                Position = null,
                CommitteeTermId = term,
                OfficeHolderTypeId = office,
                StartDate = new DateTime(2021 + i % 2, 4, 6, 0, 0, 0, DateTimeKind.Utc),
                EndDate = office is null && i % 4 == 0 ? new DateTime(2022, 1, 1, 0, 0, 0, DateTimeKind.Utc) : null,
                IsDeleted = archived,
                DeletedAt = archived ? utc.AddDays(1500) : null,
                DeletedBy = archived ? "system" : null,
                CreatedAt = utc.AddDays(700 + i), UpdatedAt = utc.AddDays(705 + i),
            });
        }

        // GL journal entry headers.
        var journalIds = new List<Guid>();
        var journalTypes = Enum.GetValues<JournalEntryType>();
        for (var i = 0; i < 30; i++)
        {
            var id = Guid.NewGuid();
            journalIds.Add(id);
            db.JournalEntries.Add(new JournalEntry
            {
                Id = id,
                Type = journalTypes[i % journalTypes.Length],
                Date = new DateTime(2021 + i % 4, 1 + i % 12, 1 + i % 28, 0, 0, 0, DateTimeKind.Utc),
                Description = i % 3 == 0 ? null : $"Journal {i}",
                CreatedAt = utc.AddDays(800 + i),
            });
        }

        // Fees — for most members, a handful across years; tax codes varied incl. null.
        var feeIds = new List<Guid>();
        var taxCodes = new TaxCode?[] { null, TaxCode.Taxable, TaxCode.TaxExempt, TaxCode.Excluded };
        var feeTypes = Enum.GetValues<FeeType>();
        for (var m = 0; m < memberIds.Count; m++)
        {
            var count = 2 + rng.Next(4);
            for (var f = 0; f < count; f++)
            {
                var id = Guid.NewGuid();
                feeIds.Add(id);
                var year = 2018 + (m + f) % 7;
                db.Fees.Add(new Fee
                {
                    Id = id,
                    MemberId = memberIds[m],
                    FeeType = feeTypes[(m + f) % feeTypes.Length],
                    Amount = Math.Round(10m + (m % 13) + f * 2.5m + 0.34m, 2),
                    FeeDate = new DateTime(year, 1, 1),                       // Unspecified kind
                    DueDate = new DateTime(year, 12, 31),                     // Unspecified kind
                    PaidAtCreation = (m + f) % 2 == 0,
                    RehearsalId = f == 0 && m < rehearsalIds.Count ? rehearsalIds[m % rehearsalIds.Count] : null,
                    TaxCode = taxCodes[(m + f) % taxCodes.Length],
                    CreatedAt = utc.AddDays(m + f),
                });
            }
        }

        // Payments.
        var paymentIds = new List<Guid>();
        var payTypes = Enum.GetValues<PaymentType>();
        var payMethods = Enum.GetValues<PaymentMethod>();
        for (var m = 0; m < memberIds.Count; m++)
        {
            var count = 1 + rng.Next(4);
            for (var p = 0; p < count; p++)
            {
                var id = Guid.NewGuid();
                paymentIds.Add(id);
                var created = utc.AddDays(m + p * 3);
                db.Payments.Add(new Payment
                {
                    Id = id,
                    MemberId = memberIds[m],
                    Date = new DateTime(2019 + (m + p) % 6, 1 + (m + p) % 12, 1 + (m + p) % 28, 0, 0, 0, DateTimeKind.Utc),
                    Amount = Math.Round(5m + (m % 9) + p * 1.75m + 0.05m, 2),
                    PaymentMethod = payMethods[(m + p) % payMethods.Length],
                    PaymentType = payTypes[(m + p) % payTypes.Length],
                    Notes = (m + p) % 4 == 0 ? null : $"Receipt {m}-{p}",
                    CreatedAt = created,
                    UpdatedAt = (m + p) % 5 == 0 ? created.AddDays(1) : created,   // Notes edited on some
                });
            }
        }

        // GL transactions — balanced debit/credit pairs; ~half grouped under a journal entry.
        var txnIds = new List<Guid>();
        for (var p = 0; p < 1000; p++)
        {
            var amount = Math.Round(1m + (p % 97) + 0.11m * (p % 9), 2);
            var date = new DateTime(2019 + p % 6, 1 + p % 12, 1 + p % 28, 0, 0, 0, DateTimeKind.Utc);
            var created = utc.AddDays(p % 1500);
            var journalId = p % 2 == 0 ? journalIds[p % journalIds.Count] : (Guid?)null;
            var memberId = p % 3 == 0 ? memberIds[p % memberIds.Count] : (Guid?)null;
            var taxCode = taxCodes[p % taxCodes.Length];

            var debit = new Transaction
            {
                Id = Guid.NewGuid(), Date = date, AccountId = SystemAccounts.CashId,
                DebitAmount = amount, CreditAmount = 0m, GLAccount = "1100",
                MemberId = memberId, PaymentId = null,
                FeeId = p < 5 ? feeIds[p] : null,
                JournalEntryId = journalId, TaxCode = taxCode,
                Description = p % 4 == 0 ? null : $"Debit {p}", CreatedAt = created,
            };
            var credit = new Transaction
            {
                Id = Guid.NewGuid(), Date = date, AccountId = SystemAccounts.MemberReceivableId,
                DebitAmount = 0m, CreditAmount = amount, GLAccount = "1200",
                MemberId = memberId,
                PaymentId = p < 5 ? paymentIds[p] : null,
                FeeId = null, JournalEntryId = journalId, TaxCode = taxCode,
                Description = p % 4 == 0 ? null : $"Credit {p}", CreatedAt = created,
            };
            txnIds.Add(debit.Id);
            txnIds.Add(credit.Id);
            db.Transactions.Add(debit);
            db.Transactions.Add(credit);
        }

        // Bank reconciliations — one finalised, one archived draft — with cleared lines over
        // disjoint transaction ranges (FR-011 / FR-016 archived-row fidelity).
        var finalised = new BankReconciliation
        {
            Id = Guid.NewGuid(), AccountId = SystemAccounts.CashId,
            StatementDate = new DateTime(2023, 6, 30, 0, 0, 0, DateTimeKind.Utc),
            StatementClosingBalance = 4210.75m, OpeningBalance = 0m,
            Status = ReconciliationStatus.Finalised, FinalisedAt = utc.AddDays(1600),
            Notes = "June statement", IsDeleted = false,
            CreatedAt = utc.AddDays(1590), UpdatedAt = utc.AddDays(1600),
        };
        var archivedDraft = new BankReconciliation
        {
            Id = Guid.NewGuid(), AccountId = SystemAccounts.CashId,
            StatementDate = new DateTime(2023, 7, 31, 0, 0, 0, DateTimeKind.Utc),
            StatementClosingBalance = 5000m, OpeningBalance = 4210.75m,
            Status = ReconciliationStatus.Draft, FinalisedAt = null,
            Notes = null, IsDeleted = true, DeletedAt = utc.AddDays(1650), DeletedBy = "system",
            CreatedAt = utc.AddDays(1640), UpdatedAt = utc.AddDays(1650),
        };
        db.BankReconciliations.Add(finalised);
        db.BankReconciliations.Add(archivedDraft);
        for (var i = 0; i < 18; i++)
            db.ReconciliationLines.Add(new ReconciliationLine
            {
                Id = Guid.NewGuid(), ReconciliationId = finalised.Id, TransactionId = txnIds[i],
                CreatedAt = utc.AddDays(1595),
            });
        for (var i = 18; i < 24; i++)
            db.ReconciliationLines.Add(new ReconciliationLine
            {
                Id = Guid.NewGuid(), ReconciliationId = archivedDraft.Id, TransactionId = txnIds[i],
                CreatedAt = utc.AddDays(1645),
            });

        // Audit trail entries (older rows the backup carries verbatim).
        var actions = Enum.GetValues<AuditAction>();
        for (var i = 0; i < 12; i++)
            db.AuditTrailEntries.Add(new AuditTrailEntry
            {
                Id = Guid.NewGuid(),
                EntityType = i % 2 == 0 ? "Member" : "Payment",
                EntityId = memberIds[i % memberIds.Count],
                Action = actions[i % actions.Length],
                OldValue = i % 3 == 0 ? null : $"{{\"v\":{i}}}",
                NewValue = i % 4 == 0 ? null : $"{{\"v\":{i + 1}}}",
                UserId = "system",
                Timestamp = utc.AddDays(1700 + i),
            });
    }

    // --- assertions -----------------------------------------------------------

    private static void AssertJsonEqual(string type, string expected, string actual)
    {
        if (expected == actual) return;

        var at = FirstDifferenceIndex(expected, actual);
        Assert.Fail(
            $"Round-trip mismatch in '{type}' at char {at}.\n" +
            $"  source: …{Window(expected, at)}…\n" +
            $"  restored: …{Window(actual, at)}…");
    }

    private async Task AssertAuditTrailPlusRestoreEntryAsync(CancellationToken ct)
    {
        var src = (await _source.AuditTrailEntries.AsNoTracking().ToListAsync(ct)).OrderBy(a => a.Id).ToList();
        var tgt = (await _target.AuditTrailEntries.AsNoTracking().ToListAsync(ct)).OrderBy(a => a.Id).ToList();

        foreach (var s in src)
        {
            var match = Assert.Single(tgt, t => t.Id == s.Id);
            Assert.Equal(SerializeAudit(s), SerializeAudit(match));
        }

        // FR-017: the restore records its own AuditAction.Import against the restored database —
        // the one row the target legitimately holds that the source did not.
        var extra = Assert.Single(tgt, t => src.All(s => s.Id != t.Id));
        Assert.Equal(AuditAction.Import, extra.Action);
        Assert.Equal("Backup", extra.EntityType);

        static string SerializeAudit(AuditTrailEntry a) => JsonSerializer.Serialize(
            new { a.Id, a.EntityType, a.EntityId, a.Action, a.OldValue, a.NewValue, a.UserId, a.Timestamp, a.Timestamp.Kind },
            JsonOpts);
    }

    private async Task AssertDateTimeInstantsPreservedAsync(CancellationToken ct)
    {
        var srcTx = (await _source.Transactions.AsNoTracking().ToListAsync(ct)).OrderBy(t => t.Id).ToList();
        var tgtTx = (await _target.Transactions.AsNoTracking().ToListAsync(ct)).OrderBy(t => t.Id).ToList();
        Assert.Equal(srcTx.Count, tgtTx.Count);
        for (var i = 0; i < srcTx.Count; i++)
        {
            AssertSameInstant(srcTx[i].Date, tgtTx[i].Date);
            AssertSameInstant(srcTx[i].CreatedAt, tgtTx[i].CreatedAt);
        }

        var srcS = await _source.Settings.AsNoTracking().SingleAsync(ct);
        var tgtS = await _target.Settings.AsNoTracking().SingleAsync(ct);
        AssertSameInstant(srcS.CreatedAt, tgtS.CreatedAt);
        AssertSameInstant(srcS.UpdatedAt, tgtS.UpdatedAt);
        AssertSameInstant(srcS.InceptionDate, tgtS.InceptionDate);        // Unspecified kind
        AssertSameInstant(srcS.ClosedThroughDate, tgtS.ClosedThroughDate); // Utc kind

        var srcM = (await _source.Members.IgnoreQueryFilters().AsNoTracking().ToListAsync(ct)).OrderBy(m => m.Id).ToList();
        var tgtM = (await _target.Members.IgnoreQueryFilters().AsNoTracking().ToListAsync(ct)).OrderBy(m => m.Id).ToList();
        Assert.Equal(srcM.Count, tgtM.Count);
        for (var i = 0; i < srcM.Count; i++)
        {
            AssertSameInstant(srcM[i].JoinDate, tgtM[i].JoinDate);         // Unspecified kind
            AssertSameInstant(srcM[i].DateOfBirth, tgtM[i].DateOfBirth);   // nullable
            AssertSameInstant(srcM[i].CreatedAt, tgtM[i].CreatedAt);       // Utc kind
            AssertSameInstant(srcM[i].DeletedAt, tgtM[i].DeletedAt);       // nullable, archived rows
        }
    }

    private static void AssertSameInstant(DateTime source, DateTime restored)
    {
        Assert.Equal(source.Kind, restored.Kind);
        Assert.Equal(source.Ticks, restored.Ticks);
        Assert.Equal(source.ToUniversalTime(), restored.ToUniversalTime());
        // On a host sitting in FarZone the restored value shows the same wall-clock reading.
        Assert.Equal(TimeZoneInfo.ConvertTime(source, FarZone), TimeZoneInfo.ConvertTime(restored, FarZone));
    }

    private static void AssertSameInstant(DateTime? source, DateTime? restored)
    {
        Assert.Equal(source.HasValue, restored.HasValue);
        if (source.HasValue) AssertSameInstant(source.Value, restored!.Value);
    }

    // --- database snapshot ---------------------------------------------------

    /// <summary>
    /// The full backed-up surface of <paramref name="db"/> as one JSON string per record type —
    /// exactly the fields the <c>*BackupDto</c> mappers carry, ordered by id, with an explicit
    /// <c>Kind</c> alongside each primary timestamp. Two snapshots compare equal iff every record,
    /// value, archived flag, timestamp instant/kind and per-type count round-tripped.
    /// </summary>
    private static async Task<Dictionary<string, string>> SnapshotAsync(StageFrightDbContext db, CancellationToken ct)
    {
        var members = await db.Members.IgnoreQueryFilters().AsNoTracking().ToListAsync(ct);
        var committees = await db.CommitteePositionRecords.IgnoreQueryFilters().AsNoTracking().ToListAsync(ct);
        var agms = await db.AnnualGeneralMeetings.IgnoreQueryFilters().AsNoTracking().ToListAsync(ct);
        var agmAttendance = await db.AgmAttendanceRecords.IgnoreQueryFilters().AsNoTracking().ToListAsync(ct);
        var officeHolders = await db.CommitteeOfficeHolderTypes.IgnoreQueryFilters().AsNoTracking().ToListAsync(ct);
        var terms = await db.CommitteeTerms.IgnoreQueryFilters().AsNoTracking().ToListAsync(ct);
        var rehearsals = await db.Rehearsals.IgnoreQueryFilters().AsNoTracking().ToListAsync(ct);
        var attendance = await db.AttendanceRecords.IgnoreQueryFilters().AsNoTracking().ToListAsync(ct);
        var events = await db.Events.IgnoreQueryFilters().AsNoTracking().ToListAsync(ct);
        var eventTypes = await db.EventTypes.IgnoreQueryFilters().AsNoTracking().ToListAsync(ct);
        var participation = await db.ParticipationRecords.IgnoreQueryFilters().AsNoTracking().ToListAsync(ct);
        var fees = await db.Fees.IgnoreQueryFilters().AsNoTracking().ToListAsync(ct);
        var payments = await db.Payments.IgnoreQueryFilters().AsNoTracking().ToListAsync(ct);
        var transactions = await db.Transactions.IgnoreQueryFilters().AsNoTracking().ToListAsync(ct);
        var journalEntries = await db.JournalEntries.IgnoreQueryFilters().AsNoTracking().ToListAsync(ct);
        var accounts = await db.Accounts.IgnoreQueryFilters().AsNoTracking().ToListAsync(ct);
        var reconciliations = await db.BankReconciliations.IgnoreQueryFilters().AsNoTracking().ToListAsync(ct);
        var reconciliationLines = await db.ReconciliationLines.IgnoreQueryFilters().AsNoTracking().ToListAsync(ct);
        var settings = await db.Settings.IgnoreQueryFilters().AsNoTracking().ToListAsync(ct);
        var auditEntries = await db.AuditTrailEntries.AsNoTracking().ToListAsync(ct);

        static string J<T>(IEnumerable<T> rows) => JsonSerializer.Serialize(rows, JsonOpts);

        return new Dictionary<string, string>
        {
            ["Members"] = J(members.OrderBy(x => x.Id).Select(m => new
            {
                m.Id, m.FirstName, m.LastName, m.StreetAddress, m.Phone, m.Email,
                m.JoinDate, JoinDateKind = m.JoinDate.Kind, m.DateOfBirth, m.Status,
                m.ActivateDate, m.InactivateDate, m.IsDeleted, m.DeletedAt, m.DeletedBy,
                m.CreatedAt, CreatedAtKind = m.CreatedAt.Kind, m.UpdatedAt,
            })),
            ["CommitteePositionRecords"] = J(committees.OrderBy(x => x.Id).Select(c => new
            {
                c.Id, c.MemberId, c.Year, c.Position, c.CommitteeTermId, c.OfficeHolderTypeId,
                c.StartDate, c.EndDate, c.IsDeleted, c.DeletedAt, c.DeletedBy,
                c.CreatedAt, CreatedAtKind = c.CreatedAt.Kind, c.UpdatedAt,
            })),
            ["AnnualGeneralMeetings"] = J(agms.OrderBy(x => x.Id).Select(a => new
            {
                a.Id, a.Date, DateKind = a.Date.Kind, a.Notes, a.GeneralCommitteeSeatCountTarget,
                a.IsRecorded, a.IsDeleted, a.DeletedAt, a.DeletedBy,
                a.CreatedAt, CreatedAtKind = a.CreatedAt.Kind, a.UpdatedAt,
            })),
            ["AgmAttendanceRecords"] = J(agmAttendance.OrderBy(x => x.Id).Select(a => new
            {
                a.Id, a.AnnualGeneralMeetingId, a.MemberId, a.Attended,
                a.CreatedAt, CreatedAtKind = a.CreatedAt.Kind, a.IsDeleted, a.DeletedAt, a.DeletedBy,
            })),
            ["CommitteeOfficeHolderTypes"] = J(officeHolders.OrderBy(x => x.Id).Select(t => new
            {
                t.Id, t.Name, t.DisplayOrder, t.IsBuiltIn, t.IsDeleted, t.DeletedAt, t.DeletedBy,
                t.CreatedAt, CreatedAtKind = t.CreatedAt.Kind, t.UpdatedAt,
            })),
            ["CommitteeTerms"] = J(terms.OrderBy(x => x.Id).Select(t => new
            {
                t.Id, t.StartedByAgmId, t.StartDate, StartDateKind = t.StartDate.Kind, t.EndDate,
                t.LabelYear, t.CreatedAt, CreatedAtKind = t.CreatedAt.Kind, t.UpdatedAt,
            })),
            ["Rehearsals"] = J(rehearsals.OrderBy(x => x.Id).Select(r => new
            {
                r.Id, r.Date, DateKind = r.Date.Kind, r.Time, r.Notes, r.StoredAttendanceRate,
                r.IsDeleted, r.DeletedAt, r.DeletedBy,
                r.CreatedAt, CreatedAtKind = r.CreatedAt.Kind, r.UpdatedAt,
            })),
            ["AttendanceRecords"] = J(attendance.OrderBy(x => x.Id).Select(a => new
            {
                a.Id, a.RehearsalId, a.MemberId, a.Attended,
                a.CreatedAt, CreatedAtKind = a.CreatedAt.Kind, a.IsDeleted, a.DeletedAt, a.DeletedBy,
            })),
            ["Events"] = J(events.OrderBy(x => x.Id).Select(e => new
            {
                e.Id, e.Date, DateKind = e.Date.Kind, e.EventTypeId, e.Notes, e.StoredParticipationRate,
                e.IsDeleted, e.DeletedAt, e.DeletedBy,
                e.CreatedAt, CreatedAtKind = e.CreatedAt.Kind, e.UpdatedAt,
            })),
            ["EventTypes"] = J(eventTypes.OrderBy(x => x.Id).Select(t => new
            {
                t.Id, t.Name, t.IsSystemDefault, t.IsDeleted, t.DeletedAt, t.DeletedBy,
                t.CreatedAt, CreatedAtKind = t.CreatedAt.Kind, t.UpdatedAt,
            })),
            ["ParticipationRecords"] = J(participation.OrderBy(x => x.Id).Select(p => new
            {
                p.Id, p.EventId, p.MemberId, p.Participated,
                p.CreatedAt, CreatedAtKind = p.CreatedAt.Kind, p.IsDeleted, p.DeletedAt, p.DeletedBy,
            })),
            ["Fees"] = J(fees.OrderBy(x => x.Id).Select(f => new
            {
                f.Id, f.MemberId, f.FeeType, f.Amount, f.FeeDate, FeeDateKind = f.FeeDate.Kind,
                f.DueDate, f.PaidAtCreation, f.RehearsalId, f.TaxCode,
                f.CreatedAt, CreatedAtKind = f.CreatedAt.Kind,
            })),
            ["Payments"] = J(payments.OrderBy(x => x.Id).Select(p => new
            {
                p.Id, p.MemberId, p.Date, DateKind = p.Date.Kind, p.Amount, p.PaymentMethod,
                p.PaymentType, p.Notes, p.CreatedAt, CreatedAtKind = p.CreatedAt.Kind, p.UpdatedAt,
            })),
            ["Transactions"] = J(transactions.OrderBy(x => x.Id).Select(t => new
            {
                t.Id, t.Date, DateKind = t.Date.Kind, t.AccountId, t.DebitAmount, t.CreditAmount,
                t.GLAccount, t.MemberId, t.PaymentId, t.FeeId, t.JournalEntryId, t.TaxCode,
                t.Description, t.CreatedAt, CreatedAtKind = t.CreatedAt.Kind,
            })),
            ["JournalEntries"] = J(journalEntries.OrderBy(x => x.Id).Select(j => new
            {
                j.Id, j.Type, j.Date, DateKind = j.Date.Kind, j.Description,
                j.CreatedAt, CreatedAtKind = j.CreatedAt.Kind,
            })),
            ["Accounts"] = J(accounts.OrderBy(x => x.Id).Select(a => new
            {
                a.Id, a.Name, a.Type, a.AccountNumber, a.SortOrder, a.IsSystem, a.IsBankAccount,
                a.IsDeleted, a.DeletedAt, a.DeletedBy,
                a.CreatedAt, CreatedAtKind = a.CreatedAt.Kind, a.UpdatedAt,
            })),
            ["BankReconciliations"] = J(reconciliations.OrderBy(x => x.Id).Select(r => new
            {
                r.Id, r.AccountId, r.StatementDate, StatementDateKind = r.StatementDate.Kind,
                r.StatementClosingBalance, r.OpeningBalance, r.Status, r.FinalisedAt, r.Notes,
                r.IsDeleted, r.DeletedAt, r.DeletedBy,
                r.CreatedAt, CreatedAtKind = r.CreatedAt.Kind, r.UpdatedAt,
            })),
            ["ReconciliationLines"] = J(reconciliationLines.OrderBy(x => x.Id).Select(l => new
            {
                l.Id, l.ReconciliationId, l.TransactionId, l.CreatedAt, CreatedAtKind = l.CreatedAt.Kind,
            })),
            ["Settings"] = J(settings.OrderBy(x => x.Id).Select(s => new
            {
                s.Id, s.OrganizationName, s.AnnualFee, s.AttendanceFee, s.MembershipRenewalMonth,
                s.CommitteeRenewalMonth, s.MaxAgeRangeYears, s.MinimumMemberAge, s.Theme,
                s.GeneralCommitteeSeatCountTarget, s.SchemaVersion, s.AuditRetentionYears,
                s.FinancialYearStartMonth, s.FinancialYearStartDay, s.CurrencyCode,
                s.ClosedThroughDate, s.InceptionDate, InceptionDateKind = s.InceptionDate?.Kind,
                s.IsTaxApplicable, s.TaxRate, s.AnnualFeeTaxCode, s.AttendanceFeeTaxCode,
                s.TaxEntryMode, s.LanguageCode, s.ShowParticipationGraphs,
                s.IsDeleted, s.DeletedAt, s.DeletedBy,
                s.CreatedAt, CreatedAtKind = s.CreatedAt.Kind, s.UpdatedAt,
            })),
            ["AuditTrailEntries"] = J(auditEntries.OrderBy(x => x.Id).Select(a => new
            {
                a.Id, a.EntityType, a.EntityId, a.Action, a.OldValue, a.NewValue, a.UserId,
                a.Timestamp, TimestampKind = a.Timestamp.Kind,
            })),
        };
    }

    // --- infrastructure ----------------------------------------------------------

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
        new(new BackupRepository(db), new UnitOfWork(db),
            new AuditTrailService(new AuditTrailRepository(db), NullLogger<AuditTrailService>.Instance),
            NullLogger<BackupService>.Instance, RealLocalizer.Instance,
            new TempRecoveryCopyStore(), new TempBackupDestinationPicker());

    /// <summary>Writes the pre-restore recovery copy into the system temp directory for the test.</summary>
    private sealed class TempRecoveryCopyStore : IRecoveryCopyStore
    {
        public string GetRecoveryDirectory() => Path.GetTempPath();
    }

    /// <summary>Stands in for the native Save dialog — writes the backup straight to the temp directory.</summary>
    private sealed class TempBackupDestinationPicker : IBackupDestinationPicker
    {
        public async Task<BackupDestinationResult> SaveAsync(string suggestedFileName, Stream content, CancellationToken ct = default)
        {
            var path = Path.Combine(Path.GetTempPath(), $"sf_dest_{Guid.NewGuid()}.sfbak");
            await using var dest = File.Create(path);
            await content.CopyToAsync(dest, ct);
            return BackupDestinationResult.Saved(path);
        }
    }

    private static string TempPath() =>
        Path.Combine(Path.GetTempPath(), $"sf_xplat_{Guid.NewGuid()}.sfbak");

    private static void CleanupFiles(string primaryPath)
    {
        if (File.Exists(primaryPath)) File.Delete(primaryPath);
        var dir = Path.GetDirectoryName(primaryPath) ?? Path.GetTempPath();
        foreach (var f in Directory.GetFiles(dir, "StageFright-Recovery-*.sfbak"))
            try { File.Delete(f); } catch { /* best-effort */ }
        foreach (var f in Directory.GetFiles(dir, "sf_dest_*.sfbak"))
            try { File.Delete(f); } catch { /* best-effort */ }
    }

    private static int FirstDifferenceIndex(string a, string b)
    {
        var max = Math.Min(a.Length, b.Length);
        for (var i = 0; i < max; i++)
            if (a[i] != b[i]) return i;
        return max;
    }

    private static string Window(string s, int at)
    {
        var start = Math.Max(0, at - 80);
        var len = Math.Min(160, s.Length - start);
        return s.Substring(start, len);
    }
}
