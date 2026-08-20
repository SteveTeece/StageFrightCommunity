# Implementation Plan: Setup Wizard Tabbed Redesign

**Branch**: `017-setup-wizard-tabs` | **Date**: 2026-08-21 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `specs/017-setup-wizard-tabs/spec.md`

## Summary

Replace the setup wizard's linear five-step flow with one screen built on the same `Tabs`/`Tab` component `FinancePage.razor` already uses, regrouped so no tab holds only one or two settings. Two existing standalone Finance features — the Chart of Accounts "add account" form and the Opening Balances entry table — are extracted into shared components that take a submit callback, so the standalone pages keep persisting immediately while the wizard's new tabs use the same fields/validation to *queue* entries (accounts, opening balances, and — already deferred today — committee office-holder titles) in wizard state until Finish. A new small shared `BorderedListBox` component renders every list box in the app (the queued-roles list, the queued-accounts list, and the review tab's summaries) as a bordered list, established here as an app-wide convention. `SetupRequest`/`SetupService` grow to carry the queued accounts and opening-balance entries so Finish creates the Settings record, the queued accounts, the queued committee roles, and the posted opening balances together; Finish is blocked unless opening balances were posted or "load sample data" (now pinned to the review tab) was selected instead. The debug seeder gains an explicit opening-balance posting step for the accounts it creates.

## Project Structure

### Documentation (this feature)

```
specs/017-setup-wizard-tabs/
├── spec.md
├── plan.md              # this file
├── research.md          # Phase 0 output
├── data-model.md         # Phase 1 output
├── contracts/            # Phase 1 output (UI contract)
│   └── setup-wizard-ui-contract.md
└── checklists/
    └── requirements.md
```

### Source code (repository root)

```
src/StageFright.UI/
├── Pages/Setup/
│   ├── SetupWizard.razor              # rewritten: tab strip host (Tabs/Tab), replaces the 5-step shell
│   ├── SetupWizard.razor.cs           # rewritten: per-tab state, queue collections, Finish orchestration
│   ├── SetupFormModel.cs              # extended: no structural change to existing fields; queues live in SetupWizard.razor.cs
│   └── Tabs/                          # new — one component per wizard tab
│       ├── GeneralAppearanceTab.razor(.cs)     # org name, theme dropdown (FR-022)
│       ├── MembershipFeesTab.razor(.cs)        # annual/attendance fee, renewal month, audit retention
│       ├── SalesTaxTab.razor(.cs)              # tax applicable checkbox, rate, fee tax codes
│       ├── CommitteeTab.razor(.cs)             # AGM month, seat target, role +/-, BorderedListBox
│       ├── ChartOfAccountsTab.razor(.cs)       # hosts shared AddAccountForm in queuing mode + BorderedListBox
│       ├── OpeningBalancesTab.razor(.cs)       # hosts shared OpeningBalanceEntryForm in queuing mode
│       └── ReviewTab.razor(.cs)                # read-only summary, BorderedListBox x2, sample-data checkbox
├── Shared/
│   ├── BorderedListBox.razor(.cs)              # new — FR-007's app-wide list-box rendering
│   ├── AddAccountForm.razor(.cs)               # new — extracted from ChartOfAccountsPage; OnSubmit callback
│   └── OpeningBalanceEntryForm.razor(.cs)      # new — extracted from OpeningBalancesWizard; OnSubmit callback
└── Pages/Finance/
    ├── ChartOfAccountsPage.razor(.cs)          # refactored to consume the shared AddAccountForm (immediate-create callback)
    └── OpeningBalancesWizard.razor(.cs)        # refactored to consume the shared OpeningBalanceEntryForm (immediate-post callback)

src/StageFright.Core/
├── Modules/Settings/
│   ├── SetupRequest.cs                 # extended: QueuedAccounts, QueuedOpeningBalanceEntries, OpeningBalanceAsAtDate
│   └── SetupService.cs                 # extended: create queued accounts, then post queued opening balances, after Settings
└── (no changes to IAccountService / IOpeningBalanceService contracts — SetupService composes the existing ones)

src/StageFright.App/Seeding/
└── DebugDataSeeder.cs                  # extended: post an opening balance for its seeded accounts (FR-026)

tests/
├── StageFright.UI.Tests/
│   ├── Pages/Setup/SetupWizardTests.cs             # rewritten for the tabbed flow
│   ├── Pages/Setup/Tabs/*Tests.cs                  # new, one per tab component
│   ├── Shared/BorderedListBoxTests.cs              # new
│   ├── Shared/AddAccountFormTests.cs               # new
│   └── Shared/OpeningBalanceEntryFormTests.cs      # new
├── StageFright.Core.Tests/Modules/Settings/SetupServiceTests.cs   # extended
├── StageFright.Data.Tests/... (only if a repository call pattern changes; none expected)
└── StageFright.Integration.Tests/... setup-journey test(s)         # extended for tabbed navigation + gating
```

**Structure Decision**: Web (Blazor Hybrid) single-project-per-layer structure, unchanged from the rest of the app. The only new namespace is `StageFright.UI.Pages.Setup.Tabs/`, following the existing `Pages/Settings/*Tab.razor(.cs)` naming convention already used for the (unrelated) Settings page's own tabs, so a future contributor recognizes the pattern immediately.

## Constitution Check

| Principle | Assessment |
|---|---|
| §3.1 Clean Code / simplicity | PASS — extracting `AddAccountForm`/`OpeningBalanceEntryForm` as callback-driven shared components keeps both the standalone pages and the wizard's tabs simple; no branching on an internal "mode" flag inside the shared components themselves (see research.md decision). |
| §3.2 SOLID | PASS — the shared forms take an `OnSubmit` callback (Dependency Inversion: the component doesn't know or care whether its caller persists immediately or queues); `BorderedListBox` is a single small, reusable component (Single Responsibility, Open/Closed — any future list box composes it without modifying it). |
| §3.4/§3.5/§3.6 Soft delete & financial immutability | PASS — no change to Fee/Payment/Transaction/JournalEntry immutability. Opening balances still post as one immutable `JournalEntry` + balanced `Transaction` lines via the existing `OpeningBalanceService`; nothing is edited or deleted. |
| §4.5 One class/component per file | PASS — every new `.razor` gets a paired `.razor.cs`; `BorderedListBox`, `AddAccountForm`, `OpeningBalanceEntryForm`, and each of the 7 tab components are each their own file pair. |
| §4.7 Blazor Component Patterns (mandatory code-behind, conditional CSS isolation) | PASS — no `@code` blocks planned; `.razor.css` added only if a genuinely component-scoped style is needed (e.g. the bordered-list border/scroll treatment), otherwise it goes in the global stylesheet per CLAUDE.md. |
| §5 Custom exceptions at boundaries | PASS — `SetupService.InitializeAsync` already translates failures into `Core.Exceptions.ValidationException`; extending it to call `IAccountService`/`IOpeningBalanceService` (which already throw the project's `ValidationException`) needs no new exception types. |
| §8 Plug-in architecture | PASS — no extension-point contracts change; this is entirely core-module UI/orchestration work. |
| §11 Testing Standards (exhaustive path coverage) | PASS (tracked, not yet executed) — plan's test list above covers success, validation-failure, boundary (blank/duplicate/empty-queue), and the new Finish-gating exception paths; `tasks.md` will enumerate each as a discrete task per CLAUDE.md's non-negotiable coverage rule. |

No violations — Complexity Tracking is omitted.
