# Implementation Plan: Print Chart of Accounts

**Branch**: `020-print-chart-of-accounts` | **Date**: 2026-08-23 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/020-print-chart-of-accounts/spec.md`

## Summary

Add a "Print Chart of Accounts" button and an off-by-default "include current balances" `RadzenSwitch` to the existing Chart of Accounts screen, backed by one new `ChartOfAccountsReportProvider : IReportProvider` that groups active accounts into the fixed Assets/Liabilities/Equity/Income/Expenses sections (ordered by account number) and, when the filter is on, adds a balance column sourced from the same `IAccountBalanceService` the screen already uses — guaranteeing the printed figure matches the screen by construction, including its existing per-account error isolation. The screen's own Print button resolves that provider through `IReportProviderRegistry` and renders straight to a temp PDF via the existing `IPdfReportRenderer` + `Process.Start` hand-off (matching `RehearsalList`'s attendance-roll print button, not the `ReportViewer` preview modal), while registering the provider in DI is all Story 3 needs to also surface it in the central Reports menu with CSV export, for free. No new entity, migration, or persisted field is introduced.

## Project Structure

```text
src/StageFright.Reports/Providers/
└── ChartOfAccountsReportProvider.cs        # NEW — IReportProvider; groups AccountBalance rows by type/number

src/StageFright.App/
└── MauiProgram.cs                          # EDIT — one DI line: AddScoped<IReportProvider, ChartOfAccountsReportProvider>

src/StageFright.UI/Pages/Finance/
├── ChartOfAccountsPage.razor               # EDIT — "Print Chart of Accounts" button + include-balances RadzenSwitch
└── ChartOfAccountsPage.razor.cs            # EDIT — print handler: registry lookup → GenerateAsync → PdfRenderer → temp file → Process.Start

tests/StageFright.Reports.Tests/
└── ChartOfAccountsReportProviderTests.cs   # NEW — section grouping/ordering, includeBalances on/off, HasError row, archived exclusion

tests/StageFright.UI.Tests/Pages/Finance/
└── ChartOfAccountsPageTests.cs             # EDIT — button/switch render, print handler invocation
```

**Structure Decision**: Everything routes through the existing `IReportProvider` pipeline (`StageFright.Reports`), registered exactly like every other Finance report; the screen change is additive to the already-paired `ChartOfAccountsPage.razor`/`.razor.cs`. No new project, module, or persistence layer is touched.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design — still passes, no changes below.*

| Principle | Assessment |
|---|---|
| One class per file | PASS — `ChartOfAccountsReportProvider` is the only new type, in its own file. |
| Simple over clever | PASS — reuses `IAccountBalanceService` verbatim rather than re-deriving GL sums; no new abstraction layer. |
| Blazor code-behind mandatory | PASS — editing the existing paired `.razor`/`.razor.cs`; no `@code` block added. |
| No custom JavaScript | PASS — `RadzenSwitch` + Bootstrap button only. |
| Custom exceptions at every boundary | PASS — no new boundary is crossed; `IAccountBalanceService` already isolates per-account failures internally (never throws for a balance error), and the page's print handler follows `ReportViewer.PrintReport()`'s established generic-catch-and-friendly-message pattern for the PDF render/launch step. |
| Exhaustive code-path test coverage | PASS (commitment) — provider test covers grouping/ordering/both filter states/HasError/archived-exclusion; page test covers the new button and switch. Enforced at `/speckit-tasks` + `/speckit-implement`. |
| Soft-delete everywhere except finance | N/A — no new or modified entity. |
| Data grid standards | N/A — `ChartOfAccountsPage`'s existing `RadzenDataGrid` usage is untouched. |
| Toggle control standards | PASS — include-balances option uses `RadzenSwitch`, matching the Members List "show inactive" reference usage. |
| Reports pipeline (single pipeline, provider-declared filters, generation isolation) | PASS — implemented purely as one more `IReportProvider`; no bespoke rendering path. |
| Finance / GL integrity | N/A — read-only report; no fee/payment/GL write. |

No violations — Complexity Tracking is omitted.
