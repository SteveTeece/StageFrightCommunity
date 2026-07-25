# Quickstart: Validating the Committee Report Year Summary

This guide validates the redesigned Committee Report end-to-end. It assumes the implementation described in `plan.md`/`data-model.md`/`contracts/` is complete.

## Prerequisites

- `dotnet build` succeeds for the whole solution (`StageFrightCommunity.slnx`).
- Seed or enter committee membership data spanning at least two years, including:
  - A year with President, Secretary, and Treasurer all filled, plus one non-named position (e.g., "Welfare Officer") and at least two blank-position (general) members.
  - A year with a named role left unfilled (to see "Vacant").
  - A year where two members share the same position label (e.g., two people both recorded as "President") to exercise FR-010.
  - Position values entered with inconsistent casing/whitespace for the same role (e.g., `" president"` and `"President "`) to exercise FR-007.
  - At least one archived member with a committee record, to exercise the existing "Member Status" filter.

## Automated validation

```bash
dotnet test tests/StageFright.Reports.Tests/ --filter "FullyQualifiedName~CommitteeReportProviderTests"
dotnet test tests/StageFright.Integration.Tests/ --filter "FullyQualifiedName~V11_ReportsMenuTests"
```

Expected: all tests pass, including (once implemented per `plan.md`'s Constitution Check §11 list) cases for year grouping/ordering, named-role vacancy, other-position ordering, General Committee Members grouping, case/whitespace-insensitive matching, the Member Status filter, year omission when empty, duplicate-role members, and the empty-state case.

## Manual validation (live app)

1. Run `dotnet run --project src/StageFright.App/` and navigate to the Committee Report (Members module → Reports).
2. **User Story 1 (year summary)**: Confirm the report shows one row per year with committee records, most recent year first, each showing the year and a "Positions Recorded" count. Change the "Member Status" filter and confirm the counts/years update accordingly (Active Only / Archived Only / All).
3. **User Story 2 (role breakdown)**: Expand a year's row and confirm:
   - President, Secretary, and Treasurer each appear as their own line, showing the recorded member or "Vacant".
   - The non-named position (e.g., "Welfare Officer") appears as its own line, alphabetically ordered after the named roles.
   - Blank-position members appear together under "General Committee Members", sorted alphabetically, as the last line.
   - The year with duplicate role holders shows both members together on one line (e.g., "President: Alice, Bob") — neither is dropped.
   - The differently-cased/whitespaced position values for the same role collapse into a single line, not two.
4. **Edge case — empty filter result**: Switch the filter to a status with no committee records at all and confirm the report shows the existing empty-state grid message rather than a blank list of years or an error.
5. **User Story 3 (exports)**:
   - Click "Print / PDF" and confirm each year's summary and role breakdown are legible, with section breaks between years, and nothing splits confusingly across pages.
   - Click "Export CSV" and open the file — confirm every row includes the year and the position/member(s) it refers to (per `contracts/committee-report-row-shape-contract.md` rule 1), and that no information present on screen is missing from the CSV.

## Regression check

Run the full suite to confirm no other report or UI test regressed:

```bash
dotnet test
```

Expected: full solution build and test suite green, per `CLAUDE.md`'s Build & Test Verification requirement.
