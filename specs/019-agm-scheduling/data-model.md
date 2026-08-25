# Data Model: Schedule Future AGMs

**Feature**: `019-agm-scheduling` | **Phase**: 1 (Design) | **Depends on**: [research.md](./research.md)

One entity field changes shape (`AnnualGeneralMeeting.IsRecorded`), one repository method is
added, one service method is added and one is re-shaped, and two request DTOs change. No new
entity, no new table, no new project.

## Changed entity: `AnnualGeneralMeeting`

`src/StageFright.Core/Entities/AnnualGeneralMeeting.cs`

```csharp
public class AnnualGeneralMeeting
{
    public Guid Id { get; set; }
    public DateTime Date { get; set; }
    public string? Notes { get; set; }
    public int? GeneralCommitteeSeatCountTarget { get; set; }

    /// <summary>
    /// NEW. True once attendance and elections have been recorded against this AGM (FR-004);
    /// false for a scheduled-but-not-yet-recorded AGM (FR-001, FR-002). Set once, at RecordAsync,
    /// and never cleared — tracked directly on the row, never inferred from AttendanceRecords
    /// (research.md Decision 1, spec Assumptions, Edge Case 5).
    /// </summary>
    public bool IsRecorded { get; set; }

    // --- Soft-delete / audit fields: unchanged ---
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public string? DeletedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public ICollection<AgmAttendanceRecord> AttendanceRecords { get; set; } = new List<AgmAttendanceRecord>();
}
```

**Validation rules**:
- `IsRecorded` starts `false` at `ScheduleAsync` and is set to `true` exactly once, inside
  `RecordAsync`'s transaction — no code path ever sets it back to `false`.
- `Date`/`Notes` are fixed at `ScheduleAsync` time and never rewritten by `RecordAsync` (spec
  Assumptions: "A scheduled AGM cannot be edited or rescheduled once saved").
- One non-archived `AnnualGeneralMeeting` per calendar year of `Date` (FR-015) — enforced by
  `AgmService.ScheduleAsync` via the new `IAgmRepository.ExistsForYearAsync` check (below), not by
  a database constraint (research.md Decision 3).

**State transitions**:

```
ScheduleAsync            RecordAsync
   (new row)  ──────►  IsRecorded=false  ──────►  IsRecorded=true
                        (scheduled)                (recorded, terminal)
```

There is no path back to `IsRecorded=false` and no in-place edit of `Date`/`Notes` at any stage —
the only other transition available from either state is `ArchiveAsync` (`IsDeleted=true`, FR-013,
unchanged behavior), which frees the row's calendar year for a replacement (FR-015).

**EF configuration**: No change to `AnnualGeneralMeetingConfiguration.cs` — `IsRecorded` is a
plain non-nullable `bool` column, configured the same implicit way `IsDeleted` already is (no
explicit `.IsRequired()` call for either flag).

**Migration**: One new EF Core migration (e.g. `AddIsRecordedToAgm`) adding the `IsRecorded`
column (`NOT NULL DEFAULT 0`), generated via:
```
dotnet ef migrations add AddIsRecordedToAgm --project src/StageFright.Data/ --startup-project src/StageFright.App/
```
Existing AGM rows (all created under today's always-complete `RecordAsync`) migrate to
`IsRecorded = 1` via a one-line `UPDATE` in the migration's `Up()` (every pre-existing row was, by
definition, already fully recorded — there was no "scheduled" state before this feature).

**Backup DTO**: `AnnualGeneralMeetingBackupDto`
(`src/StageFright.Core/Modules/Settings/Backup/AnnualGeneralMeetingBackupDto.cs`) mirrors the
entity 1:1 (constitution-documented convention) and gains one new field:
```csharp
[ProtoMember(10)] public bool IsRecorded { get; set; }
```
`BackupService.MapAgm`/`MapAgmFromDto` (`src/StageFright.Core/Modules/Settings/BackupService.cs`)
each gain one matching `IsRecorded = a.IsRecorded` / `IsRecorded = d.IsRecorded` line.

## Changed repository contract: `IAgmRepository`

`src/StageFright.Core/Contracts/IAgmRepository.cs`

```csharp
public interface IAgmRepository : ISoftDeletableRepository<AnnualGeneralMeeting>
{
    /// <summary>
    /// Returns every non-deleted AGM ordered most-recent-first — despite the name, this
    /// includes scheduled-but-not-yet-recorded AGMs (any Date, past or future); it applies no
    /// date filter beyond ordering.
    /// </summary>
    Task<IReadOnlyList<AnnualGeneralMeeting>> GetPastOrderedAsync(CancellationToken ct = default);

    /// <summary>
    /// NEW. True if a non-archived AGM already exists with a meeting Date in the given calendar
    /// year (FR-015). Archived AGMs are excluded automatically by the entity's global
    /// !IsDeleted query filter.
    /// </summary>
    Task<bool> ExistsForYearAsync(int year, CancellationToken ct = default);
}
```

