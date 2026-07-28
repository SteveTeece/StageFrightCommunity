# Data Model: Printable Member Attendance Roll

**Feature**: `012-printable-attendance-roll` | **Phase**: 1 (Design) | **Depends on**: [research.md](./research.md)

No existing entity or table changes — this feature is entirely read-only (spec Assumptions:
"Generating the roll is a read-only operation"). It introduces two new plain DTOs and one new
service in `StageFright.Core`, consumed by one new renderer in `StageFright.Reports`.

> **Correction — 2026-07-28**: `AnnualFeePaid` is removed; `Attended` and `RehearsalFeePaid` are
> added (both now carry real per-rehearsal data instead of being always-blank); `AttendanceRollData`
> gains `AttendanceFeeAmount`. See spec.md's "Correction" clarification session and research.md
> Decisions 5, 8–10.

## New DTO: `AttendanceRollData`

`src/StageFright.Core/Modules/Rehearsals/AttendanceRollData.cs`

```csharp
public sealed class AttendanceRollData
{
    public DateTime RehearsalDate { get; init; }
    public TimeSpan RehearsalTime { get; init; }
    public decimal AttendanceFeeAmount { get; init; }
    public IReadOnlyList<AttendanceRollMember> Members { get; init; } = Array.Empty<AttendanceRollMember>();
}
```

- `RehearsalDate` / `RehearsalTime`: copied from the `Rehearsal` entity so the printed sheet can be
  identified and matched to the correct rehearsal (FR-008). No `RehearsalId` is needed on the DTO —
  it exists only to carry data from service to renderer within a single call.
- `AttendanceFeeAmount`: the current `Settings.AttendanceFee` value, read at generation time. The
  renderer formats this as the fee column's header text (FR-006; research.md Decision 9) — it is
  not a per-member value.
