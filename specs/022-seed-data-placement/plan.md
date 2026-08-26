# Implementation Plan: Move Seed Data Checkbox to Organisation Settings

**Branch**: `022-seed-data-placement` | **Date**: 2026-08-25 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `specs/022-seed-data-placement/spec.md`

## Summary

Relocate the Setup Wizard's debug-only "Load sample data" checkbox from the Review tab to the Organisation Settings tab (the wizard's first tab), and make checking it disable the Chart of Accounts, Opening Balances, and Committee tabs so Next skips straight to Review. The checkbox's own markup and behavior are unchanged — only where it lives and what it now controls are new. Tab availability is driven by BlazorBootstrap's own `Tab.Disabled` parameter (already present on the installed `Blazor.Bootstrap` 3.5.0 package, confirmed via assembly inspection — no custom CSS/JS needed), backed by a code-level guard in `SetActiveTab` so a bypassed tab can never become active regardless of how a click reaches it. No new entities, services, or database schema are introduced.

## Project Structure

### Documentation (this feature)

```
specs/022-seed-data-placement/
├── spec.md
├── plan.md              # this file
├── research.md          # Phase 0 output
├── data-model.md         # Phase 1 output
├── contracts/
│   └── ui-contract.md    # Phase 1 output
├── checklists/
│   └── requirements.md
└── tasks.md              # Phase 2 output (/speckit.companion.tasks — not created here)
```

### Source Code (repository root)

```
src/StageFright.UI/Pages/Setup/
├── SetupWizard.razor              # MODIFY — wire SampleDataTab into tab 0; Disabled="@_seedWithTestData" on
│                                   #   Chart of Accounts / Opening Balances / Committee <Tab>s; drop
│                                   #   SeedWithTestDataChanged wiring from the Review <Tab>
├── SetupWizard.razor.cs           # MODIFY — SetActiveTab/HandleNextAsync bypass the three disabled tabs;
│                                   #   new HandleSeedWithTestDataChanged(bool) clears the three in-session
│                                   #   queues when sample data is checked (FR-006)
└── Tabs/
    ├── SampleDataTab.razor        # NEW — the relocated checkbox markup (moved as-is from ReviewTab.razor)
    ├── SampleDataTab.razor.cs     # NEW — code-behind (paired file per project's Blazor component rule)
    ├── ReviewTab.razor            # MODIFY — remove the interactive checkbox block; add a read-only
    │                               #   "Load sample data" row to the existing <dl> summary (FR-002)
    └── ReviewTab.razor.cs         # MODIFY — drop SeedWithTestDataChanged param and its change handler
                                    #   (Review no longer writes this value, only reads it)

tests/StageFright.UI.Tests/Pages/Setup/
├── SetupWizardTests.cs            # MODIFY — checkbox interactions move off the Review tab; add tab-disable/
│                                   #   Next-skip/queue-discard coverage (US1–US3 acceptance scenarios)
├── SetupWizardNoSeederTests.cs    # MODIFY — assert absence on Organisation Settings tab, not Review
└── Tabs/
    ├── SampleDataTabTests.cs      # NEW — the two checkbox-behavior tests moved from ReviewTabTests
    └── ReviewTabTests.cs          # MODIFY — replace the two checkbox tests with a read-only-row test

specs/017-setup-wizard-tabs/
└── spec.md                        # MODIFY — tab descriptions, FR-025, and the "ADDED Requirements"
                                    #   tab-strip description all currently place the checkbox on Review

capabilities/app-host/
└── spec.md                        # MODIFY — "Optional sample-data seeding" section's scenario locates the
                                    #   checkbox at the final setup step; add the tab-bypass behavior
```

**Structure Decision**: Existing Blazor Razor Class Library layout (`StageFright.UI`) — no new project or top-level directory. The new component follows the established one-file-per-concern pattern already used for the Organisation Settings tab's other sub-sections (`GeneralAppearanceTab`, `MembershipFeesTab`, `ThemeSelectionTab`, `SalesTaxTab`), living in the same `Tabs/` folder next to the tab it's being extracted from.

## Constitution Check

| Principle | Assessment |
|---|---|
| §3.1–3.2 Clean Code / SOLID | PASS — `SampleDataTab` has one responsibility (render + report the opt-in); `SetupWizard` keeps orchestration (queue-clearing, tab-skip) it already owns for the other two queues. |
| §3.4 Soft Delete Pattern | N/A — no entities touched. |
| §4.5 Code Organization (one class per file) | PASS — `SampleDataTab.razor`/`.razor.cs` is a new paired file; no type gains a second file. |
| §4.7 Blazor Component Patterns (MANDATORY: code-behind, no `@code`) | PASS — `SampleDataTab.razor.cs` holds all logic; markup file carries no `@code` block. |
| §5.2–5.3 Custom Exceptions / Boundary Translation | N/A — no framework exception is newly caught or crossed; `HandleValidSubmitAsync`'s existing try/catch is untouched. |
| §11 Testing Standards (exhaustive path coverage) | PASS (tracked, not yet done) — `tasks.md` will enumerate a test per acceptance scenario in User Stories 1–3; Constitution re-check after Phase 1 confirms no path is left uncovered by the design. |
| Toggle control standard (CLAUDE.md: `<RadzenSwitch>` for on/off toggles) | Justified non-violation — see Complexity Tracking below. |

### Complexity Tracking

| Violation | Why needed | Simpler alternative rejected |
|---|---|---|
| "Load sample data" stays a plain Bootstrap `form-check` checkbox, not `<RadzenSwitch>` | This markup already exists (spec 017) and issue #313 asks only to relocate and extend its effect, not restyle it; CLAUDE.md's toggle-standard examples (Members List "show inactive", Settings theme toggle) are persistent ambient settings, whereas this is a one-time wizard opt-in field, closer in kind to the wizard's other plain form inputs than to a live state toggle. | Converting to `RadzenSwitch` while relocating was considered and rejected — it would touch styling/behavior issue #313 never asked for, outside this feature's scope, and risk regressing the two existing bUnit tests that assert on `#seedData` as a checkbox input. |

*(Re-checked after Phase 1 design below — no new violations introduced.)*

## Post-Design Constitution Re-Check

Phase 1 design (data-model.md, contracts/ui-contract.md) introduces no new entity, service, or exception boundary, and keeps the one new component to a single responsibility with a paired code-behind file. The table above stands unchanged.
