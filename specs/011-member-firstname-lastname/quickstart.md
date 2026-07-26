# Quickstart: Validate Split Member Name into First Name and Last Name

**Feature**: `011-member-firstname-lastname` | **Phase**: 1 (Design)

This guide validates the feature end-to-end once implemented, mapped to the spec's three user
stories and their acceptance scenarios. It assumes `tasks.md` (Phase 2) has been implemented.

## Prerequisites

```bash
dotnet restore
dotnet ef database update --project src/StageFright.Data/ --startup-project src/StageFright.App/
dotnet build
```

The SQLite database at `<repo-root>/TestData/stagefright.db` will have the
`SplitMemberNameIntoFirstLastName` migration applied — back it up first if it contains data you
want to compare pre/post-conversion (`copy TestData\stagefright.db TestData\stagefright.pre-migration.db`
on Windows).

## Automated validation (primary gate)

```bash
dotnet test
```

All five test projects must pass, including the new/updated coverage for this feature:
- `tests/StageFright.Core.Tests/Modules/Members/MemberNameSplitterTests.cs` — FR-006/FR-008 split-rule edge cases (trim, whitespace collapse, mononym, >100-char truncation)
- `tests/StageFright.Data.Tests/Migrations/SplitMemberNameIntoFirstLastNameTests.cs` (new) — seeds legacy `Name` rows, runs the migration, asserts the SQL-based split matches the same expected outputs as `MemberNameSplitterTests`
- `tests/StageFright.Core.Tests/Modules/Members/{MemberServiceTests,MemberValidationServiceTests}.cs` — required/max-length validation, audit old/new value capture (FR-002, FR-009, FR-011)
- `tests/StageFright.UI.Tests/Pages/Members/{MemberFormTests,MemberListTests,MemberDetailTests}.cs` — two-field entry, search-by-first/last/full name, sorted display
- `tests/StageFright.Reports.Tests/{MemberListReportProviderTests,MemberAccountSummaryReportProviderTests,CommitteeReportProviderTests}.cs` — "Last, First" column/label format, sort order
- `tests/StageFright.Integration.Tests/Scenarios/V2_MemberManagementTests.cs` and related scenario files — full user-journey coverage

## Manual validation — User Story 1 (P1): Enter first and last name separately

```bash
dotnet run --project src/StageFright.App/
```

1. Navigate to **Members → Add Member**.
2. Confirm the form shows separate **First Name** and **Last Name** inputs (no single "Name"
   field).
3. Enter a First Name and Last Name, fill remaining required fields, save.
   - **Expected**: Member is created; the confirmation/detail view shows `"{FirstName}
     {LastName}"` (entry order, FR-005).
4. Open **Edit Member** for that member.
   - **Expected**: First Name and Last Name fields are pre-populated with the saved values,
     independently editable.
5. Clear First Name (leave Last Name filled) and attempt to save.
   - **Expected**: Validation message shown; record not saved (FR-002).
6. Repeat with Last Name cleared instead.
   - **Expected**: Same validation behavior for Last Name independently.

## Manual validation — User Story 2 (P2): Find and browse members by name

1. On **Member List**, type a known member's **last name only** into search.
   - **Expected**: Matching member(s) appear (FR-004).
2. Clear search, type the same member's **first name only**.
   - **Expected**: Same member(s) appear.
3. Clear search, type the full name (`"First Last"`).
   - **Expected**: Same member(s) appear.
4. Click the Name column header to sort.
   - **Expected**: List sorts alphabetically by Last Name, then First Name, displayed as `"Last,
     First"` (FR-005).
5. Open **Committee** report, **Member Account Summary** report, and **Member List** report
   (Reports menu → Generate → PDF and CSV for each).
   - **Expected**: Each shows one combined Full Name column/label formatted `"Last, First"`, no
     missing/truncated/malformed values (FR-003, SC-003).
6. Open a **Rehearsal**'s Attendance grid and an **Event**'s Participation grid.
   - **Expected**: Member names render correctly and the grids sort consistently by Last, First.

## Manual validation — User Story 3 (P3): Existing records converted automatically

1. Before applying the migration (using the pre-migration DB backup from Prerequisites), note the
   total member count and a few sample `Name` values (e.g. via a SQLite browser or
   `sqlite3 TestData/stagefright.pre-migration.db "SELECT Name FROM Members LIMIT 10;"`).
2. Apply the migration (`dotnet ef database update ...`, per Prerequisites).
3. Re-check the member count — must be identical (SC-001, zero records lost/duplicated).
4. Spot-check the same sample members: a two-word `Name` like `"Jane Smith"` should now show
   `FirstName = "Jane"`, `LastName = "Smith"`. A single-word legacy `Name` (if any exist) should
   show the full value in `FirstName` with `LastName` blank, and must still appear normally in
   Member List (not hidden) — FR-008.
5. Confirm archived/inactive members converted identically — filter Member List to "Show
   Inactive" and repeat the spot-check.

## Backup/restore compatibility check (research.md Decision 6)

1. Using a backup taken **before** this feature was implemented (or a synthetic one with the old
   `MemberBackupDto` shape), restore it via **Settings → Backup/Restore**.
   - **Expected**: Restore succeeds; every restored member has non-empty `FirstName` (and
     `LastName` where the original `Name` had more than one word), derived from the same
     split rule as the live-upgrade migration.
2. Export a fresh backup post-feature, then restore it into the same (or a clean) database.
   - **Expected**: Round-trips `FirstName`/`LastName` exactly, no data loss.

## Success criteria checklist (from spec.md)

- [ ] SC-001: 100% of existing member records retain non-empty First Name and correct status after upgrade; zero lost/duplicated
- [ ] SC-002: Search by first name or last name returns correct results every time
- [ ] SC-003: All six MVP reports/screens show accurate, correctly formatted full names
- [ ] SC-004: Add/Edit Member with two fields takes no longer than the prior single-field flow (spot-check via manual timing, no added required steps)
- [ ] SC-005: Every sorted list/report orders consistently by Last Name, then First Name
