# Phase 0 Research: Move Seed Data Checkbox to Organisation Settings

## Decision: Tab-unavailability mechanism

**Decision**: Bind BlazorBootstrap's native `Tab.Disabled` boolean parameter on the Chart of Accounts, Opening Balances, and Committee `<Tab>` elements to `_seedWithTestData`, and additionally guard `SetActiveTab(int index)` in code to no-op for those three indices whenever `_seedWithTestData` is true.

**Rationale**: Assembly inspection of the installed `Blazor.Bootstrap` 3.5.0 package (`BlazorBootstrap.dll`, reflected from `tests/StageFright.UI.Tests/bin/Debug/net10.0/`) confirms `BlazorBootstrap.Tab` already exposes a public `Disabled` boolean property — the library's own affordance for exactly this case, rendering the header visibly greyed-out/`aria-disabled` and Bootstrap's standard `pointer-events: none` behavior for a disabled nav-link. No existing usage of `Disabled` exists anywhere else in the codebase to copy, but using a library-native parameter matches CLAUDE.md's "prefer existing patterns... over custom" guidance far better than hand-rolled CSS or a wrapper `<div>` intercepting clicks. The `SetActiveTab` guard is a second, independent line of defense: FR-003 requires a bypassed tab header to **never** open its content, and a defense-in-depth guard at the single choke point every tab-activation path already funnels through (`Tab.OnClick` and `HandleNextAsync` both call `SetActiveTab`) makes that guarantee hold regardless of exactly how BlazorBootstrap's own click-suppression is implemented internally.

**Alternatives considered**:
- *CSS-only visual disabling with no functional guard* — rejected: FR-003 requires clicking a disabled header to do nothing, not merely look disabled; relying solely on the library's internal behavior (unverified beyond the `Disabled` property's existence) would be a functional guess, not a guarantee.
- *Removing the `<Tab>` elements from the DOM entirely when bypassed* — rejected: BlazorBootstrap's `Tabs`/`Tab` pairing indexes children positionally (`ShowTabByIndexAsync`, `_tabShown` list keyed by index); conditionally rendering `<Tab>` itself would shift every later tab's index and break `SetActiveTab`/`HandleNextAsync`'s existing index arithmetic for no behavioral gain over `Disabled`.

## Decision: Where the relocated checkbox lives

**Decision**: New component `Tabs/SampleDataTab.razor` + `SampleDataTab.razor.cs`, rendered inside the Organisation Settings `<Tab>`'s content block in `SetupWizard.razor`, alongside the existing `GeneralAppearanceTab`/`MembershipFeesTab`/`ThemeSelectionTab`/`SalesTaxTab` sub-section components. Its markup is copied unchanged from `ReviewTab.razor`'s current `@if (DebugSeederAvailable) { ... }` block; its three parameters (`DebugSeederAvailable`, `SeedWithTestData`, `SeedWithTestDataChanged`) mirror what `ReviewTab` already declares today.

**Rationale**: The Organisation Settings tab is already composed of several single-purpose sub-components rather than one large tab component — this is the established decomposition pattern in this exact tab (see `GeneralAppearanceTab.razor.cs`'s own doc comment: "organisation name... rendered lower in the Organisation Settings tab"). Adding a fifth sub-component keeps that pattern intact and keeps `SetupWizard.razor` from growing a large inline block. Reusing the exact existing markup (not restyling it) keeps the change to relocation + new effect, matching issue #313's ask and avoiding scope creep into control-style changes (see plan.md's Complexity Tracking).

**Alternatives considered**:
- *Inline the checkbox markup directly into `SetupWizard.razor`* — rejected: breaks the one-sub-component-per-concern pattern the other four Organisation Settings pieces already establish, and denies the new markup its own paired code-behind (CLAUDE.md's Blazor component rule expects logic in code-behind, not growing `SetupWizard.razor.cs` with a concern that isn't wizard orchestration).
- *Restyle as `<RadzenSwitch>` while relocating* — rejected: see plan.md Complexity Tracking; out of scope for issue #313, not a required consequence of relocating it.

## Decision: How Next skips the three bypassed tabs

**Decision**: `HandleNextAsync` advances `nextIndex` past any index for which a new `IsTabBypassed(int index)` helper (`_seedWithTestData && index is >= 1 and <= 3`) returns true, landing on index 4 (Review) directly from index 0 when sample data is selected — the same helper backs the `SetActiveTab` guard above.

**Rationale**: A single shared predicate keeps "which tabs are bypassed" defined in exactly one place, consumed by both the header-click guard and the Next-button skip, so the two can never drift out of sync. Because indices 1–3 are the only ones ever bypassed, incrementing past them lands correctly on 4 without needing a lookup table or configurable tab list — matching "simple over clever" for a fixed, small tab strip.

**Alternatives considered**:
- *Hard-code `nextIndex = 4` when leaving index 0 with sample data selected* — rejected: correct for today's exact tab count but silently wrong the moment a tab is inserted or reordered; the loop-based skip using the shared predicate stays correct under either the fixed-index or future-additional-tabs case with no extra cost today.

## Decision: Clearing queued data on check (FR-006)

**Decision**: A new `SetupWizard.razor.cs` method `HandleSeedWithTestDataChanged(bool value)` sets `_seedWithTestData = value` and, only when `value` is `true`, clears `_queuedAccounts`, `_queuedOpeningBalances`, and `_queuedCommitteeTitles`. This becomes the `SeedWithTestDataChanged` callback target passed to `SampleDataTab`, replacing the current inline lambda `@(v => _seedWithTestData = v)` that only `ReviewTab` needed.

**Rationale**: FR-006 requires discarding queued data only on the transition into sample-data mode, not on every checkbox toggle; gating the clear on `value == true` matches that exactly and avoids a redundant clear on uncheck (the queues are already empty at that point, since they can only be repopulated once the tabs are available again per FR-007). Because the coordinator can only reach the checkbox while on the Organisation Settings tab (tab 0), there is never a case where a bypassed tab is the *currently active* tab at the moment this fires — no extra navigation/reset logic is needed beyond clearing the three lists.

**Alternatives considered**:
- *Clear queues inside `SampleDataTab.razor.cs` itself* — rejected: the three queues are owned and defined in `SetupWizard.razor.cs` (not passed down to `SampleDataTab`); clearing them from the child would require passing three more callbacks down for no benefit over handling it where the state already lives, in the one method the child's `SeedWithTestDataChanged` already calls into.

## Verification: FR-008 (debug seeder already covers bypassed data)

**Decision**: No changes to `DebugDataSeeder.cs` are needed. FR-008 is satisfied by the seeder's existing behavior.

**Rationale**: Direct inspection of `src/StageFright.App/Seeding/DebugDataSeeder.cs` confirms it already:
- creates a chart of accounts beyond the seeded system accounts (a bank account plus three income accounts and eight expense accounts, via `IAccountService.CreateAsync`);
- posts a full opening-balance position across bank, petty cash, member receivable, accumulated surplus, and (when tax is applicable) tax accounts, via `IOpeningBalanceService.RecordOpeningBalancesAsync`;
- elects a full committee (President/Secretary/Treasurer plus six general committee members) across two seeded AGMs, via `IAgmService`.

This matches spec.md's Assumptions section, which flagged this as expected-but-unverified. `tasks.md` should still carry a verification task (re-run this check against the actual seeder output, not just its source) rather than silently dropping FR-008, per the spec's own instruction.

**Alternatives considered**: N/A — this is a verification of existing behavior, not a design choice between alternatives.
