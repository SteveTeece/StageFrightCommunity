using System.Reflection;
using StageFright.Core.Entities;
using StageFright.Core.Modules.Settings.Backup;
using SettingsEntity = StageFright.Core.Entities.Settings;

namespace StageFright.Core.Tests.Backup;

/// <summary>
/// Guards against ".sfbak drift" — a persisted entity field being added without a matching
/// backup DTO field, so a restore silently loses it (spec 030, SC-008). Every persisted
/// scalar property on an entity must have a same-named property on its BackupDto.
/// Navigation properties and get-only computed members are intentionally out of scope.
/// </summary>
public class BackupDtoFieldParityTests
{
    private static readonly (Type Entity, Type Dto)[] Pairs =
    [
        (typeof(Member), typeof(MemberBackupDto)),
        (typeof(CommitteePositionRecord), typeof(CommitteePositionRecordBackupDto)),
        (typeof(AnnualGeneralMeeting), typeof(AnnualGeneralMeetingBackupDto)),
        (typeof(AgmAttendanceRecord), typeof(AgmAttendanceRecordBackupDto)),
        (typeof(CommitteeOfficeHolderType), typeof(CommitteeOfficeHolderTypeBackupDto)),
        (typeof(CommitteeTerm), typeof(CommitteeTermBackupDto)),
        (typeof(Rehearsal), typeof(RehearsalBackupDto)),
        (typeof(AttendanceRecord), typeof(AttendanceRecordBackupDto)),
        (typeof(Event), typeof(EventBackupDto)),
        (typeof(EventType), typeof(EventTypeBackupDto)),
        (typeof(ParticipationRecord), typeof(ParticipationRecordBackupDto)),
        (typeof(Fee), typeof(FeeBackupDto)),
        (typeof(Payment), typeof(PaymentBackupDto)),
        (typeof(Transaction), typeof(TransactionBackupDto)),
        (typeof(JournalEntry), typeof(JournalEntryBackupDto)),
        (typeof(Account), typeof(AccountBackupDto)),
        (typeof(BankReconciliation), typeof(BankReconciliationBackupDto)),
        (typeof(ReconciliationLine), typeof(ReconciliationLineBackupDto)),
        (typeof(SettingsEntity), typeof(SettingsBackupDto)),
        (typeof(AuditTrailEntry), typeof(AuditTrailBackupDto)),
    ];

    [Fact]
    public void Should_ExposeEveryPersistedEntityProperty_When_ComparedToItsBackupDto()
    {
        var failures = new List<string>();

        foreach (var (entity, dto) in Pairs)
        {
            var dtoNames = dto
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(p => p.Name)
                .ToHashSet();

            var missing = entity
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(IsPersistedScalar)
                .Select(p => p.Name)
                .Where(name => !dtoNames.Contains(name))
                .ToList();

            if (missing.Count > 0)
                failures.Add($"{dto.Name} is missing backup coverage for {entity.Name}.{{{string.Join(", ", missing)}}}");
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    private static bool IsPersistedScalar(PropertyInfo p)
    {
        // A stored column round-trips through the DTO: it has a public setter (excludes
        // computed get-only members like Member.FullName) and a scalar type (excludes
        // navigation properties and child collections).
        if (p.SetMethod is not { IsPublic: true })
            return false;

        var t = Nullable.GetUnderlyingType(p.PropertyType) ?? p.PropertyType;
        if (t.IsEnum || t.IsPrimitive)
            return true;

        return t == typeof(string)
            || t == typeof(Guid)
            || t == typeof(DateTime)
            || t == typeof(DateTimeOffset)
            || t == typeof(TimeSpan)
            || t == typeof(decimal);
    }
}
