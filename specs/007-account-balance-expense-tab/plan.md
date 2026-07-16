# Implementation Plan: Chart of Accounts Balance Column & Record Expense Tab

**Branch**: `007-account-balance-expense-tab` | **Date**: 2026-07-16 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/007-account-balance-expense-tab/spec.md`

**Note**: This template is filled in by the `/speckit-plan` command. See `.specify/templates/plan-template.md` for the execution workflow.

## Summary

Two independent, additive changes to the Finance module: (1) add a computed, read-only **Balance** column to both the Active and Archived grids on the Chart of Accounts screen, sourced from the same `IGLRepository.GetAccountBalanceAsync` inception-to-date calculation already used by `BalanceSheetReportProvider`, via a new `AccountBalanceService`/`AccountBalance` view model mirroring the existing `MemberBalanceService`/`MemberBalance` pattern; and (2) move "Record Expense" from a standalone Finance-menu sub-item into a new tab on the Finance Overview screen (positioned after "Record Income", before "Apply Annual Fees") by embedding the existing `ExpensePaymentPage` component directly in a new tab, while its `/finance/expenses` route keeps working unchanged. No database schema changes, no new GL logic, no new plugin extension points.

## Technical Context

**Language/Version**: C# 14, .NET (MAUI Blazor Hybrid)

**Primary Dependencies**: EF Core (SQLite), Radzen.Blazor (`RadzenDataGrid`), Blazor.Bootstrap (`Tabs`/`Tab`), Serilog

**Storage**: SQLite (existing `StageFrightDbContext`) — no schema changes; feature reads existing `Account`/`Transaction` tables only

**Testing**: xUnit + NSubstitute (unit, `StageFright.Core.Tests`), bUnit (`StageFright.UI.Tests`), SQLite in-memory + EF migrations (`StageFright.Integration.Tests`)

**Target Platform**: Windows desktop and macOS desktop (MAUI)

**Project Type**: Desktop app (MAUI Blazor Hybrid) — single solution, layered projects (see Project Structure)

**Performance Goals**: No new performance target beyond parity with existing screens — Chart of Accounts balance computation intentionally reuses the same per-account query pattern already used by `BalanceSheetReportProvider`/`TrialBalanceReportProvider` (see research.md §1), so its load time scales the same way those reports already do today.

**Constraints**: Balance must always agree with Trial Balance/Balance Sheet figures for the same account (FR-004/SC-003); a single account's balance-calculation failure must not blank the rest of the grid (FR-012); no new database migration.

**Scale/Scope**: 2 UI-visible changes (Chart of Accounts grid columns ×2, Finance Overview tab ×1) + 1 new service/contract/view-model + 1 menu-provider edit; no new pages, no new routes, no new entities.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Gate | Status | Notes |
|---|---|---|
| Vertical Slice Module Architecture (§4.1) | ✅ PASS | New `AccountBalanceService`/`IAccountBalanceService`/`AccountBalance` live in the existing `StageFright.Core/Modules/Finance/` slice, alongside the sibling `MemberBalanceService` they mirror. No new module. |
| One Class Per File (§3.2.1 / §4.5) | ✅ PASS | `AccountBalance.cs`, `IAccountBalanceService.cs`, `AccountBalanceService.cs` each get their own file, matching the `MemberBalance`/`IMemberBalanceService`/`MemberBalanceService` precedent exactly. |
| Blazor Component Patterns (§4.7 — code-behind mandatory, no `@code` blocks) | ✅ PASS | `ChartOfAccountsPage.razor`/`.razor.cs` and `FinancePage.razor`/`.razor.cs` already follow this pattern and gain no new components (Record Expense reuses the existing `ExpensePaymentPage` component as-is; the Balance column is added to existing `.razor`/`.razor.cs` pairs). |
| CSS Isolation (§4.7.2) | ✅ PASS | No new component-scoped styles anticipated; a currency-formatted grid column and an inline error glyph both fit the existing global stylesheet / Bootstrap utility classes already used on this page. |
| Custom Exceptions at Boundaries (§5) | ✅ PASS | `AccountBalanceService` catches per-account calculation failures and logs via Serilog rather than letting a raw exception cross into the UI layer (FR-012); this is the intended per-row isolation behavior, not framework-exception leakage — no new custom exception type is needed since the failure is handled (not translated-and-rethrown). |
| Exhaustive Test Coverage (§11) | ✅ PASS (planned) | research.md §7 enumerates unit (`AccountBalanceServiceTests`), UI (`ChartOfAccountsPageTests`, new `FinancePageTests`), and menu-provider tests covering success, zero-balance, per-account failure, sign convention, tab order/state, and menu-removal paths — to be finalized in tasks.md. |
| Soft Delete / Financial Immutability (§3.4/§3.5/§3.6) | ✅ PASS | No new writes at all — this feature is entirely read/display (Balance column) and UI navigation (Record Expense tab). No entity, soft-delete field, or financial-record mutation is touched. |
| Data Grid Standards | ✅ PASS | Both grids keep `RadzenDataGrid` with `AllowSorting="true" AllowPaging="true" PageSize="15" class="rz-shadow-0"` unchanged; the new Balance column follows the existing `MemberBalanceList` precedent (`Property="Balance"`, `FormatString="{0:C}"`). |
| Plug-in Architecture (§8) | ✅ PASS (N/A) | No new extension point is introduced; `IAccountBalanceService` is an internal application service, not a plugin contract in `StageFright.Plugins.Contracts`. |

No violations — Complexity Tracking is not needed.

## Project Structure

### Documentation (this feature)

```text
specs/007-account-balance-expense-tab/
├── plan.md              # This file (/speckit-plan command output)
├── research.md          # Phase 0 output (/speckit-plan command)
├── data-model.md        # Phase 1 output (/speckit-plan command)
├── quickstart.md        # Phase 1 output (/speckit-plan command)
├── contracts/           # Phase 1 output (/speckit-plan command)
│   ├── account-balance-service-contract.md
│   └── finance-tab-navigation-contract.md
└── tasks.md             # Phase 2 output (/speckit-tasks command - NOT created by /speckit-plan)
```

### Source Code (repository root)

Existing MAUI Blazor Hybrid solution (`StageFrightCommunity.slnx`) with layered projects (see CLAUDE.md Architecture). This feature touches only the Finance vertical slice within `StageFright.Core` and its consuming pages in `StageFright.UI` — no new projects, no new modules.

```text
src/
├── StageFright.Core/
│   ├── Contracts/
│   │   └── IAccountBalanceService.cs          # NEW — sibling of IMemberBalanceService.cs
│   └── Modules/Finance/
│       ├── AccountBalance.cs                   # NEW — view model, sibling of MemberBalance.cs
│       ├── AccountBalanceService.cs            # NEW — sibling of MemberBalanceService.cs
│       └── FinanceMenuItemProvider.cs          # MODIFIED — remove "Record Expense" sub-item
│
├── StageFright.UI/
│   └── Pages/Finance/
│       ├── ChartOfAccountsPage.razor           # MODIFIED — Balance column, bind to AccountBalance
│       ├── ChartOfAccountsPage.razor.cs        # MODIFIED — load via IAccountBalanceService
│       ├── FinancePage.razor                   # MODIFIED — new "Record Expense" tab
│       ├── FinancePage.razor.cs                # MODIFIED — DefaultTabIndex mapping shift
│       └── ExpensePaymentPage.razor(.cs)        # UNCHANGED — embedded as-is in the new tab
│
└── StageFright.App/
    └── MauiProgram.cs                          # MODIFIED — register IAccountBalanceService

tests/
├── StageFright.Core.Tests/Modules/Finance/
│   └── AccountBalanceServiceTests.cs           # NEW
├── StageFright.UI.Tests/Pages/Finance/
│   ├── ChartOfAccountsPageTests.cs             # MODIFIED — Balance column assertions
│   └── FinancePageTests.cs                     # NEW — tab order/state, if not already covered
└── StageFright.Integration.Tests/Scenarios/
    └── (existing V6/V11/V14 scenarios re-verified for parity, no new scenario file required)
```

**Structure Decision**: Single-project MAUI Blazor Hybrid layout (per CLAUDE.md), Option "desktop-app". All new code lands inside the existing `StageFright.Core/Modules/Finance/` vertical slice and its paired `StageFright.UI/Pages/Finance/` pages — consistent with the repo's established module-per-folder convention and the sibling `MemberBalanceService`/`MemberBalanceList` pattern this feature mirrors. No new top-level directories, projects, or plugin extension points are introduced.

## Complexity Tracking

*No entries — Constitution Check above has no violations to justify.*
