# Quickstart: Printable Member Attendance Roll

**Feature**: `012-printable-attendance-roll` | **Depends on**: [data-model.md](./data-model.md), [contracts/attendance-roll-contract.md](./contracts/attendance-roll-contract.md)

This is a validation guide for confirming the feature works end-to-end once implemented — it is
not an implementation spec (see tasks.md, generated separately by `/speckit-tasks`, for that).

## Prerequisites

- Solution builds: `dotnet build` from the repo root (`StageFrightCommunity.slnx`).
- A local dev database exists (auto-created on first run at `TestData/stagefright.db`) with:
  - At least one scheduled `Rehearsal` (any date) with attendance **not yet recorded**.
  - A second scheduled `Rehearsal` with attendance **already recorded**, including: one member
    marked attended with their fee paid, one member marked attended with their fee marked unpaid
    (via the attendance grid's "mark as unpaid" option), and one member marked absent (or with no
    record at all).
  - At least one **active** `Member` (to verify inclusion) and at least one **archived/inactive**
    `Member` (to verify exclusion).
  - A member who changed status (active → inactive, or vice versa) after one of the seeded
    rehearsals' dates (to verify the point-in-time membership rule, FR-002).
  - Two members sharing the same surname (to verify first-name sub-sort, FR-004 / Edge Cases).
  - Enough active members to exceed one column's capacity on a page (to verify FR-009 overflow —
    see the `RowsPerColumn` constant in `AttendanceRollPdfRenderer` for the exact threshold once
    implemented).
  - A non-zero `Settings.AttendanceFee` value configured (to verify the fee column's header shows
    the correct amount).

The bundled `DebugDataSeeder` (`src/StageFright.App/Seeding/DebugDataSeeder.cs`) already seeds
demo members with a mix of fee/payment states and can be used as a starting point instead of
manual data entry, if it covers these cases.

## Automated validation (run before any manual check)

```bash
dotnet build
dotnet test tests/StageFright.Core.Tests/ --filter "FullyQualifiedName~AttendanceRollService"
dotnet test tests/StageFright.Reports.Tests/ --filter "FullyQualifiedName~AttendanceRollPdfRenderer"
dotnet test tests/StageFright.UI.Tests/ --filter "FullyQualifiedName~RehearsalList"
dotnet test tests/StageFright.Integration.Tests/ --filter "FullyQualifiedName~V3_RehearsalAttendance"
```

All of the above must pass before moving to manual verification. These map directly to the four
test layers described in plan.md's Constitution Check (§11 row) and enumerated in
[data-model.md](./data-model.md) / [research.md](./research.md).

## Manual validation scenarios

Run the app (`dotnet run --project src/StageFright.App/`) and navigate to **Rehearsals**
(`/rehearsals`).

### Scenario 1 — Basic roll generation, pre-attendance (User Story 1, P1)

1. Find the not-yet-recorded rehearsal in the list; click its **Print Roll** action.
2. **Expect**: a PDF opens (via the OS's default PDF viewer) listing every member active as of
   that rehearsal's date exactly once, sorted alphabetically by surname, each with blank
   "Present" and fee checkbox boxes, and the fee column's header showing the configured
   attendance fee (e.g. "$5").
3. **Expect**: no archived/inactive member appears anywhere on the roll; a member who was
   inactive as of that rehearsal's date but is active today does not appear, and vice versa.

### Scenario 2 — Empty active-as-of-date member list (User Story 1, Edge Case / FR-013)

1. Temporarily inactivate/archive every member (or test against a fresh, unseeded database).
2. Click **Print Roll** for any scheduled rehearsal.
3. **Expect**: an inline message is shown (e.g. "No active members found...") — no PDF is
   generated or opened.

### Scenario 3 — Real attendance and fee-payment state after recording (User Story 2, P2)

1. Using the rehearsal with attendance already recorded from Prerequisites, click **Print Roll**.
2. **Expect**: the attended-and-paid member's "Present" and fee boxes are both checked; the
   attended-but-unpaid member's "Present" box is checked while their fee box is empty; the
   absent/no-record member's "Present" and fee boxes are both empty.

### Scenario 4 — Compact two-column, print-friendly layout (User Story 3, P3)

1. Using a rehearsal whose active-as-of-date member count exceeds one column's capacity, click
   **Print Roll**.
2. **Expect**: the first column fills completely (in alphabetical order) before the list
   continues into the second column on the same page; if the roster is large enough, additional
   pages repeat the same two-column layout and column headings.
3. **Expect**: the "Present" and fee checkbox columns are visibly narrower than the name column.
4. **Expect**: every surname is rendered in capital letters; every column heading that's wider
   than its column wraps onto multiple lines rather than being cut off or widening the column.

### Scenario 5 — Re-generation reflects live data, membership stays point-in-time (spec Assumptions)

1. Print a roll for the not-yet-recorded rehearsal, note the member list.
2. Record attendance for that rehearsal (marking at least one member present), or change a
   member's active/inactive status.
3. Click **Print Roll** again for the same rehearsal.
4. **Expect**: "Present"/fee checkboxes reflect the newly recorded attendance — it is not a
   cached snapshot from the first printing. **Expect**: the member list itself is unchanged
   unless the status change affects whether that member was active as of the rehearsal's date
   (a status change made *today* does not add/remove them from a *past* rehearsal's roll).

## Rollback / no-op safety check

Since this feature is read-only (see contracts/attendance-roll-contract.md), a good sanity check
after any scenario above: confirm via the Members, Finance, and Rehearsals screens (or a database
inspection) that no `Member`, `Rehearsal`, `AttendanceRecord`, `Fee`, `Payment`, `Transaction`, or
GL record was created, changed, or removed by generating a roll — only a PDF file was written to
the OS temp directory and opened.
