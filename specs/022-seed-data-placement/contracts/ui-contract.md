# Phase 1 UI Contract: Move Seed Data Checkbox to Organisation Settings

These are the component-parameter shapes tests and callers code against. No route, API, or CLI surface is introduced — this is a Blazor component parameter contract, the closest equivalent this feature has to an interface.

## `SampleDataTab` (new)

`src/StageFright.UI/Pages/Setup/Tabs/SampleDataTab.razor` + `.razor.cs`

| Parameter | Type | Required | Behavior |
|---|---|---|---|
| `DebugSeederAvailable` | `bool` | yes | When `false`, renders nothing (no checkbox, no markup at all) — release-build parity with today's `ReviewTab` behavior. |
| `SeedWithTestData` | `bool` | yes | Current checked state, rendered as the checkbox's `checked` attribute. |
| `SeedWithTestDataChanged` | `EventCallback<bool>` | yes | Invoked with the new checked state on every change; caller (`SetupWizard`) is responsible for queue-clearing side effects — this component only reports the raw toggle. |

Rendered element id is `#seedData` (preserved exactly — existing bUnit tests and any future ones locate the checkbox by this id).

## `ReviewTab` (changed)

`src/StageFright.UI/Pages/Setup/Tabs/ReviewTab.razor` + `.razor.cs`

| Parameter | Type | Required | Change |
|---|---|---|---|
| `Model` | `SetupFormModel` | yes | unchanged |
| `QueuedCommitteeTitles` | `IReadOnlyList<string>` | yes | unchanged |
| `QueuedAccounts` | `IReadOnlyList<QueuedAccountRequest>` | yes | unchanged |
| `DebugSeederAvailable` | `bool` | no | unchanged — still gates whether the read-only row appears |
| `SeedWithTestData` | `bool` | no | unchanged — now read-only display value only |
| `SeedWithTestDataChanged` | `EventCallback<bool>` | — | **removed** — Review no longer writes this value |

New read-only output: when `DebugSeederAvailable` is `true`, the existing `<dl>` summary gains a `<dt>Load sample data</dt><dd>Yes|No</dd>` row reflecting `SeedWithTestData`. No interactive `<input>` remains in `ReviewTab`'s markup.

## `SetupWizard` (changed — internal, not a reusable component, but its tab-strip behavior is directly test-visible)

`src/StageFright.UI/Pages/Setup/SetupWizard.razor` + `.razor.cs`

| Element / member | Contract |
|---|---|
| `<Tab Title="Chart of Accounts">`, `<Tab Title="Opening Balances">`, `<Tab Title="Committee">` | Each carries `Disabled="@_seedWithTestData"`. |
| `#btn-next` (unchanged id) | Clicking it while on Organisation Settings (tab 0) with `_seedWithTestData == true` shows the Review tab's content next, not Chart of Accounts'. |
| Tab headers for the three bypassed tabs | Clicking one while `_seedWithTestData == true` leaves `_currentTabIndex` and the visible tab content unchanged (no navigation occurs). |
| `HandleSeedWithTestDataChanged(bool)` (new, private) | The single mutation point for `_seedWithTestData`; on transition to `true`, clears `_queuedAccounts`, `_queuedOpeningBalances`, `_queuedCommitteeTitles`. |

Every id referenced above (`#seedData`, `#btn-next`) is copied verbatim from the current implementation — this feature does not rename any existing test-facing identifier.
