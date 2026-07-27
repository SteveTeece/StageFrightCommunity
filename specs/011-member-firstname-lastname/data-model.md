# Data Model: Split Member Name into First Name and Last Name

**Feature**: `011-member-firstname-lastname` | **Phase**: 1 (Design) | **Depends on**: [research.md](./research.md)

## Entity: `Member`

Source: `src/StageFright.Core/Entities/Member.cs`

### Field changes

| Field | Before | After | Notes |
|---|---|---|---|
| `Name` | `string`, required, max 255 (`Member.cs:15`) | **removed** | Replaced by `FirstName` + `LastName`. Dropped by the migration after backfill (research.md Decision 3). |
| `FirstName` | — | `string`, required, max **100** | New. Given name. Set/edited via `CreateMemberRequest`/`UpdateMemberRequest`. |
| `LastName` | — | `string`, required*, max **100** | New. Family name. *Required for all new/edited records (FR-002); may be an empty string on legacy records converted from a single-word `Name` until an administrator edits and re-saves that member (FR-008) — the DB column is `NOT NULL` with `''` as a legitimate stored value, not application-required for pre-existing rows. |
| `FullName` | — | `string`, computed, read-only | **Not mapped by EF.** `$"{FirstName} {LastName}".Trim()`. Used in entry/detail contexts (Add/Edit confirmation, Member Detail header) per FR-005's second clause. |
| `SortableFullName` | — | `string`, computed, read-only | **Not mapped by EF.** `$"{LastName}, {FirstName}"` (or just `LastName` when `FirstName` is empty, and vice versa, to avoid a stray leading/trailing comma-space). Used everywhere names are sorted, searched, or listed (FR-005's first clause, FR-003's report-column format). |

All other `Member` fields (`Id`, `StreetAddress`, `Phone`, `Email`, `JoinDate`, `DateOfBirth`,
`Status`, `ActivateDate`, `InactivateDate`, soft-delete fields, audit fields,
`CommitteeMemberships`) are unchanged.

### Validation rules (`MemberValidationService.ValidateCommon`)

| Rule | Requirement | Exception |
|---|---|---|
| `FirstName` required | Non-null, non-whitespace | `ValidationException("First name is required.", "Member", operationContext)` |
| `LastName` required | Non-null, non-whitespace | `ValidationException("Last name is required.", "Member", operationContext)` |
| `FirstName` max length | ≤ 100 characters (after trim) | `ValidationException("First name must be 100 characters or fewer.", "Member", operationContext)` |
| `LastName` max length | ≤ 100 characters (after trim) | `ValidationException("Last name must be 100 characters or fewer.", "Member", operationContext)` |

These four checks replace the single existing `Name` required-check (`MemberValidationService.cs:36-37`)
and apply identically to `CreateMemberRequest` and `UpdateMemberRequest` (both routed through
`ValidateCommon`, matching the existing structure). No uniqueness constraint on
`FirstName`+`LastName` is added (per spec Clarifications, duplicates are explicitly allowed).

### State / lifecycle

No change to `Member`'s lifecycle (Active/Inactive/soft-delete via `Status`, `IsDeleted`). The
name split is a field-shape change only; it does not introduce new states or transitions.

### Relationships

Unchanged — `Member` 1-to-many `CommitteeMembership`, plus existing (not navigation-mapped in
`Member.cs` but referenced elsewhere) relationships to `AttendanceRecord`, `ParticipationRecord`,
`Fee`, `Payment`, `Transaction` via `MemberId` foreign keys. None of these are affected; they key
off `Member.Id`, never `Member.Name`.

## EF Core Mapping

`src/StageFright.Data/Configurations/MemberConfiguration.cs`:

```csharp
// Before:
builder.Property(m => m.Name).IsRequired().HasMaxLength(255);

// After:
builder.Property(m => m.FirstName).IsRequired().HasMaxLength(100);
builder.Property(m => m.LastName).IsRequired().HasMaxLength(100);
// FullName / SortableFullName: no builder.Property() call — computed, unmapped by default.
```

## Migration: `SplitMemberNameIntoFirstLastName`

Naming follows the existing PascalCase-verb-phrase convention (see
`ConvertCategoriesToAccounts`, `AddShowParticipationGraphs`). Generated via:

```bash
dotnet ef migrations add SplitMemberNameIntoFirstLastName --project src/StageFright.Data/ --startup-project src/StageFright.App/
```

Because `dotnet ef migrations add` auto-generates the scaffold from the updated `Member`/
`MemberConfiguration` model (which will show `Name` removed, `FirstName`/`LastName` added), the
generated `Up()`/`Down()` bodies must be **hand-edited** immediately after scaffolding — exactly
as `ConvertCategoriesToAccounts` was — to replace the auto-generated `DropColumn(Name)` +
`AddColumn(FirstName)` + `AddColumn(LastName)` sequence with the backfill-safe SQL from
research.md Decision 3:

1. `AddColumn` FirstName (nullable), `AddColumn` LastName (nullable) — do **not** let EF
   auto-generate these as `NOT NULL` yet; the backfill runs before the columns are locked down.
