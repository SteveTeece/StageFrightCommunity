# UI Contract: Setup Wizard Tabbed Redesign

This feature exposes no HTTP/CLI API. Its "contract" is the UI surface other code and tests code against: the route, the tab order/identifiers, and the shared components' public parameters. Nothing here is pinned by the spec's own wording (no Verbatim Constraints section exists in spec.md), so these are the plan's own concrete choices — tasks/tests should treat them as authoritative once implementation begins, and update this file if a task discovers a better name.

## Route

Unchanged: `@page "/setup"`, still resolved by `Blazor Router` per the app-host living spec's first-run redirect. No new route is introduced — every tab lives inside this one page.

## Tab order

1. **General** — organisation name, theme dropdown (FR-022).
2. **Membership & Fees** — annual fee, attendance fee, membership renewal month, audit retention years.
3. **Sales Tax** — tax-applicable checkbox, tax rate, annual/attendance fee tax treatment.
4. **Committee** — AGM month, seat count target, office-holder role queue (+ / bordered list).
5. **Chart of Accounts** — queued-account form (shared `AddAccountForm`) + bordered list of queued accounts.
6. **Opening Balances** — queued balance entries (shared `OpeningBalanceEntryForm`), covering existing + queued accounts; must come after Chart of Accounts per spec Assumptions.
7. **Review** — read-only summary of every tab, two bordered-list summaries (roles, accounts), "load sample data" checkbox (FR-025), Finish button.

Tab 5 before tab 6 is a hard ordering requirement (data dependency, not just presentation — see data-model.md's account-reference resolution). Tabs 1–4 and 7 are logically fixed by their content but have no cross-tab data dependency on order.

## Shared component parameters

### `AddAccountForm`
```
[Parameter, EditorRequired] public EventCallback<NewAccountModel> OnSubmit { get; set; }
[Parameter] public string SubmitButtonText { get; set; } = "Add Account";
```
Caller supplies `OnSubmit`; the component owns field markup, `DataAnnotationsValidator`, and the Asset-only bank-flag conditional. Duplicate-name/blank-name validation stays inside the component (shared regardless of caller) since it's identical in both immediate and queued modes — only *what happens after* a valid submit differs, and that's the callback's job.

### `OpeningBalanceEntryForm`
```
[Parameter, EditorRequired] public IReadOnlyList<Account> Accounts { get; set; }
[Parameter, EditorRequired] public EventCallback<RecordOpeningBalancesRequest> OnSubmit { get; set; }
[Parameter] public bool ShowAlreadyPostedWarning { get; set; } = true;
```
`Accounts` is supplied by the caller (the standalone page passes `IOpeningBalanceService.GetOpeningBalanceAccountsAsync()`'s result; the wizard's tab passes that same call's result concatenated with the queued accounts already created in memory-only form). `ShowAlreadyPostedWarning` defaults on for the standalone page and is turned off by the wizard tab, since first-run setup can never have a prior `OpeningBalance` entry (research.md).

### `BorderedListBox<TItem>`
```
[Parameter, EditorRequired] public IReadOnlyList<TItem> Items { get; set; }
[Parameter, EditorRequired] public RenderFragment<TItem> RowTemplate { get; set; }
[Parameter] public EventCallback<TItem>? OnRemove { get; set; }  // null/unset => read-only (review tab summaries)
[Parameter] public string EmptyText { get; set; } = "Nothing added yet.";
```
`OnRemove` unset renders a read-only bordered list (review tab); set, it renders a remove affordance per row (committee tab, Chart of Accounts tab). This is the one component every list box in the app is expected to compose (FR-007).

## Element identifiers tests will rely on

Following the existing wizard's convention of explicit `id`s on primary actions (`id="btn-next"`, `id="btn-back"`, `id="btn-finish"` already exist and are preserved):

- Tab headers: rendered by `Tabs`/`Tab`'s own markup (no new ids needed — tests select by tab `Title` text, matching how `FinancePage.razor`'s own tests already do it).
- `id="committee-role-input"`, `id="committee-role-add-btn"` — the +/entry pair on the Committee tab.
- `id="coa-tab-*"` prefix reused from `ChartOfAccountsPage`'s existing `id="account-name"`, `id="account-type"`, `id="account-is-bank"` where the shared `AddAccountForm` renders them (same ids in both callers is fine — they're never both on screen at once).
- `id="ob-tab-*"` — as-at-date input and the balance-entry table cells, mirroring `OpeningBalancesWizard`'s existing `aria-label="Balance for @row.Account.Name"` pattern for per-row inputs (no shared `id` possible across dynamic rows; tests select by `aria-label`, as the standalone page's own tests presumably already do).
- `id="seed-data-checkbox"` — moved from wherever it renders today to the Review tab specifically (FR-025).
