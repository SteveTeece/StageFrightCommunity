# Phase 0 Research: Member Account Summary Report Redesign

No `NEEDS CLARIFICATION` markers remain in the Technical Context — all open questions were resolved during the brainstorming session that produced `spec.md` (see its Assumptions section). This document records the resulting technical decisions and the alternatives considered, for traceability.

## Decision 1: Extend the shared report pipeline vs. build a dedicated page

**Decision**: Add two new **optional** properties to the existing `ReportData`/`ReportSection` model (`ReportData.SummaryColumns`, `ReportSection.SummaryRow`) rather than building a standalone page/component for this one report.

**Rationale**: Keeps a single reporting pipeline (`IReportProvider` → `ReportData` → `ReportViewer` → PDF/CSV renderers) for all six reports. Because both new properties are nullable/empty by default, the other five reports (which never set them) render through the exact code path they use today — zero behavior change, zero regression risk for them.

**Alternatives considered**:
- *Dedicated page bypassing `ReportViewer`*: rejected — would duplicate the filter panel, Print/CSV export wiring, and cancel/error handling that `ReportViewer` already provides, and would fragment the "one pipeline, six reports" architecture documented in `CLAUDE.md`.
- *Force all six reports through a new master-detail shape*: rejected — the other five reports' sections are true flat data (no natural "collapsed" summary), so this would be a solution in search of a problem and risks regressing five working reports for a change only one report needs.

## Decision 2: RadzenDataGrid scope — master list only, not the expanded detail

**Decision**: Use `RadzenDataGrid` (with Radzen's built-in master-detail `<Template>` row-expand pattern) only for the top-level, one-row-per-member list. The expanded detail panel (opening balance, transactions, closing balance, aging — rows of differing shapes) keeps the existing hand-rolled Bootstrap table rendering already used by `ReportViewer` today.

**Rationale**: The master list has a stable, well-defined shape (name + aging figures) that maps cleanly onto `RadzenDataGrid` columns. The expanded detail mixes heterogeneous row shapes (a balance row, N transaction rows, an aging summary row) across a *dynamic*, per-report column set shared with the other five reports and with PDF/CSV export — exactly the situation `CLAUDE.md`'s existing data grid standards note as the reason `ReportViewer` is "the one exception" to the RadzenDataGrid rule. Forcing the detail into `RadzenDataGrid` as well would not improve it and would require a second, incompatible dynamic-column mechanism.

**Alternatives considered**:
- *Full RadzenDataGrid for both levels*: rejected per above — no clean typed-column mapping for the heterogeneous detail rows.
- *Custom expand/collapse chevron built by hand (no RadzenDataGrid at all)*: rejected — Radzen's master-detail pattern already provides exactly this interaction natively and consistently with how other grids in the app look and behave; reinventing it would be extra code for no benefit.

## Decision 3: Column sorting on the master grid

**Decision**: Disable native Radzen column-header sorting on the master list; keep the provider's existing alphabetical-by-member-name ordering.

**Rationale**: Master-row cells are pre-formatted display strings (e.g., `"Current: 12.00"`), not reflectable typed properties, so Radzen's built-in sort (which sorts by bound property) either wouldn't work meaningfully or would require brittle string-parsing sort comparators. Alphabetical-by-name is the existing, well-understood default and satisfies FR-008.

**Alternatives considered**:
- *Sort by a hidden numeric aging total*: rejected as unnecessary scope — not requested, and adds a second implicit sort key users didn't ask for.

## Decision 4: Transaction ordering within expanded detail

**Decision**: Keep/confirm standard chronological (oldest-first) ordering for transactions within an expanded member's detail — i.e., no change from the report's current behavior.

**Rationale**: The user's initial request asked for newest-first, but on reviewing the draft spec asked to switch to "standard accounting sort order," which is chronological ascending (oldest-first), matching how the report already orders transactions (`OrderBy(t => t.Date)`) and how ledgers/statements are conventionally read (running balance builds up from the opening balance to the closing balance).

**Alternatives considered**:
- *Newest-first (the original request)*: superseded by the user's explicit follow-up correction (see spec revision history / FR-006).

## Decision 5: Archived-members filter default

**Decision**: New `includeArchived` boolean filter, default **off**.

**Rationale**: Matches the "active by default, opt in to archived" convention already used elsewhere in the system (e.g., the Members grid's Active/Inactive toggle), reducing noise for the common case while keeping full visibility one click away.

**Alternatives considered**:
- *Default on (matching today's always-include behavior)*: rejected by the user in favor of the more conventional default.

## Decision 6: Export (Print/PDF, CSV) behavior

**Decision**: Exports always include full per-member transaction detail for every member in scope (per the archived-members filter), regardless of on-screen expand/collapse state.

**Rationale**: `PdfReportRenderer` and `CsvReportExporter` operate on `ReportData.Sections[].Rows` directly and are never made aware of the new `SummaryRow`/`SummaryColumns` fields — so exports are unaffected by this feature by construction, preserving today's export completeness guarantees without any renderer changes.

**Alternatives considered**:
- *Exports match on-screen collapse state*: rejected — produces a less complete financial document and was explicitly rejected by the user during brainstorming.
