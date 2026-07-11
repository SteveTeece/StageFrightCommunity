# Quickstart: Validating the Member Account Summary Report Redesign

## Prerequisites

- Repo built successfully: `dotnet restore && dotnet build` from the repo root.
- A local dev database with at least: two active members with fees at different aging buckets, one active member with no outstanding fees, and one archived (soft-deleted) member with an outstanding fee — so all Edge Cases in `spec.md` are exercisable.

## Automated validation

```bash
# Full suite — must be green, including the 5 unaffected reports
dotnet test

# Just this feature's provider + viewer tests
dotnet test tests/StageFright.Reports.Tests/ --filter "FullyQualifiedName~MemberAccountSummary"
dotnet test tests/StageFright.UI.Tests/ --filter "FullyQualifiedName~ReportViewer"
dotnet test tests/StageFright.Integration.Tests/ --filter "FullyQualifiedName~AccountingReports"
```

Expected: all pass, including pre-existing tests for Income Statement, Trial Balance, Account Register, Member List, and Committee Report (unaffected by this feature).

## Manual validation (run the app)

```bash
dotnet run --project src/StageFright.App/
```

1. Navigate to **Reports → Member Account Summary**.
2. **User Story 1 (collapsed default view)**: Confirm the report loads showing one row per active member — name plus Current/30/60/90+ aging totals — with no transaction rows visible anywhere on the page.
3. **User Story 2 (expand/collapse)**: Click a member's row. Confirm it expands in place to show Opening Balance, that member's transactions, Closing Balance, and the Aging summary row — matching what the pre-redesign report showed for that member. Click it again and confirm it collapses. Expand a second member and confirm the first member's state is unaffected.
4. **User Story 3 (archived filter)**: Confirm the archived member does not appear by default. Open the filter panel, enable "Show Archived Members", click Apply, and confirm the archived member now appears labeled "(Archived)".
5. **User Story 4 (standard accounting order)**: Expand a member with multiple transactions and confirm they read oldest-to-newest, with Opening Balance first and Closing Balance last (before the Aging row) — unchanged from the report's behavior before this redesign.
6. **Export parity**: With some members expanded and others collapsed, click **Print / PDF** and **Export CSV**. Confirm both outputs contain full transaction detail for *every* in-scope member (not just the expanded ones) — open the CSV/PDF and compare against what full expansion shows on screen.
7. **Paging**: If there are more than 15 in-scope members, confirm the grid pages by whole members (15 member rows per page), and that expanding a member doesn't change the page's member count.

## Rollback check

Toggle the archived filter off/on and re-apply a couple of times; confirm no stale data lingers (regenerating always reflects the current filter state), consistent with the existing Regenerate/Refresh behavior in `ReportViewer`.