2. `Sql(...)`: trim `Name`.
3. `Sql(...)` ×10: collapse internal whitespace runs in `Name`.
4. `Sql(...)`: split `Name` on first space into `FirstName`/`LastName`, truncating each to 100
   chars (handles FR-008's mononym case — `LastName` becomes `''`).
5. `AlterColumn` FirstName/LastName to `NOT NULL` (EF Core's SQLite provider performs the
   necessary table rebuild automatically for this operation — no manual rebuild code needed).
6. `DropColumn` `Name`.
7. No `Settings.SchemaVersion` change (research.md Decision 9).

`Down()` reverses: `AddColumn` `Name` (nullable) → `Sql("UPDATE Members SET Name =
TRIM(FirstName || ' ' || LastName)")` → `AlterColumn` `Name` to `NOT NULL` with `HasMaxLength(255)`
→ `DropColumn` FirstName, `DropColumn` LastName. (Round-trip is lossy only in the sense that
original irregular whitespace/casing isn't recoverable — acceptable, since `Down()` migrations in
this codebase are a dev/rollback safety net, not a guaranteed lossless inverse; see
`ConvertCategoriesToAccounts.Down()` for the same precedent of an approximate, not exact, inverse.)

## Request/Response DTOs

`src/StageFright.Core/Modules/Members/CreateMemberRequest.cs`,
`UpdateMemberRequest.cs`:

```csharp
// Before: public string Name { get; init; } = string.Empty;
// After:
public string FirstName { get; init; } = string.Empty;
public string LastName { get; init; } = string.Empty;
```

`MemberService.CreateAsync`/`UpdateAsync` map `FirstName = request.FirstName.Trim()`,
`LastName = request.LastName.Trim()` in place of the single `Name = request.Name.Trim()` line.

## Backup DTO

`src/StageFright.Core/Modules/Settings/Backup/MemberBackupDto.cs` (protobuf-net, field-number-bound
— see research.md Decision 6):

```csharp
[ProtoContract]
public class MemberBackupDto
{
    [ProtoMember(1)] public Guid Id { get; set; }
    [ProtoMember(2)] public string LegacyName { get; set; } = string.Empty;  // renamed from Name; same field #, wire-compatible with old backups
    [ProtoMember(3)] public string StreetAddress { get; set; } = string.Empty;
    // ... ProtoMember(4) through (15) unchanged ...
    [ProtoMember(16)] public string FirstName { get; set; } = string.Empty;  // new
    [ProtoMember(17)] public string LastName { get; set; } = string.Empty;   // new
}
```

`BackupService.MapMember` (export, `BackupService.cs:272`): populate `FirstName`/`LastName`;
leave `LegacyName` blank.

`BackupService.MapMemberFromDto` (restore, `BackupService.cs:383`): if `FirstName` and `LastName`
are both empty and `LegacyName` is non-empty, call `MemberNameSplitter.Split(d.LegacyName)` to
populate them; otherwise use `d.FirstName`/`d.LastName` directly.

## New utility: `MemberNameSplitter`

`src/StageFright.Core/Modules/Members/MemberNameSplitter.cs` — static, pure function
implementing FR-006/FR-008's rule in C# (used by backup restore and covered directly by unit
tests; see research.md Decision 4):

```csharp
public static class MemberNameSplitter
{
    public static (string FirstName, string LastName) Split(string combinedName)
    {
        // 1. Trim + collapse internal whitespace to single spaces
        // 2. Split on first remaining space
        // 3. Truncate each side to 100 characters
        // Mononym input -> (FirstName: value, LastName: "")
    }
}
```

## Consumer inventory (read-side changes, no schema impact)

Every direct `.Name` read on a `Member` instance switches to `.FullName` or `.SortableFullName`
per research.md Decision 2's context rule:

| Consumer | Property to use | Reason |
|---|---|---|
| `MemberForm.razor(.cs)` | Two bound inputs: `_form.FirstName`, `_form.LastName` | Direct entry, not a display read |
| `MemberDetail.razor` | `FullName` | Detail header — entry order |
| `MemberList.razor(.cs)` grid + search | `SortableFullName` (display/sort), `FirstName`/`LastName`/`FullName` (search match) | List/sort context |
| `EventDetail.razor`, `ParticipationGrid.razor.cs` | `SortableFullName` | Grid/list context |
| `AttendanceGrid.razor(.cs)` | `SortableFullName` | Grid/list context |
| `MemberBalanceList.razor` (via `MemberBalanceService.MemberBalance.Name`) | Rename `MemberBalance.Name` field to carry `SortableFullName`'s value | List context |
| `PaymentForm.razor.cs` (`_memberName`) | `FullName` | Single-member display on a form, not a sorted list |
| `MemberListReportProvider.cs` | `SortableFullName` for sort key and cell value | Report, sorted |
| `MemberAccountSummaryReportProvider.cs` | `SortableFullName` for sort key, section heading, summary label | Report, sorted |
| `CommitteeReportProvider.cs` | `SortableFullName` fed into existing `JoinAlphabetically` | Report, sorted |
| `PaymentService.cs` GL `Description` strings | `FullName` | Human-readable transaction note, not a sorted list |
