# Phase 0 Research: Committee Report Year Summary

The spec's own Clarifications session (2026-07-25) already resolved the two open ambiguities (duplicate same-role members; non-named position display). No `NEEDS CLARIFICATION` markers remain in the Technical Context. This document instead resolves the implementation-level decisions the spec deliberately leaves to the plan, and records the existing-infrastructure findings that shape the approach.

## Decision: Reuse the spec-005 master-detail `ReportData` extension instead of building new report infrastructure

**Rationale**: `ReportData.SummaryColumns` and `ReportSection.SummaryRow` were added in spec 005 (Member Account Summary Report Redesign) specifically to let a report collapse to one summary row per group, expandable to full detail — exactly the year-summary/expand-to-role-breakdown shape User Story 1 and 2 describe. `ReportViewer.razor`/`.razor.cs` already renders any report with non-empty `SummaryColumns` via `RadzenDataGrid`'s built-in master-detail row-expand (`UseMasterDetail` computed property, confirmed at `src/StageFright.UI/Shared/ReportViewer.razor.cs:30`). `PdfReportRenderer` and `CsvReportExporter` were both confirmed (by reading both files) to read only `Columns`/`Sections[].Rows`/`Subtotal`/`GrandTotal` — neither references `SummaryColumns`/`SummaryRow` — so they require zero changes and the master-detail contract rules from `specs/005-member-account-summary-redesign/contracts/report-master-detail-contract.md` apply unchanged here.

**Alternatives considered**:
- *Build a new grouping/expand mechanism specific to this report*: rejected — would duplicate `RadzenDataGrid` master-detail wiring already proven in production for Member Account Summary, violating the constitution's preference for consistency and simplicity (§3.1).
- *Render year grouping as `ReportSection.Heading` only, without a `SummaryRow`*: rejected — `Heading` alone renders in the existing flat-table path (no count, no collapse/expand), which does not satisfy FR-002 (year + position count shown before expansion).

## Decision: Detail rows carry their own Year value; don't rely on `ReportSection.Heading` for year identification in CSV

**Rationale**: `CsvReportExporter.Export` (confirmed by reading `src/StageFright.Reports/Rendering/CsvReportExporter.cs`) writes only `section.Rows` and `section.Subtotal` — it never writes `ReportSection.Heading`. If the year were only carried in `Heading`, CSV export would lose the year on every row, failing US3 Acceptance Scenario 2 ("each row identifies the year and the role or member it refers to") and FR-011. `PdfReportRenderer` does render `Heading` as a bold section label, so setting `Heading` to the year is still useful there, but it must not be relied upon as the sole source of the year in a row.

**Resolution**: every detail `ReportRow` for a year explicitly includes the year as its first cell. `ReportSection.Heading` is additionally set to the year (e.g., `"2026"`) purely to improve PDF section separation; it has no effect on CSV output and no effect on `SummaryRow` content.

**Alternatives considered**:
- *Rely on `Heading` for CSV year grouping*: rejected per the CSV renderer inspection above — this is not a hypothetical risk, it is a straightforward reading of the shipped `CsvReportExporter` code.

## Decision: Detail table columns are `[Year, Position, Member(s)]`; summary (master) columns are `[Year, Positions Recorded]`

**Rationale**: Mirrors the existing (pre-feature) `CommitteeReportProvider` column shape (`Member, Year, Position`) closely enough to stay familiar, while satisfying FR-003–FR-006a's requirement that each role/position appears as its own line with its member(s) — one row per position-line per year, rather than one row per member. `Positions Recorded` in the summary row is the literal count of committee membership records for that year under the active filter (FR-002 says "total number of committee positions recorded" — i.e., record count, not distinct-line count), so a year with "President: Alice, Bob" plus 3 general members counts as 5, matching what a secretary would get by counting raw committee records for that year.

