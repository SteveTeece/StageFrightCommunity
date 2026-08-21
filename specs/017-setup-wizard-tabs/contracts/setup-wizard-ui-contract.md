# UI Contract: Setup Wizard Tabbed Redesign

This feature exposes no HTTP/CLI API. Its "contract" is the UI surface other code and tests code against: the route, the tab order/identifiers, and the shared components' public parameters. Nothing here is pinned by the spec's own wording (no Verbatim Constraints section exists in spec.md), so these are the plan's own concrete choices — tasks/tests should treat them as authoritative once implementation begins, and update this file if a task discovers a better name.

## Route

Unchanged: `@page "/setup"`, still resolved by `Blazor Router` per the app-host living spec's first-run redirect. No new route is introduced — every tab lives inside this one page.

## Tab order

1. **General & Membership** — organisation name, theme dropdown (FR-022), annual fee, attendance fee, membership renewal month, audit retention years. Originally two separate tabs ("General" and "Membership & Fees"); merged into one so the wizard reads as fewer, denser steps — the two source components (`GeneralAppearanceTab`, `MembershipFeesTab`) are unchanged and simply render one after the other inside this tab's content.
2. **Sales Tax** — tax-applicable checkbox, tax rate, annual/attendance fee tax treatment.
3. **Committee** — AGM month, seat count target, office-holder role queue (+ / bordered list).
4. **Chart of Accounts** — queued-account form (shared `AddAccountForm`) + bordered list of queued accounts.
5. **Opening Balances** — queued balance entries (shared `OpeningBalanceEntryForm`), covering existing + queued accounts; must come after Chart of Accounts per spec Assumptions.
6. **Review** — read-only summary of every tab, two bordered-list summaries (roles, accounts), "load sample data" checkbox (FR-025), Finish button.

Tab 4 before tab 5 is a hard ordering requirement (data dependency, not just presentation — see data-model.md's account-reference resolution). Tabs 1–3 and 6 are logically fixed by their content but have no cross-tab data dependency on order.

## Shared component parameters

### `AddAccountForm`
```
[Parameter, EditorRequired] public EventCallback<NewAccountModel> OnSubmit { get; set; }
[Parameter] public string SubmitButtonText { get; set; } = "Add Account";
[Parameter] public IReadOnlyList<string> ExistingNames { get; set; } = Array.Empty<string>();
```
Caller supplies `OnSubmit`; the component owns field markup, `DataAnnotationsValidator`, and the Asset-only bank-flag conditional. Duplicate-name/blank-name validation stays inside the component (shared regardless of caller) since it's identical in both immediate and queued modes — only *what happens after* a valid submit differs, and that's the callback's job. `ExistingNames` (added during implementation — this is the parameter T003 flagged as an open gap) is the case-insensitive duplicate-check set: the standalone `ChartOfAccountsPage` passes its real active+archived account names, the wizard's `ChartOfAccountsTab` passes that same set unioned with names already queued this session.

### `OpeningBalanceEntryForm`
```
[Parameter, EditorRequired] public IReadOnlyList<Account> Accounts { get; set; }
[Parameter, EditorRequired] public EventCallback<RecordOpeningBalancesRequest> OnSubmit { get; set; }
[Parameter, EditorRequired] public DateTime AsAtDate { get; set; }
[Parameter] public bool ShowAlreadyPostedWarning { get; set; } = true;
[Parameter] public bool HasExistingOpeningBalances { get; set; }
[Parameter] public string SubmitButtonText { get; set; } = "Post Opening Balances";
```
`Accounts` is supplied by the caller (the standalone page passes `IOpeningBalanceService.GetOpeningBalanceAccountsAsync()`'s result; the wizard's `OpeningBalancesTab` passes that same call's result concatenated with a placeholder `Account` per queued Chart of Accounts entry, keyed by its `QueuedAccountRequest.ClientId`). `AsAtDate` is owned and rendered by the caller, not this component (the standalone page has its own Step 1; the wizard tab renders its own `#ob-tab-as-at-date` input), which is why it's required rather than defaulted. `ShowAlreadyPostedWarning` defaults on for the standalone page and is turned off by the wizard tab, since first-run setup can never have a prior `OpeningBalance` entry (research.md); `HasExistingOpeningBalances` is the caller-supplied answer that warning is conditioned on. `SubmitButtonText` defaults to "Post Opening Balances" for the standalone page's immediate-post button; the wizard tab overrides it to "Queue Balances" for its deferred-queue button.

### `BorderedListBox<TItem>`
```
[Parameter, EditorRequired] public IReadOnlyList<TItem> Items { get; set; }
[Parameter, EditorRequired] public RenderFragment<TItem> RowTemplate { get; set; }
[Parameter] public EventCallback<TItem>? OnRemove { get; set; }  // null/unset => read-only (review tab summaries)
[Parameter] public string EmptyText { get; set; } = "Nothing added yet.";
```
`OnRemove` unset renders a read-only bordered list (review tab); set, it renders a remove affordance per row (committee tab, Chart of Accounts tab). This is the one component every list box in the app is expected to compose (FR-007).

## Element identifiers tests will rely on

Following the existing wizard's convention of explicit `id`s on primary actions (`id="btn-next"`, `id="btn-finish"` already exist and are preserved — there is no `id="btn-back"`: the tabbed redesign has no dedicated Back button, since clicking any earlier tab header is the back mechanism):

- Tab headers: rendered by `Tabs`/`Tab`'s own markup (no new ids needed — tests select by tab `Title` text, matching how `FinancePage.razor`'s own tests already do it).
- `id="committee-role-input"`, `id="committee-role-add-btn"` — the +/entry pair on the Committee tab.
- `AddAccountForm` renders `id="account-name"`, `id="account-type"`, `id="account-is-bank"` — the *same* ids `ChartOfAccountsPage` already used before extraction, reused as-is by every caller including the wizard's `ChartOfAccountsTab` (no `coa-tab-` prefix was needed in practice — the two callers are never both on screen at once, so the shared ids never collide).
- `id="ob-tab-as-at-date"` — the wizard's Opening Balances tab as-at-date input (the standalone page's own Step 1 keeps its separate `id="asAtDate"`). The balance-entry table's per-row inputs mirror `OpeningBalancesWizard`'s existing `aria-label="Balance for @row.Account.Name"` pattern (no shared `id` possible across dynamic rows; tests select by `aria-label`).
- `id="seedData"` — moved from wherever it renders today to the Review tab specifically (FR-025); not `seed-data-checkbox` as first drafted here.
- `id="themeSelect"` — the General & Membership tab's Light/Dark theme dropdown (US6/FR-022), replacing the old `[role=switch]` toggle.
