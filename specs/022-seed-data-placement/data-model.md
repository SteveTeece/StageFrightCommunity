# Phase 1 Data Model: Move Seed Data Checkbox to Organisation Settings

This feature introduces no persisted entity, database table, or column. Its only "data" is a single in-session flag and the three existing queues it now interacts with — all already present in `SetupWizard.razor.cs` before this feature.

## Sample-data opt-in flag

| Field | Type | Owner | Notes |
|---|---|---|---|
| `_seedWithTestData` | `bool` | `SetupWizard.razor.cs` | Unchanged field — already exists today. Its *source* moves from `ReviewTab` to the new `SampleDataTab`; its *effect* expands from "gate the post-Finish seeding call" (unchanged) to also "gate whether the three tabs are enabled" (new). |

**State transitions**:

| From | To | Trigger | Side effects |
|---|---|---|---|
| `false` | `true` | Coordinator checks "Load sample data" on Organisation Settings | `_queuedAccounts`, `_queuedOpeningBalances`, `_queuedCommitteeTitles` are all cleared (FR-006); Chart of Accounts / Opening Balances / Committee `<Tab>`s become `Disabled`; `HandleNextAsync` from tab 0 now lands on tab 4 (Review). |
| `true` | `false` | Coordinator unchecks "Load sample data" | No queue mutation (they are already empty per the entry above); the three tabs become enabled again, each starting empty (FR-007) — "empty" falls out naturally from the prior clear, not a separate reset action. |

## Existing in-session queues (unchanged shape, newly reachable side effect)

These three fields already exist in `SetupWizard.razor.cs`; this feature adds exactly one new way they can be mutated (the clear-on-check transition above) and does not change their type, ownership, or how `HandleValidSubmitAsync` consumes them.

| Field | Type | Cleared by this feature when |
|---|---|---|
| `_queuedAccounts` | `List<QueuedAccountRequest>` | `_seedWithTestData` transitions `false → true` |
| `_queuedOpeningBalances` | `List<OpeningBalanceEntry>` | `_seedWithTestData` transitions `false → true` |
| `_queuedCommitteeTitles` | `List<string>` | `_seedWithTestData` transitions `false → true` |

## Validation rules

No new validation rules. `SetupFormModel`'s existing `IValidatableObject` cross-field validation and the wizard's existing FR-021 Finish gate ("a posted opening balance OR sample data selected") are unchanged — sample data selected still satisfies the balance side of that either/or, per FR-005.