**Alternatives considered**:
- *Summary count = number of distinct position lines shown (e.g., 4 lines: President/Secretary/Treasurer/General)*: rejected — doesn't match the FR-002 wording ("total number of committee positions recorded") and would undercount when a line has multiple members, which is precisely the scenario FR-010 calls out as important not to lose.

## Decision: Position-line ordering — President, Secretary, Treasurer (always present), then other distinct labels alphabetically, then "General Committee Members" last

**Rationale**: Directly follows FR-003–FR-006a's presentation order and User Story 2's narrative order (named roles, then other recorded positions, then unlabeled general members). Placing "General Committee Members" after the alphabetical other-position lines (rather than interleaving it alphabetically by its own label) keeps it visually anchored as the catch-all, consistent with FR-006a treating it as a single reserved line distinct from "every other distinct, non-blank position value" in FR-006.

**Alternatives considered**:
- *Sort "General Committee Members" alphabetically among the other position labels ("G" for "General...")*: rejected — FR-006 and FR-006a describe it as a separate, reserved category rather than one of the "other distinct position labels," so it should not compete alphabetically with real position titles.

## Decision: Case-insensitive/trimmed grouping key normalizes to lowercase-trimmed for comparison; the *displayed* label uses the trimmed text of the first record encountered for that key

**Rationale**: FR-007 mandates case-insensitive, whitespace-trimmed matching so `"president "`, `"President"`, and `"PRESIDENT"` collapse to one line, but the spec does not mandate a specific display casing when source data varies (e.g., `"welfare officer"` vs `"Welfare Officer"` in different records). Using the first-encountered trimmed value is deterministic (iteration is already ordered — members alphabetically, then their memberships), avoids inventing a title-casing transformation the spec never asked for, and is trivial to explain/test. For the three named roles, the canonical display strings `"President"`, `"Secretary"`, `"Treasurer"` are always used regardless of source casing, since FR-003–FR-005 name them explicitly as fixed labels.

**Alternatives considered**:
- *Always title-case the displayed label*: rejected — not requested by the spec, and would silently rewrite user-entered data (e.g., an intentionally lowercase position title) in the report output, which risks surprising a secretary who typed the position value themselves.

## Decision: Members within a multi-member line (named role, other position, or General Committee Members) are always listed alphabetically by name

**Rationale**: FR-006 and FR-006a both explicitly require alphabetical-by-name ordering for their respective line types. FR-010 (duplicate named-role members, e.g., "President: Alice, Bob") does not explicitly state ordering, but using the same alphabetical rule everywhere is the simplest consistent behavior and matches the FR-010 example ordering ("Alice, Bob") for free.

**Alternatives considered**:
- *Order named-role duplicates by record creation order*: rejected — introduces an extra ordering rule for one case only, adds nondeterminism risk in tests (creation timestamps), and isn't asked for by the spec or its example.

## Decision: Empty state relies on `RadzenDataGrid`'s built-in "no records" rendering; no new empty-state UI is built

**Rationale**: Switching `CommitteeReportProvider` to populate `SummaryColumns` moves it onto the same `RadzenDataGrid` master-detail rendering path already used by `MemberAccountSummaryReportProvider`, which has shipped in production with zero custom empty-state handling — an empty `Sections` list simply renders Radzen's default grid empty-state message. This satisfies FR-009/FR-012 ("the report shows the existing 'no data' empty state") without new code, and keeps `CommitteeReportProvider` consistent with its only other master-detail sibling.

**Alternatives considered**:
- *Add a custom "No committee records" message specific to this report*: rejected — not requested, inconsistent with the sibling report's behavior, and adds UI code the constitution's simplicity principle (§3.1) doesn't justify for a case the existing grid already handles.

## No new dependencies, no data/schema changes

`ICommitteeMembershipRepository` and `IMemberRepository` are used exactly as the current provider already uses them (`GetByMemberAsync`, `GetByStatusAsync`, `GetArchivedAsync`, `GetAllAsync`); no repository, entity, or migration changes are needed. No new NuGet packages are introduced.