- `Members`: already sorted by surname then first name (FR-004) by the time the service returns it
  — the renderer does not re-sort. Empty when there are no members active as of the rehearsal's
  date (FR-013's precondition; see research.md Decision 6 for who handles the empty state).

## New DTO: `AttendanceRollMember`

`src/StageFright.Core/Modules/Rehearsals/AttendanceRollMember.cs`

```csharp
public sealed class AttendanceRollMember
{
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public bool Attended { get; init; }
    public bool RehearsalFeePaid { get; init; }
}
```

- `FirstName` / `LastName`: plain values, copied as-is from `Member`. Uppercasing the surname for
  display (FR-003) is a rendering concern, applied by `AttendanceRollPdfRenderer` when it builds
  the PDF text cell — not baked into this DTO, so the DTO stays a pure data carrier reusable by any
  future consumer without assuming a display transform.
- `Attended`: pre-computed boolean (FR-005) — `true` only if an `AttendanceRecord` exists for this
  member and rehearsal with `Attended == true`; `false` when no attendance has been recorded yet,
  or the member was recorded absent. See service description below for the exact rule.
- `RehearsalFeePaid`: pre-computed boolean (FR-006) — `true` only if an `Attendance`-type `Fee`
  exists for this member and rehearsal with no outstanding balance; `false` otherwise (including
  when the member attended but the fee was marked unpaid). Independent of `Attended` — see service
  description below.
- No `MemberId` field is included; the roll is a print-only, one-shot artifact with no interactive
  per-row action, so there's nothing downstream that needs to correlate a row back to a member id.
- No `AnnualFeePaid` field exists on this DTO — that column was removed; Annual Fee Paid is out of
  scope for this spec (research.md Decision 10).

## New service contract: `IAttendanceRollService`

`src/StageFright.Core/Modules/Rehearsals/IAttendanceRollService.cs`

```csharp
public interface IAttendanceRollService
{
    /// <summary>
    /// Assembles the printable attendance roll for a scheduled rehearsal: every member active as
    /// of the rehearsal's date (FR-002), sorted by surname then first name (FR-004), each with
    /// pre-computed Present (FR-005) and RehearsalFeePaid (FR-006) flags reflecting any attendance
    /// already recorded. Read-only — creates, updates, or deletes nothing.
    /// </summary>
    /// <exception cref="EntityNotFoundException">rehearsalId does not match a saved rehearsal.</exception>
    Task<AttendanceRollData> GenerateAsync(Guid rehearsalId, CancellationToken ct = default);
}
```

### `AttendanceRollService` — implementation shape

`src/StageFright.Core/Modules/Rehearsals/AttendanceRollService.cs`

Dependencies (constructor-injected, all existing interfaces — no new repository/service is
introduced): `IRehearsalRepository`, `IMemberRepository` (replaces `IMemberService`),
`IAttendanceRepository` (new dependency on this service), `IMemberBalanceService`,
`IFeeRepository`, `ISettingsRepository` (new dependency on this service).

```csharp
public async Task<AttendanceRollData> GenerateAsync(Guid rehearsalId, CancellationToken ct = default)
{
    var rehearsal = await _rehearsalRepo.GetByIdAsync(rehearsalId, ct)
        ?? throw new EntityNotFoundException("Rehearsal", rehearsalId, nameof(GenerateAsync));

    var members = (await _memberRepo.GetActiveAsOfAsync(rehearsal.Date, ct))
        .OrderBy(m => m.LastName).ThenBy(m => m.FirstName)
        .ToList();

    var attendanceByMember = (await _attendanceRepo.GetByRehearsalAsync(rehearsalId, ct))
        .ToDictionary(a => a.MemberId, a => a.Attended);

    var rollMembers = new List<AttendanceRollMember>();
    foreach (var member in members)
    {
        var attended = attendanceByMember.TryGetValue(member.Id, out var wasAttended) && wasAttended;
        var feePaid = await IsRehearsalFeePaidAsync(member.Id, rehearsalId, ct);
        rollMembers.Add(new AttendanceRollMember
        {
            FirstName = member.FirstName,
            LastName = member.LastName,
            Attended = attended,
            RehearsalFeePaid = feePaid
        });
    }

    var settings = await _settingsRepo.GetAsync(ct);

    return new AttendanceRollData
    {
        RehearsalDate = rehearsal.Date,
        RehearsalTime = rehearsal.Time,
        AttendanceFeeAmount = settings?.AttendanceFee ?? 0m,
        Members = rollMembers
    };
}

private async Task<bool> IsRehearsalFeePaidAsync(Guid memberId, Guid rehearsalId, CancellationToken ct)
{
    var memberFees = await _feeRepo.GetByMemberAsync(memberId, ct);
    var fee = memberFees.FirstOrDefault(f => f.FeeType == FeeType.Attendance && f.RehearsalId == rehearsalId);
    if (fee is null)
        return false; // No attendance fee recorded yet for this rehearsal -> unchecked (spec Edge Cases)

    var outstanding = await _memberBalanceService.GetOutstandingFeesAsync(memberId, ct);
    return !outstanding.Any(f => f.FeeId == fee.Id);
}
```

(`IFeeRepository.GetByMemberAsync(memberId)` and `IMemberBalanceService.GetOutstandingFeesAsync
(memberId)` — both confirmed existing — are exactly the two calls research.md Decision 5 needs; no
new repository method is required.)

### Validation / preconditions

| Rule | Requirement | Exception |
|---|---|---|
| Rehearsal must exist | `rehearsalId` matches a non-deleted `Rehearsal` | `EntityNotFoundException("Rehearsal", rehearsalId, nameof(GenerateAsync))` |
| No other precondition | Any active-as-of-date member count (including zero) is a valid result | — (empty `Members` list, not an exception; see research.md Decision 6) |

### State / lifecycle

Read-only — no state transitions. `GenerateAsync` may be called any number of times for the same
rehearsal (spec Assumption: "can be generated, and re-generated ... at any point after it has been
scheduled") and always reflects live data at call time, not a cached/historical snapshot.

## New renderer contract: `IAttendanceRollPdfRenderer`

`src/StageFright.Reports/Rendering/IAttendanceRollPdfRenderer.cs`

```csharp
public interface IAttendanceRollPdfRenderer
{
    /// <summary>
    /// Renders an attendance roll to PDF bytes: two-column layout (FR-009), minimal-width
    /// checkbox columns (FR-010), wrapping column headings (FR-011), surname in capitals
    /// alongside first name (FR-003). The returned array is non-empty on success, even for a
    /// zero-member roll (an empty roll is a valid, renderable — if not typically requested —
    /// document; the UI layer is what prevents generating one per FR-013).
    /// </summary>
    byte[] Render(AttendanceRollData data, string organizationName = "");
}
```

### `AttendanceRollPdfRenderer` — layout shape

`src/StageFright.Reports/Rendering/AttendanceRollPdfRenderer.cs`

- Header block (mirrors `PdfReportRenderer`'s header for visual consistency, FR-012): organization
  name, "Attendance Roll" title, rehearsal date/time subtitle (FR-008), generated-at timestamp.
- `private const int RowsPerColumn = <tuned constant>;` (see research.md Decision 3 and Outstanding
  Risks — exact value confirmed via a manual visual check during implementation).
- `data.Members.Chunk(RowsPerColumn * 2)` → one QuestPDF `container.Page(...)` per chunk; each page
  is a `Row` of two `Table` columns (left = first `RowsPerColumn` of the chunk, right = the rest).
- Each column `Table`'s `ColumnsDefinition`: one wide "Name" column (`RelativeColumn(4)`) showing
  `$"{m.LastName.ToUpperInvariant()}, {m.FirstName}"`, then two minimal-width checkbox columns
  (`RelativeColumn(1)` each, FR-010) — "Present" (empty or marked box per `m.Attended`) and a fee
  column headed by `data.AttendanceFeeAmount.ToString("C0")` (e.g. "$5"; research.md Decision 9,
  empty or marked box per `m.RehearsalFeePaid`). Column headers use QuestPDF's default text
  wrapping within the narrow width (FR-011) — no explicit `\n` needed.
- Checkbox cells: small bordered `Container` elements, not Unicode glyphs (research.md Decision 4).
- Document-wide footer: "Page X of Y" exactly as `PdfReportRenderer` already does, spanning across
  the multiple `Page()` blocks emitted for a large roster.
- Zero-member input (`data.Members.Count == 0`): `Chunk` on an empty sequence yields zero chunks,
  so the renderer emits a single page with just the header block and no member table — a valid,
  non-throwing result, even though the UI layer is expected to never call `Render` in this state
  (research.md Decision 6).

## Relationships

- `AttendanceRollService` reads `Rehearsal` (via `IRehearsalRepository`), `Member` (via
  `IMemberRepository`), `AttendanceRecord` (via `IAttendanceRepository`), `Fee` (via
  `IFeeRepository`), GL-derived balances (via `IMemberBalanceService`, itself backed by
  `IGLRepository`), and `Settings` (via `ISettingsRepository`) — purely as read dependencies. No
  foreign keys, navigation properties, or schema relationships are added.
- `AttendanceRollPdfRenderer` has no dependency on any repository or DbContext — it is a pure
  function of `AttendanceRollData` (plus an organization-name string), exactly like
  `PdfReportRenderer`'s relationship to `ReportData`.

## DI registration (`src/StageFright.App/MauiProgram.cs`)

Two new lines in `RegisterCoreServices`, alongside the existing neighbors:

```csharp
services.AddScoped<IAttendanceRollService, AttendanceRollService>();   // near IRehearsalService
services.AddScoped<IAttendanceRollPdfRenderer, AttendanceRollPdfRenderer>(); // near IPdfReportRenderer
```