`AgmRepository` implementation (`src/StageFright.Data/Repositories/AgmRepository.cs`):
```csharp
public async Task<bool> ExistsForYearAsync(int year, CancellationToken ct = default) =>
    await _db.AnnualGeneralMeetings.AnyAsync(a => a.Date.Year == year, ct);
```

## Changed service contract: `IAgmService`

`src/StageFright.Core/Contracts/IAgmService.cs`

```csharp
public interface IAgmService
{
    /// <summary>NEW. Schedules an AGM (date + optional notes only); creates no attendance,
    /// elected position, or committee term (FR-001, FR-002). Rejects a second non-archived AGM
    /// in the same calendar year (FR-015).</summary>
    /// <exception cref="ValidationException">Another non-archived AGM already exists for
    /// request.Date's calendar year.</exception>
    Task<AnnualGeneralMeeting> ScheduleAsync(ScheduleAgmRequest request, CancellationToken ct = default);

    /// <summary>CHANGED signature (was RecordAsync(RecordAgmRequest)). Records attendance and
    /// committee elections against a previously scheduled AGM (FR-004), updating that same row.
    /// </summary>
    /// <exception cref="EntityNotFoundException">agmId does not match a saved AGM.</exception>
    /// <exception cref="ValidationException">The AGM's Date is still in the future (FR-005), or
    /// it has already been recorded (FR-006), or a member is assigned more than one committee
    /// slot from this AGM (unchanged rule).</exception>
    Task<AnnualGeneralMeeting> RecordAsync(Guid agmId, RecordAgmRequest request, CancellationToken ct = default);

    // Unchanged:
    Task<AnnualGeneralMeeting?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<AnnualGeneralMeeting>> GetPastAsync(CancellationToken ct = default);
    Task ArchiveAsync(Guid id, string deletedBy, CancellationToken ct = default);
    Task<CommitteePositionRecord> RecordSpecialElectionAsync(RecordSpecialElectionRequest request, CancellationToken ct = default);
}
```

### `ScheduleAsync` — implementation shape

`src/StageFright.Core/Modules/Agm/AgmService.cs`

```csharp
public async Task<AnnualGeneralMeeting> ScheduleAsync(ScheduleAgmRequest request, CancellationToken ct = default)
{
    if (await _agmRepo.ExistsForYearAsync(request.Date.Year, ct))
        throw new ValidationException(
            $"An AGM already exists for {request.Date.Year}. Archive it before scheduling a replacement.",
            nameof(AnnualGeneralMeeting), nameof(ScheduleAsync));

    var now = DateTime.UtcNow;
    var agm = new AnnualGeneralMeeting
    {
        Id = Guid.NewGuid(),
        Date = request.Date,
        Notes = request.Notes,
        IsRecorded = false,
        CreatedAt = now,
        UpdatedAt = now
    };

    AnnualGeneralMeeting saved = null!;
    await _unitOfWork.ExecuteInTransactionAsync(async innerCt =>
    {
        saved = await _agmRepo.AddAsync(agm, innerCt);
        await _audit.LogAsync(nameof(AnnualGeneralMeeting), saved.Id, AuditAction.Create, ct: innerCt);
    }, ct);

    return saved;
}
```

### `RecordAsync` — re-shaped implementation

Loads the existing AGM instead of constructing one; guards on both `IsRecorded` and `Date`;
everything downstream of the guards (attendance-record creation, term rollover, position-record
creation) is unchanged except it reads `agm.Date` (the immutable scheduled date) wherever it
previously read `request.Date`, and the AGM row is `UpdateAsync`'d instead of `AddAsync`'d:

```csharp
public async Task<AnnualGeneralMeeting> RecordAsync(Guid agmId, RecordAgmRequest request, CancellationToken ct = default)
{
    var agm = await _agmRepo.GetByIdAsync(agmId, ct)
        ?? throw new EntityNotFoundException(nameof(AnnualGeneralMeeting), agmId, nameof(RecordAsync));

    if (agm.IsRecorded)
        throw new ValidationException(
            "This AGM has already been recorded.",
            nameof(AnnualGeneralMeeting), nameof(RecordAsync), agmId);

    if (agm.Date.Date > DateTime.Today)
        throw new ValidationException(
            "Attendance and elections cannot be recorded before the AGM's meeting date.",
            nameof(AnnualGeneralMeeting), nameof(RecordAsync), agmId);

    // ... unchanged duplicate-assignment check over OfficeHolderAssignments + GeneralCommitteeMemberIds ...

    await _unitOfWork.ExecuteInTransactionAsync(async innerCt =>
    {
        var now = DateTime.UtcNow;
        var settings = await _settingsService.GetAsync(innerCt);

        agm.GeneralCommitteeSeatCountTarget = settings?.GeneralCommitteeSeatCountTarget;
        agm.IsRecorded = true;
        agm.UpdatedAt = now;
        await _agmRepo.UpdateAsync(agm, innerCt);

        // ... unchanged: AgmAttendanceRecord batch, open-CommitteeTerm rollover, new CommitteeTerm,
        //     CommitteePositionRecord rows — every request.Date reference becomes agm.Date ...

        await _audit.LogAsync(nameof(AnnualGeneralMeeting), agm.Id, AuditAction.Update, ct: innerCt);
    }, ct);

    return agm;
}
```

