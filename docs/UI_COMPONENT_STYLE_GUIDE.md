# UI Component Style Guide

## Philosophy

StageFright Community's design system is **"Midnight Glass"** — a glassmorphism theme of soft gradient-orb backgrounds, frosted/blurred panels, and a condensed display typeface, defined entirely as CSS custom properties in `StageFright.App/wwwroot/app.css`. The source-of-truth mockup is `StageFright UI Options.dc.html` (repo root, palette "a").

**Stack**: Bootstrap (via Blazor.Bootstrap) supplies layout/spacing utility classes (`d-flex`, `gap-2`, `mb-2`, `input-group`) and a few widgets (tabs, sortable lists); **Radzen.Blazor** supplies every interactive control that needs one (data grids, switches, most form inputs). There is no third, hand-rolled CSS framework — don't invent new utility classes when a Bootstrap one already does the job, and don't reach for a plain `<input>`/`<select>` where a Radzen or `RadzenSwitch` equivalent is the established pattern (see [Component Standards](#component-standards) below).

**Key Principles:**
- One design-token source (`app.css`'s `--sf-*` custom properties) — never hardcode a color, always reference a token
- Light and dark variants defined per `[data-bs-theme]`, toggled app-wide by `ThemeProvider`/the sidebar's `RadzenSwitch`
- Compact, information-dense layouts using Bootstrap spacing utilities, not bespoke spacing CSS
- Accessible: semantic roles, `aria-label`/`aria-live` on dynamic regions, keyboard-navigable sidebar

---

## Design Tokens

All tokens are CSS custom properties defined once in `StageFright.App/wwwroot/app.css`, redefined per theme under `:root, [data-bs-theme="light"]` and `[data-bs-theme="dark"]`. Never hardcode a hex value in component markup or CSS — reference the token.

### Typography

Display/body typeface is **Saira Semi Condensed** (bundled as `.woff2`, weights 400/500/600/700), falling back to `Segoe UI, system-ui, sans-serif`:

```css
html, body {
    font-family: 'Saira Semi Condensed', 'Segoe UI', system-ui, sans-serif;
}
```

Radzen and Bootstrap both pick this up via `--rz-text-font-family` and `--bs-body-color`/`--bs-body-bg` overrides set on `.theme-root` (the wrapper `ThemeProvider` renders around the whole app), so a component never needs to set font-family itself.

### Color Tokens (excerpt)

| Token | Light | Dark | Used for |
|---|---|---|---|
| `--sf-bg` | `#e6edfb` | `#050d1c` | Page background base (under the gradient orbs) |
| `--sf-text` | `#0c1c38` | `#eaf1ff` | Primary text |
| `--sf-sub` | `rgba(20,45,90,.6)` | `rgba(205,222,255,.62)` | Secondary/muted text |
| `--sf-accent` / `--sf-accent2` | `#2b63e8` / `#0aa6d8` | `#4f8dff` / `#38d2ff` | Primary buttons, active nav, sidebar logo gradient |
| `--sf-good` | `#0e9a6a` | `#3ddca0` | Positive balances, success states |
| `--sf-bad` | `#d5445f` | `#ff7d92` | Negative balances, danger states, badges |
| `--sf-glass` / `--sf-glass2` | `rgba(255,255,255,.55)` / `.8` | `rgba(255,255,255,.06)` / `.11` | Frosted panel backgrounds (sidebar, cards, pills) |
| `--sf-brd` | `rgba(255,255,255,.9)` | `rgba(255,255,255,.14)` | Panel borders |
| `--sf-line` | `rgba(10,40,90,.1)` | `rgba(255,255,255,.08)` | Hairline dividers |

A handful of legacy alias tokens (`--sf-body-bg`, `--sf-card-bg`, `--sf-balance-positive`, etc.) exist purely for older styles that reference them; new CSS should use the primary tokens above directly.

### Background: Gradient Orbs

The shell background (`.shell-container`) is three soft radial gradients over `--sf-bg`, using `--sf-orb1/2/3`:

```css
background:
    radial-gradient(600px 420px at 12% 8%, var(--sf-orb1), transparent 65%),
    radial-gradient(520px 400px at 88% 18%, var(--sf-orb2), transparent 60%),
    radial-gradient(700px 500px at 55% 110%, var(--sf-orb3), transparent 60%),
    var(--sf-bg);
```

### Frosted / Glass Panels

Sidebar, cards, and pills share the same recipe: a translucent `--sf-glass*` background, a `--sf-brd` border, and `backdrop-filter: blur(...)`:

```css
.shell-sidebar {
    background: var(--sf-glass);
    border-right: 1px solid var(--sf-brd);
    backdrop-filter: blur(24px);
    box-shadow: 0 16px 44px var(--sf-dock-shadow), inset 0 1px 0 rgba(255,255,255,.14);
}
```

---

## Layout

Use Bootstrap utility classes for spacing and flex layout (`d-flex`, `align-items-center`, `justify-content-between`, `gap-2`, `mb-2`, `flex-wrap`) rather than inline styles or bespoke CSS classes — this is the established pattern across every page (`MemberList.razor`, `SettingsPage.razor`, etc.):

```razor
<div class="page-header d-flex align-items-center justify-content-between mb-2">
    <h1 class="h3 mb-0">Members</h1>
    <button class="btn btn-primary btn-sm" @onclick="AddMember">Add Member</button>
</div>
```

Reach for a `.razor.css` isolation file only when a style is genuinely scoped to one component; everything else belongs in `app.css`.

---

## Component Standards

### Data Grids — always `RadzenDataGrid<TItem>`

Every tabular view uses `RadzenDataGrid<TItem>`, never a plain `<table>` or a `table-responsive` wrapper `div`. `MemberList.razor` is the reference:

```razor
<RadzenDataGrid Data="@DisplayMembers" TItem="Member"
                AllowSorting="true" AllowPaging="true" PageSize="15"
                class="rz-shadow-0">
    <Columns>
        <RadzenDataGridColumn TItem="Member" Property="SortableFullName" Title="Name" Width="200px">
            <Template Context="member">
                <a href="#" @onclick:preventDefault @onclick="() => ViewMember(member.Id)"
                   class="text-decoration-none fw-semibold">
                    @member.SortableFullName
                </a>
            </Template>
        </RadzenDataGridColumn>
        <RadzenDataGridColumn TItem="Member" Property="Email" Title="Email" Width="200px" />
    </Columns>
</RadzenDataGrid>
```

A column needing a "select all" header checkbox uses a `HeaderTemplate`, not a separate control outside the grid.

**Exception**: `ReportViewer.razor` hand-rolls its own paging (fixed at page size 15) because its dynamic columns, section headers, and subtotal/grand-total rows don't fit RadzenDataGrid's typed-column model.

### List Boxes — always `BorderedListBox<TItem>`

Every bordered list box (queued items, role lists, read-only summaries) uses `BorderedListBox<TItem>` (`StageFright.UI/Shared/BorderedListBox.razor`), never a hand-rolled bordered `<div>`:

```razor
<BorderedListBox TItem="QueuedAccountRequest" Items="@_queuedAccounts" EmptyText="No accounts queued.">
    <RowTemplate Context="account">
        <span>@account.Name (@account.AccountType)</span>
    </RowTemplate>
</BorderedListBox>
```

It takes `Items`, a `RowTemplate`, an optional `OnRemove` (unset → read-only render; set → adds a per-row `×` remove button), and `EmptyText`. See the Setup Wizard's Chart of Accounts, Committee, and Review tabs for the reference usage.

### Toggles — always `<RadzenSwitch>`

Every on/off toggle uses `<RadzenSwitch>` with `@bind-Value`/`Value` + a `Change` callback (not `@bind:after`), never a hand-rolled Bootstrap `form-check form-switch` checkbox:

```razor
<label for="showInactiveSwitch" class="form-label mb-0">Show inactive members</label>
<RadzenSwitch Name="showInactiveSwitch" @bind-Value="_showInactive" Change="@(async (bool _) => await LoadAsync())" />
```

`RadzenSwitch` renders no native `onchange`-wired `<input>`. In bUnit, drive it via `cut.Find("[role=switch]").Click()` and assert state via `.GetAttribute("aria-checked")`, not `.Change(bool)`/`HasAttribute("checked")`.

> **Deliberate exception**: the Setup Wizard's own theme control is a Light/Dark `<select>` dropdown, not a switch (FR-022 of spec `017-setup-wizard-tabs`) — the wizard's screen-shell has no cascaded theme state to toggle live the way `ShellLayout` does. Don't take it as a new default over `RadzenSwitch`.

### Buttons

Bootstrap button classes, with the primary variant styled by the design tokens:

```razor
<button class="btn btn-primary btn-sm">Save</button>
<button class="btn btn-secondary btn-sm">Cancel</button>
<button class="btn btn-outline-danger btn-sm">Remove</button>
```

```css
.btn-primary {
    background: linear-gradient(135deg, var(--sf-accent), var(--sf-accent2));
    border: 0;
}
```

### Loading / Empty States — inline, not dedicated components

There are **no** `<LoadingSpinner>`/`<EmptyState>`/`<Badge>`/`<IconButton>` shared components — those are conditional inline markup per page, following `MemberList.razor`'s pattern:

```razor
@if (_loading)
{
    <p role="status" aria-live="polite">Loading members…</p>
}
else if (!DisplayMembers.Any())
{
    <p class="text-muted">No members found.</p>
}
```

Keep this pattern for new pages rather than introducing a new shared loading/empty-state component.

### Icons

Sidebar and inline icons are Bootstrap Icons (MIT-licensed, https://icons.getbootstrap.com) inlined as CSS masks so they render in `currentColor` and follow theme/active state — not an icon font tag or emoji glyph:

```css
.nav-icon {
    width: 17px;
    height: 17px;
    background-color: currentColor;
    -webkit-mask: center / contain no-repeat;
    mask: center / contain no-repeat;
}
```

---

## Navigation Shell

Navigation is a **fixed vertical sidebar** (`Layout/ShellLayout.razor`), not a top nav bar. It's 232px wide (`--sf-sidebar-w`), frosted-glass styled, and renders `IEnumerable<IMenuItemProvider>` items ordered by `DisplayOrder`, with expandable sub-item groups (auto-expanding while a child route is active) and badge counts:

```razor
@foreach (var provider in MenuProviders.OrderBy(p => p.DisplayOrder))
{
    @foreach (var item in provider.GetMenuItems().OrderBy(i => i.DisplayOrder))
    {
        <li class="sidebar-item">
            <a class="sidebar-link @(IsActive(item.Route) ? "active" : "")"
               href="@item.Route" @onclick:preventDefault @onclick="() => Navigate(item.Route)">
                <span class="nav-icon @IconClass(item.Route)" aria-hidden="true"></span>
                <span class="sidebar-label">@item.Title</span>
                @if (!string.IsNullOrEmpty(item.BadgeText))
                {
                    <span class="sidebar-badge" role="status" aria-label="@item.BadgeText items">@item.BadgeText</span>
                }
            </a>
        </li>
    }
}
```

A pill-shaped light/dark `RadzenSwitch` sits in the top bar (`shell-topbar`, offset past the sidebar width), hidden on `/setup` per the Setup Wizard exception above. See [ARCHITECTURE.md § Navigation](ARCHITECTURE.md#navigation) for how `IMenuItemProvider` contributes items.

---

## Dashboard Tiles

Dashboard tiles opt into one of four grid footprints via `DashboardTileSize` (`StageFright.Plugins.Contracts`), mapped to CSS-Grid span classes in `app.css`:

| `TileSize` | CSS class | Grid footprint |
|---|---|---|
| `OneByOne` (default) | `.tile-size-1x1` | 1 col × 1 row |
| `OneByTwo` | `.tile-size-1x2` | 2 cols × 1 row |
| `TwoByOne` | `.tile-size-2x1` | 1 col × 2 rows |
| `TwoByTwo` | `.tile-size-2x2` | 2 cols × 2 rows |

```css
.sf-dash-grid {
    display: grid;
    grid-template-columns: repeat(auto-fit, minmax(240px, 1fr));
    grid-auto-rows: minmax(120px, auto);
    grid-auto-flow: dense;
    gap: .5rem;
}

.tile-size-1x2 { grid-column: span 2; grid-row: span 1; }
```

Below 576px every tile collapses to a single column. See [ARCHITECTURE.md § Dashboard Tiles](ARCHITECTURE.md#dashboard-tiles) for how a provider opts into a size.

---

## Forms

`EditForm` + `DataAnnotationsValidator` + Bootstrap form classes, with Radzen inputs where the field needs one (dropdowns, numeric steppers, switches):

```razor
<EditForm Model="@model" OnValidSubmit="@HandleSubmit">
    <DataAnnotationsValidator />

    <div class="mb-2">
        <label class="form-label">Name *</label>
        <InputText @bind-Value="model.Name" class="form-control form-control-sm" />
        <ValidationMessage For="@(() => model.Name)" />
    </div>

    <div class="d-flex gap-2 justify-content-end">
        <button type="button" class="btn btn-secondary btn-sm" @onclick="OnCancel">Cancel</button>
        <button type="submit" class="btn btn-primary btn-sm">Save</button>
    </div>
</EditForm>
```

> **bUnit limitation**: a shared child form component (e.g. `AddAccountForm`/`OpeningBalanceEntryForm`) rendered *inside* another component's own outer `<EditForm>` — as in the Setup Wizard — cannot have its nested `<form>` submit simulated by bUnit; its inner `<form>` collapses when bUnit builds its AngleSharp DOM. This is a bUnit limitation, not a production bug. In tests, invoke the child component's own `EventCallback` parameters directly (`cut.FindComponent<ChildTab>().Instance.OnSubmit.InvokeAsync(...)`) instead of simulating the nested submit.

---

## Accessibility

- Semantic elements and roles: `<nav role="navigation" aria-label="Main navigation">`, dynamic status text as `role="status" aria-live="polite"`.
- Every icon-only or ambiguous control gets `aria-label` (e.g. `aria-label="Edit @member.SortableFullName"`, `aria-label="Remove"`).
- Interactive sidebar links are keyboard-reachable (`tabindex="0"`) and expose `aria-expanded`/`aria-label` on the collapse chevron.
- Don't rely on color alone — the light/dark good/bad tokens (`--sf-good`/`--sf-bad`) pair with text, not standalone color swatches.
- All text must meet WCAG AA contrast against its token-defined background in both themes.

---

## Reports

`ReportViewer.razor` renders `ReportData` (rows/columns/sections/subtotals) inside a modal with a synchronous "Generating…" state; a Cancel action appears after 5 seconds. It is the one place that deviates from `RadzenDataGrid` (see above). In QuestPDF-rendered checkbox-style cells (`AttendanceRollPdfRenderer` etc.), render a checked box as a bordered `Container` with a centered "✓" glyph — **never** a solid filled box.

---

## Do's and Don'ts

### ✅ DO
- Reference `--sf-*` tokens for every color; never hardcode a hex value
- Use `RadzenDataGrid`, `BorderedListBox`, and `RadzenSwitch` for their respective concerns — don't hand-roll an equivalent
- Use Bootstrap utility classes for spacing/layout
- Keep loading/empty states as simple inline conditional markup, matching existing pages
- Add `aria-label`/`aria-live` to icon-only controls and dynamic status regions
- Put component-scoped styles in a `.razor.css` isolation file only when truly scoped; otherwise use `app.css`

### ❌ DON'T
- Introduce a second CSS framework or hand-rolled utility-class system alongside Bootstrap
- Use a plain `<table>` or Bootstrap `form-check form-switch` where `RadzenDataGrid`/`RadzenSwitch` is the standard
- Hardcode a color instead of a design token
- Put `@code { }` blocks in a `.razor` file — logic belongs in the paired `.razor.cs`
- Add a hand-written `.js` file for interaction that Blazor/Radzen/Blazor.Bootstrap already provides

---

## Testing UI Components

bUnit + NSubstitute is the standard:

```csharp
[Fact]
public void Should_DisplayMembersList_When_ComponentRendered()
{
    var memberService = Substitute.For<IMemberService>();
    memberService.GetActiveMembersAsync(Arg.Any<CancellationToken>())
        .Returns(new List<Member> { new() { FirstName = "John", LastName = "Doe" } });

    using var ctx = new TestContext();
    ctx.Services.AddSingleton(memberService);
    var cut = ctx.RenderComponent<MemberList>();

    Assert.Contains("John", cut.Markup);
}

[Fact]
public void Should_ToggleShowInactive_When_SwitchClicked()
{
    // RadzenSwitch renders role="switch"; drive it via Click(), assert via aria-checked.
    using var ctx = new TestContext();
    var cut = ctx.RenderComponent<MemberList>();

    cut.Find("[role=switch]").Click();

    Assert.Equal("true", cut.Find("[role=switch]").GetAttribute("aria-checked"));
}
```

Known flaky tests: `ParticipationGridTests.DoesNotRender_FeeColumns` and `EventFormTests.DoesNotRender_FeeOrPaidFields` intermittently false-positive because bUnit-rendered markup embeds random lowercase-hex GUIDs and "fee" is a valid 3-hex-digit run — treat a failure there as this known flake (re-run in isolation) unless the diff actually touches Events/ParticipationGrid/EventForm.

---

## Questions & Support

- Check [ARCHITECTURE.md](ARCHITECTURE.md) for how a component's data flows in from a service
- Review existing pages under `src/StageFright.UI/Pages/` for the closest analogous pattern before introducing a new one
- Check `StageFright.App/wwwroot/app.css` for the current token set before adding a new one
- Open an issue with screenshots for any UI element in question
