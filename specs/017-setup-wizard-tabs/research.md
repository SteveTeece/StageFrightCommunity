# Phase 0 Research: Setup Wizard Tabbed Redesign

## Decision: Tab component

**Decision**: Build the wizard's tab strip with the same `Tabs`/`Tab` BlazorBootstrap component `FinancePage.razor` already uses (`<Tabs><Tab Title="..." OnShown="...">...</Tab></Tabs>`), not `RadzenTabs` or hand-rolled Bootstrap nav markup.

**Rationale**: The issue explicitly says "tabs should look like the ones in the Finance screen." `Tabs`/`Tab` is already a proven, tested pattern in this codebase for exactly this shape of UI (independent panels + an active-tab callback), and reusing it avoids introducing a second tab-component convention.

**Alternatives considered**: `RadzenTabs` — used elsewhere for data-dense areas but not for navigation chrome like this, and doesn't match the issue's explicit visual reference. Hand-rolled Bootstrap `nav-tabs` — no existing precedent in this codebase (grepped; zero hits), would be a third pattern.

## Decision: Shared components use a submit callback, not an internal mode flag

**Decision**: `AddAccountForm` and `OpeningBalanceEntryForm` (extracted from `ChartOfAccountsPage` and `OpeningBalancesWizard` respectively) take a required `EventCallback<T>`-style `OnSubmit` parameter and know nothing about persistence. The standalone pages pass a callback that calls `IAccountService.CreateAsync` / `IOpeningBalanceService.RecordOpeningBalancesAsync` directly (unchanged from today); the wizard's tabs pass a callback that appends to an in-memory queue instead.

**Rationale**: Satisfies FR-016/FR-019 ("single shared experience... standalone page's existing behavior MUST remain unchanged... wizard's use MUST defer") without the component itself branching on an "am I in the wizard?" flag — Dependency Inversion (§3.2 of the constitution): the low-level persistence choice is injected by the caller, not known by the shared component. It also means the component's own tests never need to know about deferred vs. immediate mode; only the two callers' tests do.

**Alternatives considered**: An internal `Mode` enum (`Immediate`/`Deferred`) branching inside the shared component. Rejected — it would put persistence knowledge inside a component that shouldn't have it, and would grow a third branch every time a future caller needs a third behavior.

## Decision: Queued state lives in `SetupWizard.razor.cs`, not a new service

