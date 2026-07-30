# Implementation Plan: Outstanding Fee Selection on the Record Member Payment Form

**Branch**: `008-outstanding-fee-payment-allocation` | **Date**: 2026-07-17 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/008-outstanding-fee-payment-allocation/spec.md`

## Summary

Add an outstanding-fees selection grid to the `PaymentForm` component (the "Record Member Payment" screen, reached from Finance → Outstanding). Each row shows a fee's type, fee date, due date, and *true remaining amount owed* (accounting for prior partial payments), with a leftmost checkbox column and a select-all header checkbox following the existing `AttendanceGrid`/`ParticipationGrid` convention. Checking/unchecking fees recalculates the Amount field to the sum of checked fees' remaining amounts; Amount stays editable downward for partial payments. `IPaymentService.RecordAsync` gains an optional `SelectedFeeIds` list on `RecordPaymentRequest`: when supplied (non-null), the payment is allocated to exactly those fees, oldest-first, and both the UI and the service independently reject an amount that exceeds the selected total or an explicitly-empty selection with a positive amount. When `SelectedFeeIds` is `null` (the default, used by every existing caller), behavior is byte-for-byte unchanged — FIFO allocation across the member's full unpaid fee history.

The one real design gap uncovered during research: no existing service method returns a fee's *true remaining amount owed* (original amount minus prior GL settlements) — `MemberBalanceService.SelectOutstandingFees` returns full-history fees at their original `Amount`, and the only place true remaining-owed is computed today is inline inside `PaymentService.RecordAsync`'s FIFO loop. This plan introduces one new read method, `IMemberBalanceService.GetOutstandingFeesAsync`, that surfaces this computation as a reusable DTO (`OutstandingFee`) for both the new grid and any future caller, rather than duplicating the formula a third time.

## Technical Context

**Language/Version**: C# 14 / .NET (MAUI target), matches rest of solution

**Primary Dependencies**: .NET MAUI Blazor Hybrid, `Radzen.Blazor` (`RadzenDataGrid` for the new grid), EF Core (SQLite), Serilog — all existing, no new packages

**Storage**: SQLite via `StageFrightDbContext` (existing `Fee`, `Payment`, `Transaction` tables) — no schema changes; this feature is a read-projection + allocation-logic change only

**Testing**: xUnit + NSubstitute (`StageFright.Core.Tests`), bUnit (`StageFright.UI.Tests`), SQLite-backed integration tests (`StageFright.Integration.Tests`, `StageFright.Data.Tests`)

**Target Platform**: Windows desktop / macOS desktop (MAUI Blazor Hybrid host)

**Project Type**: desktop-app (single MAUI Blazor Hybrid solution; existing vertical-slice module layout, no new project)

**Performance Goals**: No explicit targets in spec; grid checkbox interactions must recalculate Amount synchronously with no perceptible lag (in-memory sum over an already-loaded fee list, no re-query per click)

**Constraints**: Must preserve GL debit=credit balancing (existing `GLBalanceException` guard) and the single-ACID-transaction-per-payment invariant; must not alter behavior for any existing caller of `RecordAsync` that doesn't supply `SelectedFeeIds`; fee/payment/transaction records remain immutable (no new mutable fields on those entities)

**Scale/Scope**: One new DTO, one new service method + interface member, one amended request DTO field, one amended service method (filter step in existing FIFO loop), one new paired Blazor grid component, amendments to `PaymentForm`/`PaymentFormModel` — a contained, single-module change

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Assessment |
|---|---|
| §4.1 Vertical Slice Module Architecture | PASS — all changes are within the existing `Finance` module (`StageFright.Core/Modules/Finance/`, `StageFright.UI/Pages/Finance/`); no cross-module coupling introduced. |
| §3.2.1 / §4.5 One Class Per File | PASS — new `OutstandingFee` DTO gets its own file; new grid component gets its own paired `.razor`/`.razor.cs` files; no multi-type files planned. |
| §4.7 Blazor Component Patterns | PASS — new grid component follows mandatory code-behind pattern (no `@code` block); CSS isolation only added if the grid needs styles the global stylesheet can't cover (not expected — reuses `AttendanceGrid`'s existing classes). |
| Data grid standards (CLAUDE.md) | PASS — new grid uses `RadzenDataGrid` with `AllowSorting="true" AllowPaging="true" PageSize="15" class="rz-shadow-0"` and a `HeaderTemplate` select-all checkbox, matching `AttendanceGrid`/`ParticipationGrid`. |
| §5 Custom Exceptions at Boundaries | PASS — new validation (amount exceeds selection, empty explicit selection) reuses the existing `ValidationException` type and constructor shape already used by `RecordAsync`; no raw framework exceptions cross boundaries. |
| §3.4/§3.5/§3.6 Soft Delete & Financial Immutability | PASS — no new entities; `Fee`/`Payment`/`Transaction` remain untouched and immutable. Allocation still only ever *creates* new GL transaction rows, never edits/deletes existing ones. |
| Finance / GL integrity (CLAUDE.md) | PASS — the amended `RecordAsync` still wraps fee allocation + GL pair creation + balance assertion in the existing single `DbContext` transaction; the only change is which subset of fees the existing FIFO loop iterates over. |
| §11 Testing Standards | PASS (planned) — unit tests (`PaymentServiceTests`, `MemberBalanceServiceTests`), bUnit tests (`PaymentFormTests`, new grid component tests), and an integration/acceptance test (`V5_PaymentsTests.cs`) are all identified in Phase 1 design; see tasks.md (Phase 2) for the exhaustive path list. |

No violations identified. Complexity Tracking table is not needed.

**Post-Phase 1 re-check**: Design artifacts (`data-model.md`, `contracts/finance-payment-contracts.md`) confirmed the plan stayed within the original assessment — one new DTO (`OutstandingFee`, its own file), one new UI-only row view-model (`OutstandingFeeRow`, scoped to its component's code-behind per the nested-type exception), one additive interface member (`IMemberBalanceService.GetOutstandingFeesAsync`) and one additive request property (`RecordPaymentRequest.SelectedFeeIds`) — no interface signature breaks, no new entities, no new exception types (reuses `ValidationException`), no cross-module dependency introduced. PASS, unchanged from the pre-design gate.

## Project Structure

### Documentation (this feature)

```text
specs/008-outstanding-fee-payment-allocation/
├── plan.md              # This file (/speckit-plan command output)
├── research.md          # Phase 0 output (/speckit-plan command)
├── data-model.md        # Phase 1 output (/speckit-plan command)
├── quickstart.md        # Phase 1 output (/speckit-plan command)
├── contracts/           # Phase 1 output (/speckit-plan command)
└── tasks.md             # Phase 2 output (/speckit-tasks command - NOT created by /speckit-plan)
```

### Source Code (repository root)

```text
src/StageFright.Core/
├── Modules/Finance/
│   ├── PaymentService.cs              # MODIFIED — filter FIFO loop to SelectedFeeIds when supplied
│   ├── RecordPaymentRequest.cs        # MODIFIED — add SelectedFeeIds property
│   ├── MemberBalanceService.cs        # MODIFIED — implement new GetOutstandingFeesAsync
│   └── OutstandingFee.cs              # NEW — read-model DTO (FeeId, FeeType, FeeDate, DueDate, RemainingAmount)
└── Contracts/
    └── IMemberBalanceService.cs       # MODIFIED — add GetOutstandingFeesAsync member

