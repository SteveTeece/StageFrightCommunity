# Implementation Plan: Bank Deposit Recording

**Branch**: `009-bank-deposit-transfers` | **Date**: 2026-07-23 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/009-bank-deposit-transfers/spec.md`

**Note**: This template is filled in by the `/speckit-plan` command. See `.specify/templates/plan-template.md` for the execution workflow.

## Summary

Retire the generic "Transfer between any two accounts" workflow (issue #237) and replace it with a bank-deposit-specific workflow: source is always fixed to the system Cash on Hand account, the user only picks the destination bank account. The existing `AccountTransferService`/`IAccountTransferService`/`RecordTransferRequest`/`TransferPage` are replaced by `BankDepositService`/`IBankDepositService`/`RecordBankDepositRequest`/`BankDepositPage`, which post the same balanced GL pair (Debit destination bank account / Credit Cash on Hand) but under a new, distinct `JournalEntryType.BankDeposit` classification (FR-011) instead of the historical `JournalEntryType.Transfer`. Historical `Transfer`-typed journal entries and their GL rows are untouched and continue to render correctly in reports (FR-009) because reports read `Transaction`/`Account` data directly and never key off `JournalEntryType`. The general-purpose "any two accounts" capability remains available, unaffected, via the existing Journal Entry page (`GeneralJournalService`/`JournalEntryPage`). No database schema change is required — `JournalEntry.Type` is already persisted as a string column, so adding an enum member needs no EF migration.

## Technical Context

**Language/Version**: C# 14, .NET 10 (MAUI Blazor Hybrid)

**Primary Dependencies**: EF Core (SQLite), Radzen.Blazor, Blazor.Bootstrap, Serilog; xUnit + NSubstitute (unit), bUnit (UI), SQLite in-memory + EF migrations (integration)

**Storage**: SQLite (existing `StageFrightDbContext`) — no schema changes; `JournalEntry.Type` is already configured with `HasConversion<string>()` (`JournalEntryConfiguration.cs`), so a new enum member is a pure data-level addition

**Testing**: xUnit + NSubstitute (`StageFright.Core.Tests`), bUnit (`StageFright.UI.Tests`), SQLite in-memory + EF migrations (`StageFright.Integration.Tests`)

**Target Platform**: Windows desktop and macOS desktop (MAUI)

**Project Type**: Desktop app (MAUI Blazor Hybrid) — single solution, layered projects (see Project Structure)

**Performance Goals**: No new performance target — same single balanced-GL-pair-under-one-transaction pattern already used by `AccountTransferService`/`IncomeEntryService`/`ExpensePaymentService` (SC-001's "under 30 seconds" is a UX/workflow goal, not a throughput target)

**Constraints**: Books must remain balanced (Σdebits = Σcredits) via the existing `IGLRepository.AddBalancedSetAsync` + `IUnitOfWork.ExecuteInTransactionAsync` pattern (FR-006/SC-002); historical `Transfer`-typed entries must remain visible and unchanged in reports after the refactor (FR-009); no minimum/maximum amount or approval workflow beyond existing positive-amount validation (per spec Assumptions)

**Scale/Scope**: 1 page replaced (`TransferPage` → `BankDepositPage`), 1 service/contract/request model replaced (`AccountTransferService` → `BankDepositService`), 1 new enum member (`JournalEntryType.BankDeposit`), 1 menu-provider edit, 1 DI registration edit, 1 debug-seeder call-site edit; no new projects, no new database migration

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Gate | Status | Notes |
|---|---|---|
| Vertical Slice Module Architecture (§4.1) | ✅ PASS | `BankDepositService`/`IBankDepositService`/`RecordBankDepositRequest` live in the existing `StageFright.Core/Modules/Finance/` slice, directly replacing the sibling files they retire. No new module. |
| One Class Per File (§3.2.1 / §4.5) | ✅ PASS | `RecordBankDepositRequest.cs`, `IBankDepositService.cs`, `BankDepositService.cs`, `BankDepositModel.cs` each get their own file, matching the retired `RecordTransferRequest`/`IAccountTransferService`/`AccountTransferService`/`TransferModel` 1:1. |
| Blazor Component Patterns (§4.7 — code-behind mandatory, no `@code` blocks) | ✅ PASS | `BankDepositPage.razor`/`.razor.cs` follows the same paired structure as the retired `TransferPage.razor`/`.razor.cs`; no inline `@code` blocks. |
| CSS Isolation (§4.7.2) | ✅ PASS | No new component-scoped styles — the form reuses the same Bootstrap utility classes (`card`, `row g-2`, `input-group`) already used by `TransferPage.razor`; no `.razor.css` needed. |
| Custom Exceptions at Boundaries (§5) | ✅ PASS | `BankDepositService` reuses the existing `ValidationException`/`EntityNotFoundException` pattern from `AccountTransferService`/`IncomeEntryService` for bad amount, missing account, non-bank destination, and destination = Cash on Hand. |
| GL Integrity (Finance/GL rules) | ✅ PASS | Reuses `IGLRepository.AddBalancedSetAsync` + `IUnitOfWork.ExecuteInTransactionAsync` — the same atomic balanced-pair pattern as every other Finance posting service; `GLBalanceException`/rollback behavior is inherited, not reimplemented. |
| Financial Record Immutability (§3.4/§3.5/§3.6) | ✅ PASS | `Transaction`/`JournalEntry` remain exempt from soft-delete and are never edited/deleted; bank deposits are new immutable rows like every other GL posting. Historical `Transfer`-typed rows are never touched (FR-009). |
| Exhaustive Test Coverage (§11) | ✅ PASS (planned) | research.md §5 enumerates unit (`BankDepositServiceTests`), integration (`V14_ExpensesTransfersTests` bank-deposit section), UI (new `BankDepositPageTests`), and menu-provider test updates covering success, validation failures, zero-eligible-destination, audit logging, and default-description paths — to be finalized in tasks.md. Note: `TransferPage` had no pre-existing bUnit coverage; `BankDepositPage` gets full coverage as new/changed functionality per constitution §11.3. |
| Plug-in Architecture (§8) | ✅ PASS (N/A) | No new extension point — `IBankDepositService` is an internal application service, not a `StageFright.Plugins.Contracts` interface. |

No violations — Complexity Tracking is not needed.

## Project Structure

### Documentation (this feature)

```text
specs/009-bank-deposit-transfers/
├── plan.md              # This file (/speckit-plan command output)
├── research.md          # Phase 0 output (/speckit-plan command)
├── data-model.md        # Phase 1 output (/speckit-plan command)
├── quickstart.md        # Phase 1 output (/speckit-plan command)
├── contracts/           # Phase 1 output (/speckit-plan command)
│   └── bank-deposit-service-contract.md
└── tasks.md             # Phase 2 output (/speckit-tasks command - NOT created by /speckit-plan)
```

### Source Code (repository root)

Existing MAUI Blazor Hybrid solution (`StageFrightCommunity.slnx`) with layered projects (see CLAUDE.md Architecture). This feature touches only the Finance vertical slice within `StageFright.Core` and its consuming pages in `StageFright.UI`, plus one DI registration and one debug-seeder call-site in `StageFright.App` — no new projects, no new modules.

```text
src/
├── StageFright.Core/
│   ├── Enums/
│   │   └── JournalEntryType.cs                 # MODIFIED — add BankDeposit member
│   ├── Contracts/
│   │   ├── IBankDepositService.cs              # NEW — replaces IAccountTransferService.cs
│   │   └── IAccountTransferService.cs          # REMOVED
│   └── Modules/Finance/
│       ├── BankDepositService.cs               # NEW — replaces AccountTransferService.cs
│       ├── AccountTransferService.cs           # REMOVED
│       ├── RecordBankDepositRequest.cs         # NEW — replaces RecordTransferRequest.cs
│       ├── RecordTransferRequest.cs            # REMOVED
│       └── FinanceMenuItemProvider.cs          # MODIFIED — "Transfers" → "Record Bank Deposit"
│
├── StageFright.UI/
│   └── Pages/Finance/
│       ├── BankDepositPage.razor               # NEW — replaces TransferPage.razor
│       ├── BankDepositPage.razor.cs            # NEW — replaces TransferPage.razor.cs
│       ├── BankDepositModel.cs                 # NEW — replaces TransferModel.cs
│       ├── TransferPage.razor                  # REMOVED
│       ├── TransferPage.razor.cs               # REMOVED
│       └── TransferModel.cs                    # REMOVED
│
└── StageFright.App/
    ├── MauiProgram.cs                          # MODIFIED — register IBankDepositService
    └── Seeding/DebugDataSeeder.cs              # MODIFIED — call BankDepositService instead

tests/
├── StageFright.Core.Tests/Modules/Finance/
│   ├── BankDepositServiceTests.cs              # NEW — replaces AccountTransferServiceTests.cs
│   ├── AccountTransferServiceTests.cs          # REMOVED
│   └── FinanceMenuItemProviderTests.cs         # MODIFIED — "Transfers" → "Record Bank Deposit" InlineData
├── StageFright.UI.Tests/Pages/Finance/
│   └── BankDepositPageTests.cs                 # NEW (no prior TransferPage bUnit coverage existed)
└── StageFright.Integration.Tests/Scenarios/
    └── V14_ExpensesTransfersTests.cs           # MODIFIED — "Transfers" section → bank-deposit assertions
```

**Structure Decision**: Single-project MAUI Blazor Hybrid layout (per CLAUDE.md), desktop-app option. All changes land inside the existing `StageFright.Core/Modules/Finance/` vertical slice and its paired `StageFright.UI/Pages/Finance/` pages, following the repo's established module-per-folder convention and the sibling `IncomeEntryService`/`RecordIncome` pattern (fixed default account + picker for the other side) this feature mirrors. No new top-level directories, projects, or plugin extension points are introduced.

## Complexity Tracking

*No entries — Constitution Check above has no violations to justify.*