**Decision**: The lists of queued committee-role titles, queued accounts, and queued opening-balance entries are plain in-memory state owned by `SetupWizard.razor.cs` (the wizard's own code-behind), passed down to the relevant tab components as parameters/callbacks. No new "wizard session" service or singleton is introduced.

**Rationale**: The existing wizard already holds `_model` (a plain field) as its only state; committee-role titles are already collected into `SetupRequest.CommitteeOfficeHolderTitles` the same way today (a comma-split list built at submit time). Extending the same code-behind with a few more `List<T>` fields is the smallest change that satisfies the requirement and matches the file's existing shape — no architectural layer is missing that would justify a new service.

**Alternatives considered**: A dedicated `SetupWizardState` service registered per-wizard-instance. Rejected as unnecessary indirection for state that never leaves one component's lifetime.

## Decision: `SetupRequest` grows to carry queued accounts and opening balances; `SetupService.InitializeAsync` composes `IAccountService`/`IOpeningBalanceService`

**Decision**: `SetupRequest` gains `IReadOnlyList<QueuedAccountRequest> QueuedAccounts`, `IReadOnlyList<OpeningBalanceEntry> QueuedOpeningBalances`, and `DateTime OpeningBalanceAsAtDate` (all optional/defaulted, mirroring how `CommitteeOfficeHolderTitles` is already optional). `SetupService.InitializeAsync` creates the Settings record and default event types (as today), then creates each queued account via the already-injected `IAccountService`, then — if any opening balances were queued — posts them via `IOpeningBalanceService.RecordOpeningBalancesAsync` (resolving queued-account references to the just-assigned real `AccountId`s), then creates committee office-holder titles exactly as it does today.

**Rationale**: Confirmed neither `AccountService.CreateAsync` nor `OpeningBalanceService.RecordOpeningBalancesAsync` depends on a Settings record existing (read their source — `AccountService` needs only `IAccountRepository`/audit/reconciliation-repo; `OpeningBalanceService` needs only account/GL/journal repos), so sequencing them after `_settingsRepo.SaveAsync` inside `InitializeAsync` is safe and keeps FR-008's "one submission" requirement literally true — a single service call. This mirrors the existing, already-shipped pattern of `CommitteeOfficeHolderTitles` being created inside `InitializeAsync` rather than by the UI calling a separate service after the fact.

**Alternatives considered**: Have `SetupWizard.razor.cs` call `SetupService.InitializeAsync` then separately loop `AccountService.CreateAsync`/`OpeningBalanceService.RecordOpeningBalancesAsync` itself. Rejected — this would split "what Finish does" across two layers (UI orchestration + service), duplicating the sequencing logic `SetupService` already owns for office-holder titles, and would make partial-failure handling (e.g. Settings created but an account creation throws) inconsistent with how the rest of `InitializeAsync` already behaves.

## Decision: `BorderedListBox` is a small generic wrapper, not per-list-type components

**Decision**: One `BorderedListBox<TItem>` component (name: real content, not literal) renders a bordered container with a `RenderFragment<TItem>` per row and an optional per-row "remove" affordance; the committee-role list, the queued-accounts list, and the review tab's two summaries all compose it rather than each hand-rolling their own bordered `<div>`.

**Rationale**: FR-007 requires the bordered-list treatment to be one application-wide convention, not four independent implementations that could visually drift from each other. A single generic component is the natural way to guarantee that in code, not just in a style guide note.

**Alternatives considered**: A shared CSS class (`.bordered-list-box`) applied independently in each of the four places. Rejected — satisfies the visual requirement but not the "one convention any future list box follows" spirit as robustly; a future page could apply the class inconsistently (missing the overflow/scroll handling from the edge case), where a shared component enforces it once.

## Decision: Debug seeder posts an explicit opening balance rather than relying on simulated history alone

**Decision**: `DebugDataSeeder.SeedAsync` gains a call to `IOpeningBalanceService.RecordOpeningBalancesAsync` for the accounts it creates (at minimum the bank account, matching what a real coordinator would enter first), run before the historical-transaction simulation (`SeedHistoricalTransfersAsync` etc.) it already performs.

**Rationale**: FR-026 requires seeded accounts to have opening balances specifically, and today's seeder only reaches realistic balances by simulating years of transactions — there is no explicit `OpeningBalance`-typed journal entry today. Posting one keeps the seeded ledger's starting point auditable the same way a real coordinator's would be, and gives `HasExistingOpeningBalancesAsync` a truthful answer if a coordinator later opens the standalone Opening Balances page.

**Alternatives considered**: Treat the existing simulated transaction history as sufficient and leave the seeder unchanged. Rejected — explicitly contradicted by FR-026, and it would mean sample-data-seeded organisations never have an `OpeningBalance` journal entry, unlike every other org that completes setup normally.

## Decision: Opening-balance as-at-date default during setup

**Decision**: Default the Opening Balances tab's as-at date to today's date (matching `OpeningBalancesWizard`'s own fallback default before it successfully reads a financial-year-start setting), rather than trying to read `Settings.FinancialYearStartMonth` — which doesn't exist yet during setup.

**Rationale**: `OpeningBalancesWizard.razor.cs` already initializes `_asAtDate = DateTime.Today` before its `OnInitializedAsync` overwrites it from `SettingsService.GetAsync()`; during first-run setup no Settings record exists, so the wizard simply keeps that same today-date default and never attempts the settings lookup. This is the smallest change and matches an already-existing fallback rather than inventing a new one.

**Alternatives considered**: Derive a default from the `MembershipRenewalMonth` field entered earlier in the wizard. Rejected — conflates two unrelated concepts (membership renewal vs. financial year start) for no requirement-driven reason; today's date is simpler and already precedented.
