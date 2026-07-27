# Quickstart: Printable Member Attendance Roll

**Feature**: `012-printable-attendance-roll` | **Depends on**: [data-model.md](./data-model.md), [contracts/attendance-roll-contract.md](./contracts/attendance-roll-contract.md)

This is a validation guide for confirming the feature works end-to-end once implemented — it is
not an implementation spec (see tasks.md, generated separately by `/speckit-tasks`, for that).

## Prerequisites

- Solution builds: `dotnet build` from the repo root (`StageFrightCommunity.slnx`).
- A local dev database exists (auto-created on first run at `TestData/stagefright.db`) with:
  - At least one scheduled `Rehearsal` (any date).
  - At least one **active** `Member` with a fully-paid current-year Annual fee.
  - At least one **active** `Member` with an unpaid or partially-paid current-year Annual fee.
  - At least one **active** `Member` with **no** current-year Annual fee record at all.
  - At least one **archived/inactive** `Member` (to verify exclusion).
  - Two members sharing the same surname (to verify first-name sub-sort, FR-004 / Edge Cases).
  - Enough active members to exceed one column's capacity on a page (to verify FR-009 overflow —
    see the `RowsPerColumn` constant in `AttendanceRollPdfRenderer` for the exact threshold once
    implemented).

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

### Scenario 1 — Basic roll generation (User Story 1, P1)

1. Find a scheduled rehearsal in the list; click its **Print Roll** action.
2. **Expect**: a PDF opens (via the OS's default PDF viewer) listing every active member exactly
   once, sorted alphabetically by surname, each with blank "Attended" and "Rehearsal Fee Paid"
   checkbox boxes.
3. **Expect**: no archived/inactive member appears anywhere on the roll.

### Scenario 2 — Empty active-member list (User Story 1, Edge Case / FR-013)

1. Temporarily inactivate/archive every member (or test against a fresh, unseeded database).
2. Click **Print Roll** for any scheduled rehearsal.
3. **Expect**: an inline message is shown (e.g. "No active members found...") — no PDF is
   generated or opened.

### Scenario 3 — Annual Fee Paid accuracy (User Story 2, P2)

1. Using the three fee-state members from Prerequisites, click **Print Roll**.
2. **Expect**: the fully-paid member's "Annual Fee Paid" box is marked/checked; the
   unpaid-or-partial member's box is empty; the no-record-this-year member's box is also empty
   (not checked) — confirming the Edge Case rule that "no record yet" behaves like "unpaid," not
   like "N/A."

### Scenario 4 — Compact two-column, print-friendly layout (User Story 3, P3)

1. Using a rehearsal whose active-member count exceeds one column's capacity, click **Print
   Roll**.
2. **Expect**: the first column fills completely (in alphabetical order) before the list
   continues into the second column on the same page; if the roster is large enough, additional
   pages repeat the same two-column layout and column headings.
3. **Expect**: the three checkbox columns are visibly narrower than the name column.
4. **Expect**: every surname is rendered in capital letters; every column heading that's wider
   than its column wraps onto multiple lines rather than being cut off or widening the column.

### Scenario 5 — Re-generation reflects live data (spec Assumptions)

1. Print a roll for a rehearsal, note the member list.
2. Change one active member to inactive (or vice versa), or record a payment that settles a
   previously-outstanding annual fee.
3. Click **Print Roll** again for the same rehearsal.
4. **Expect**: the newly-printed roll reflects the updated member/fee state — it is not a cached
   snapshot from the first printing.

## Rollback / no-op safety check

Since this feature is read-only (see contracts/attendance-roll-contract.md), a good sanity check
after any scenario above: confirm via the Members, Finance, and Rehearsals screens (or a database
inspection) that no `Member`, `Rehearsal`, `Fee`, `Payment`, `Transaction`, or GL record was
created, changed, or removed by generating a roll — only a PDF file was written to the OS temp
directory and opened.