Note the audit action for this same call site changes from `AuditAction.Create` to
`AuditAction.Update`, since `ScheduleAsync` now owns the row's Create event.

## Changed request DTOs

`src/StageFright.Core/Modules/Agm/ScheduleAgmRequest.cs` (**NEW**):
```csharp
/// <summary>Request to schedule an AGM ahead of time: meeting date and optional notes only.</summary>
public record ScheduleAgmRequest(DateTime Date, string? Notes);
```

`src/StageFright.Core/Modules/Agm/RecordAgmRequest.cs` (**CHANGED** — `Date`/`Notes` removed,
since they are already fixed on the AGM row from scheduling):
```csharp
/// <summary>Request to record attendance and every election against an already-scheduled AGM.</summary>
public record RecordAgmRequest(
    IReadOnlyList<Guid> AttendedMemberIds,
    IReadOnlyList<Guid> AllActiveMemberIds,
    IReadOnlyDictionary<Guid, Guid> OfficeHolderAssignments,
    IReadOnlyList<Guid> GeneralCommitteeMemberIds);
```

## Changed read model: `AgmAttendanceSheetService` / `AgmAttendanceSheetData`

`src/StageFright.Core/Modules/Agm/AgmAttendanceSheetService.cs` — no DTO shape change
(`AgmAttendanceSheetData`/`AgmAttendanceSheetMember` are unchanged from spec 018), but
`GenerateAsync` branches on `agm.IsRecorded` and gains a third constructor dependency,
`IMemberRepository`:

```csharp
public async Task<AgmAttendanceSheetData> GenerateAsync(Guid agmId, CancellationToken ct = default)
{
    var agm = await _agmRepo.GetByIdAsync(agmId, ct)
        ?? throw new EntityNotFoundException("AnnualGeneralMeeting", agmId, nameof(GenerateAsync));

    List<AgmAttendanceSheetMember> members;

    if (agm.IsRecorded)
    {
        var records = await _attendanceRepo.GetByAgmAsync(agmId, ct);
        members = records.Select(r => new AgmAttendanceSheetMember
        {
            FirstName = r.Member.FirstName,
            LastName = r.Member.LastName,
            Attended = r.Attended
        }).ToList();
    }
    else
    {
        var activeMembers = (await _memberRepo.GetByStatusAsync(MemberStatus.Active, ct))
            .OrderBy(m => m.LastName).ThenBy(m => m.FirstName)
            .ToList();
        members = activeMembers.Select(m => new AgmAttendanceSheetMember
        {
            FirstName = m.FirstName,
            LastName = m.LastName,
            Attended = false
        }).ToList();
    }

    return new AgmAttendanceSheetData { AgmDate = agm.Date, Members = members };
}
```

### Validation / preconditions (unchanged from spec 018 except as noted)

| Rule | Requirement | Exception |
|---|---|---|
| AGM must exist | `agmId` matches a non-deleted `AnnualGeneralMeeting` | `EntityNotFoundException("AnnualGeneralMeeting", agmId, nameof(GenerateAsync))` |
| No other precondition | Both an empty recorded roster and an empty scheduled (no active members) roster are valid results | — (empty `Members` list, not an exception; FR-012) |

`IAgmAttendanceSheetService`'s own interface signature is unchanged — only its documentation
comment gains a sentence describing the new branch.

## Relationships

- `AgmService` gains a read dependency it already effectively needed conceptually
  (`IAgmRepository.ExistsForYearAsync`) — no new repository class, no new DI registration line
  (same `IAgmRepository`/`AgmRepository` pair already registered).
- `AgmAttendanceSheetService` gains one new constructor dependency, `IMemberRepository` — already
  registered in `MauiProgram.cs` (used broadly elsewhere); no new DI line needed for the
  interface/implementation pair itself, only the new constructor parameter.
- No new entity, no new foreign key, no new navigation property.

## DI registration (`src/StageFright.App/MauiProgram.cs`)

No new lines — `IAgmRepository`/`AgmRepository`, `IAgmService`/`AgmService`, and
`IAgmAttendanceSheetService`/`AgmAttendanceSheetService` are already registered (lines 159, 195,
196); their added members/constructor parameter need no separate registration.
