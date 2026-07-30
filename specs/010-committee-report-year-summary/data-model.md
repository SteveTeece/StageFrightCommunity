# Phase 1 Data Model: Committee Report Year Summary

This feature touches no database entities, EF Core migrations, or repository contracts (per the spec's Key Entities note: "no new data is introduced"). The "model" here is the in-memory reporting shape produced by `CommitteeReportProvider.GenerateAsync` and consumed by the already-existing `StageFright.Reports`/`StageFright.UI` master-detail pipeline. All changes are confined to the provider's aggregation logic.

## Source entities (existing, unchanged)

### `CommitteeMembership` (`src/StageFright.Core/Entities/CommitteeMembership.cs`)

| Field | Type | Relevance to this feature |
|---|---|---|
| `MemberId` | `Guid` | Joins to `Member` for name and status. |
| `Year` | `int` | Primary grouping key (FR-001). |
| `Position` | `string` | Free-text role/title. Matched case-insensitively and trimmed (FR-007) against `"president"`, `"secretary"`, `"treasurer"`; blank/whitespace-only → "General Committee Members" (FR-006a); anything else → its own distinct position line (FR-006). |
| `IsDeleted` | `bool` | Already filtered out by `ICommitteeMembershipRepository.GetByMemberAsync`, which returns non-deleted records only — no new filtering logic needed. |

### `Member` (existing, unchanged)

| Field | Relevance |
|---|---|
| `Id`, `Name` | Used to resolve the display name for each committee record; `Name` also used for alphabetical member-within-line ordering. |
| Status / soft-delete state | Already governs which members are fetched via the existing `memberFilter` filter (Active Only / Archived Only / All) — unchanged (FR-008). |

## In-memory aggregation shape (new — internal to `CommitteeReportProvider`, not a persisted type)

For each year present in the filtered data, the provider builds:

```csharp
private sealed record YearGroup(
    int Year,
    int RecordCount,                          // FR-002: total committee records for the year
    IReadOnlyList<PositionLine> PositionLines  // ordered: President, Secretary, Treasurer,
);                                             //   then other labels (alpha), then General Committee Members

private sealed record PositionLine(string Label, IReadOnlyList<string> MemberNames);
```

This is a private implementation detail of the provider (constitution §3.2.1's one-class-per-file rule applies to public/file-scoped types; a `private sealed record` nested for a single method's use does not warrant its own file, consistent with how other providers keep small private helper types inline — see `ReportViewer.razor.cs`'s `PagedSection` record for the established precedent). It exists only to make `GenerateAsync` readable; it is not returned from the method.

## `ReportData` (existing — `src/StageFright.Reports/Models/ReportData.cs`) — no schema change

| Field | Value this provider sets |
|---|---|
| `Title` | `"Committee Report"` (unchanged) |
| `SubTitle` | `"Filter: {memberFilter} — {generated date}"` (unchanged pattern) |
| `Columns` | `[Year, Position, Member(s)]` — the **detail** table columns, used for the expand panel, PDF, and CSV. |
| `SummaryColumns` | **New use of the existing (spec-005) field**: `[Year, Positions Recorded]` — the collapsed master-row columns. |
| `Sections` | One `ReportSection` per year with records, ordered most-recent-year-first (FR-001). |
| `GrandTotal` | Not set (`null`) — no cross-year total is required by any FR or success criterion. |

## `ReportSection` (existing — `src/StageFright.Reports/Models/ReportSection.cs`) — no schema change

For each year:

| Field | Value |
|---|---|
| `Heading` | The year as a string (e.g., `"2026"`) — improves PDF section separation only; **not** relied on for CSV (see `research.md`: CSV export never reads `Heading`). |
| `Rows` | One `ReportRow` per position line for that year: `Cells = [year.ToString(), label, string.Join(", ", memberNames)]`. `label` is `"President"`/`"Secretary"`/`"Treasurer"` (with `"Vacant"` as the member-name text when unfilled — FR-003–FR-005), the first-encountered trimmed original-casing text for other distinct positions (FR-006/FR-007), or `"General Committee Members"` for blank positions (FR-006a). Ordered per `research.md`. |
| `Subtotal` | Not set (`null`) — no numeric column exists to subtotal. |
| `SummaryRow` | **New use of the existing (spec-005) field**: `Cells = [year.ToString(), recordCount.ToString()]` — the collapsed one-line year view. |

**Validation rule** (unchanged from spec 005's contract): `ReportData.SummaryColumns` is non-empty here, so every `ReportSection` in `Sections` must set `SummaryRow` — enforced by construction in `CommitteeReportProvider` (every year section is built through the same code path that always sets both `Rows` and `SummaryRow` together), not by a runtime guard, consistent with `MemberAccountSummaryReportProvider`'s existing pattern.

## `CommitteeReportProvider` (existing — `src/StageFright.Reports/Providers/CommitteeReportProvider.cs`)

No change to `Filters` (`memberFilter`: Active Only / Archived Only / All, default "Active Only" — FR-008 unchanged).

`GenerateAsync` change:
1. Fetch filtered members exactly as today (`GetByStatusAsync`/`GetArchivedAsync`/`GetAllAsync` per `memberFilter`).
2. For each filtered member (ordered by name, as today), fetch their committee memberships via the existing `GetByMemberAsync` and flatten into a single list of `(Member, CommitteeMembership)` pairs — same repository calls as today, just accumulated instead of emitted per-member.
3. Group the flattened list by `Year`, descending (FR-001). A year with zero matching records under the filter is simply absent from this grouping — never emitted (FR-009).
4. For each year group, build the `PositionLine` list per the ordering/matching/display rules in `research.md`, and compute `RecordCount` as the raw count of `(Member, CommitteeMembership)` pairs in that year (FR-002/FR-010).
5. Map each year group to one `ReportSection` (`Heading`, `Rows`, `SummaryRow`) as described above.
6. Return `ReportData` with `Columns`, `SummaryColumns`, and the ordered `Sections`.

No changes to `ICommitteeMembershipRepository`, `IMemberRepository`, or any entity/repository contract.

## `ReportViewer` (existing — `src/StageFright.UI/Shared/ReportViewer.razor` / `.razor.cs`) — no change

`UseMasterDetail` (`_report?.SummaryColumns?.Count > 0`) automatically becomes `true` once `CommitteeReportProvider` populates `SummaryColumns`, routing the Committee Report onto the same `RadzenDataGrid` master-detail rendering already used by `MemberAccountSummaryReportProvider` — no code changes needed in this component for this feature.

No changes to `PdfReportRenderer` or `CsvReportExporter` — both continue reading `ReportData.Columns`/`Sections[].Rows`/`Subtotal`/`GrandTotal` exactly as today, oblivious to `SummaryColumns`/`SummaryRow`, and correctly render/export the year value because it is present in every detail row's `Cells[0]`, not only in `Heading`.