src/StageFright.UI/Pages/Finance/
├── PaymentForm.razor                  # MODIFIED — embed new grid component, wire Amount auto-calc
├── PaymentForm.razor.cs               # MODIFIED — load outstanding fees, validation, request wiring
├── PaymentFormModel.cs                # MODIFIED — add SelectedFeeIds/selection state if needed
├── OutstandingFeeSelectionGrid.razor      # NEW — RadzenDataGrid + checkbox column (paired component)
└── OutstandingFeeSelectionGrid.razor.cs   # NEW — selection state, select-all, Amount-changed callback

tests/StageFright.Core.Tests/Modules/Finance/
├── PaymentServiceTests.cs             # MODIFIED — selected-fee allocation, validation cases
└── MemberBalanceServiceTests.cs       # MODIFIED — GetOutstandingFeesAsync cases

tests/StageFright.UI.Tests/Pages/Finance/
├── PaymentFormTests.cs                # MODIFIED — grid presence, Amount auto-calc, validation
└── OutstandingFeeSelectionGridTests.cs # NEW — select-all, per-row checkbox, empty state

tests/StageFright.Integration.Tests/Scenarios/
└── V5_PaymentsTests.cs                # MODIFIED — end-to-end selected-fee allocation scenarios
```

**Structure Decision**: This is a contained enhancement inside the existing desktop-app solution layout (no new projects). All production changes live in the `Finance` module slice per §4.1 (`StageFright.Core/Modules/Finance/` for service/DTO/request changes, `StageFright.UI/Pages/Finance/` for the form and new grid component); repositories are untouched since no new persistence is needed — the new `GetOutstandingFeesAsync` method composes existing `IFeeRepository`/`IGLRepository` calls, matching the "repositories live centrally, are not module-owned" deviation already documented in CLAUDE.md.

## Complexity Tracking

*No entries — Constitution Check reported no violations.*
