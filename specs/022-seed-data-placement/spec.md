# Feature Specification: Move Seed Data Checkbox to Organisation Settings

**Feature Branch**: `022-seed-data-placement`
**Created**: 2026-08-25
**Status**: Draft
**Source**: GitHub issue [#313](https://github.com/SteveTeece/StageFrightCommunity/issues/313) — "Move Seed Data checkbox"

## User Scenarios & Testing

### User Story 1 - Decide on sample data from the first tab (Priority: P1)

A coordinator setting up StageFright Community for the first time sees the "Load sample data" option on the Organisation Settings tab — the very first tab of the setup wizard — instead of discovering it only after filling in every other tab on the Review tab at the end.

**Why this priority**: This is the core ask of the issue — relocating the checkbox so the decision is made up front, before the coordinator invests time in tabs that only matter if they choose *not* to use sample data. Every other part of this feature (bypassing the other tabs) depends on the coordinator being able to make this choice early.

**Independent Test**: Launch the app with no Settings record present (debug build), confirm "Load sample data" appears on the Organisation Settings tab and no longer appears on the Review tab, and confirm the Review tab still shows — read-only — whether sample data will be loaded.

**Acceptance Scenarios**:

1. **Given** the wizard is opened for the first time in a debug build, **When** the Organisation Settings tab is shown, **Then** a "Load sample data" checkbox is visible there, and the Review tab no longer renders it as an interactive control.
2. **Given** a debug data seeder is not registered (a release build), **When** the Organisation Settings tab is shown, **Then** no sample-data option appears anywhere in the wizard, exactly as today.
3. **Given** the coordinator checks "Load sample data" on the Organisation Settings tab, **When** they later open the Review tab, **Then** the Review tab shows, read-only, that sample data will be loaded.
4. **Given** the coordinator has not touched the "Load sample data" checkbox, **When** they view any other tab, **Then** that tab behaves exactly as it does today.

---

### User Story 2 - Selecting sample data bypasses manual account, balance, and committee entry (Priority: P1)

Once the coordinator selects "Load sample data", the Chart of Accounts, Opening Balances, and Committee tabs are no longer available to fill in manually — the sample data supplies its own accounts, opening balances, and committee/AGM history, so entering that information by hand would be wasted effort at best and confusing/duplicated data at worst.

**Why this priority**: This is the second explicit ask in the issue ("should bypass the account entry, opening balances and Committee settings") and is what makes relocating the checkbox meaningful rather than cosmetic — it depends on User Story 1 giving the coordinator a place to make the choice before reaching those tabs.

**Independent Test**: Check "Load sample data" on the Organisation Settings tab, confirm the Chart of Accounts, Opening Balances, and Committee tab headers become unavailable and Next skips straight from Organisation Settings to Review, then finish setup and confirm the resulting app has a chart of accounts, opening balances, and committee/AGM data present without any of it having been entered by hand.

**Acceptance Scenarios**:

1. **Given** "Load sample data" is checked on the Organisation Settings tab, **When** the coordinator looks at the tab strip, **Then** the Chart of Accounts, Opening Balances, and Committee tab headers are shown as unavailable.
2. **Given** those three tabs are unavailable, **When** the coordinator clicks "Next" from the Organisation Settings tab, **Then** the wizard advances directly to the Review tab, skipping all three.
3. **Given** those three tabs are unavailable, **When** the coordinator clicks directly on one of their headers, **Then** nothing happens — the tab does not open.
4. **Given** "Load sample data" is checked and the three tabs were never visited, **When** the coordinator clicks Finish, **Then** setup completes successfully, with account, opening-balance, and committee data all supplied by the seeded sample data.

---

### User Story 3 - Changing the sample-data choice keeps the wizard consistent (Priority: P2)

A coordinator can change their mind about sample data before finishing setup — checking it after already entering some accounts, balances, or committee titles by hand, or unchecking it after having checked it — and the wizard responds predictably rather than leaving stale or contradictory data queued.

**Why this priority**: A direct consequence of User Stories 1 and 2's bypass behavior — without defined behavior here, a coordinator who changes their mind could end up with manually-entered data silently mixed with sample data, or with tabs stuck in the wrong state. Lower priority than the two above because it's a consistency guarantee around the core behavior, not the core behavior itself.

**Independent Test**: Queue an account, an opening balance, and a committee title, then check "Load sample data" and confirm all three are discarded and their tabs become unavailable; then uncheck "Load sample data" and confirm the three tabs become available again, starting empty, and Finish once again requires a posted opening balance.

**Acceptance Scenarios**:

1. **Given** the coordinator has queued a Chart of Accounts entry, an opening balance, or a committee title, **When** they check "Load sample data", **Then** that queued data is discarded and no longer appears on its tab or on the Review tab.
2. **Given** "Load sample data" is checked, **When** the coordinator unchecks it, **Then** the Chart of Accounts, Opening Balances, and Committee tabs become available again, each starting empty.
3. **Given** "Load sample data" was unchecked after being checked, **When** the coordinator clicks Finish without entering any opening balance, **Then** Finish is rejected the same way it is today when no sample data and no opening balance are present.

---

### Edge Cases

- The debug sample-data seeder is unavailable (release build): the checkbox never appears, so the three tabs are never bypassed by this feature and behave exactly as they do today.
- The coordinator reaches the Chart of Accounts, Opening Balances, or Committee tab before ever touching the checkbox (all three are reachable from Organisation Settings without deciding on sample data first): each behaves exactly as it does today until "Load sample data" is checked.
- The coordinator checks and unchecks "Load sample data" more than once before finishing: only the state at the moment each toggle happens matters — checking always discards whatever is currently queued on the three tabs, regardless of how many times it's been toggled before.
- The coordinator clicks Finish while the three tabs are unavailable but a required field on the Organisation Settings tab itself is still invalid: Finish is rejected the same way an invalid submission is rejected today — bypassing the other tabs does not bypass the Organisation Settings tab's own validation.

## Requirements

### Functional Requirements

- **FR-001**: The setup wizard MUST present the "Load sample data" option on the Organisation Settings tab (the wizard's first tab) instead of the Review tab, subject to the same debug-build-only availability rule that applies today.
- **FR-002**: The Review tab MUST continue to show whether sample data will be loaded, as a read-only value alongside its other summarized settings, rather than as an interactive checkbox.
- **FR-003**: When "Load sample data" is checked, the Chart of Accounts, Opening Balances, and Committee tabs MUST become unavailable — their tab headers MUST NOT open their content when clicked, and MUST be visibly distinguishable as unavailable rather than behaving identically to an enabled tab that silently does nothing.
- **FR-004**: When "Load sample data" is checked and the coordinator is on the Organisation Settings tab, clicking "Next" MUST advance directly to the Review tab, skipping the Chart of Accounts, Opening Balances, and Committee tabs.
- **FR-005**: When "Load sample data" is checked, Finish MUST NOT require any Chart of Accounts entry, opening balance, or committee title to have been queued — the existing rule that Finish requires either a posted opening balance or sample data selected continues to apply, unchanged, as sample data's already-satisfied side of that either/or.
- **FR-006**: Checking "Load sample data" MUST discard any Chart of Accounts entries, opening balances, or committee titles already queued in the current setup session, so they are not submitted alongside the sample data.
- **FR-007**: Unchecking "Load sample data" MUST make the Chart of Accounts, Opening Balances, and Committee tabs available again for normal manual entry, each starting empty.
- **FR-008**: The debug sample-data seeder MUST already produce chart-of-accounts entries, an opening-balance position, and committee/AGM data equivalent in kind to what the three bypassed tabs would otherwise have captured, so bypassing manual entry never leaves the resulting sample setup incomplete; any of that data currently missing from the seeder MUST be added.
- **FR-009**: This feature MUST NOT change what happens when "Load sample data" is unavailable (release builds) or unchecked — every other tab's fields, validation, and default values remain exactly as defined by the existing tabbed wizard.

## Key Entities

- **Sample-data opt-in (in-session flag)**: Whether the coordinator has chosen to load sample data during this setup session — the same underlying flag that exists today, now read from the Organisation Settings tab instead of the Review tab, and now also the switch that governs whether the Chart of Accounts, Opening Balances, and Committee tabs accept manual entry.

## Success Criteria

### Measurable Outcomes

- **SC-001**: 100% of first-run walkthroughs in a debug build show the sample-data decision on the Organisation Settings tab; it no longer appears on the Review tab.
- **SC-002**: When sample data is selected, 100% of attempts to open the Chart of Accounts, Opening Balances, or Committee tabs (by header click or Next) are prevented, rather than merely discouraged.
- **SC-003**: 100% of setups completed with sample data selected result in a chart of accounts, an opening-balance position, and committee/AGM data being present, with zero manual entry required in any of the three bypassed tabs.
- **SC-004**: A coordinator can toggle "Load sample data" on and off at least once before finishing and reach a successful Finish in both the bypassed and full-manual-entry state, with no leftover data from the other state.

## Assumptions

- "Seed Data checkbox" in the issue refers to the existing debug-only "Load sample data" checkbox already present on the Review tab (per spec 017's FR-025); this feature relocates and extends it rather than introducing a second checkbox.
- "Organisation Settings tab" refers to the existing first tab of that name introduced by spec 017; the checkbox is added to that tab's content alongside what already lives there (organisation name, appearance/theme, membership & fees, sales tax), without changing those existing fields.
- "The account entry" in the issue means this wizard's own Chart of Accounts tab (its add-account form and queued/existing account lists) — the standalone Finance ▸ Chart of Accounts page outside the wizard is unaffected. Likewise "opening balances" and "Committee settings" mean this wizard's own Opening Balances and Committee tabs, not the standalone Opening Balances page or the Settings page's post-setup committee configuration. This mirrors spec 017's own scoping to the first-run `/setup` wizard.
- A review of the current debug data seeder (`DebugDataSeeder`) shows it already creates its own chart-of-accounts entries (beyond the seeded system accounts), posts a full opening-balance position across every eligible account, and elects committee officeholders and general members through two seeded AGMs. On that basis, FR-008's "must be added" condition is not expected to trigger — but it is written as a requirement, not skipped, so planning/implementation verifies this rather than assuming it, and updates the seeder if the assessment turns out to be incomplete.
- Release-build behavior is unchanged: the checkbox — and therefore the bypass it now drives — only exists when a debug data seeder is registered, exactly as spec 017's FR-025 already required.
- Documentation impact: `specs/017-setup-wizard-tabs/spec.md` (its tab descriptions, FR-025's reference to the Review tab, and its "ADDED Requirements" tab-strip description) and the `capabilities/app-host` living spec's "Optional sample-data seeding" section both currently describe the checkbox at its old location and must be updated once this feature is implemented, per the project's standing rule to keep touched specs/docs current in the same task.
