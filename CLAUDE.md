# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

<!-- SPECKIT START -->
For additional context about technologies to be used, project structure,
shell commands, and other important information, read the current plan
<!-- SPECKIT END -->

---

## Build & Test Verification

Always run `dotnet build` and the full test suite (without --no-build) after making code changes, and report the build/test results before considering a task complete.

## Spec & Docs Workflow

When a change touches behavior or UI that an existing `specs/<feature>/` doc (spec.md, contracts/, data-model.md, etc.) describes, always update that doc — and any other project documentation the change makes stale — in the same task, not as a separate follow-up. This applies even to small, presentation-only tweaks (e.g. a layout reorder): find the sentence that now reads wrong and fix it alongside the code.

## Git / Commit Workflow

Always commit all changed and new files at the end of a task — this overrides the default behavior of only committing when explicitly asked. Stage everything (`git add -A`) and commit with a message describing the change, following the existing commit style (see `git log`), unless the user explicitly says not to commit or asks for only specific files to be committed. Still show the user what changed; committing automatically doesn't replace surfacing a summary of the work.

## Editing Workflow

This environment's GateGuard hook blocks the first `Write` of a new file and the first `Edit` of a given file with a `[Fact-Forcing Gate]` unless the immediately-preceding turn text states: (1) callers/importers of the file, (2) confirmation no duplicate already exists, (3) data-schema details if applicable, (4) the user's verbatim current instruction. State those four facts as plain text right before *every* Write/Edit call, not just the first per file — repeat edits to an already-touched file get gated too. A denied batch of several files usually succeeds when retried one file at a time with the same facts.

## Commands

```bash
# Restore and build
dotnet restore
dotnet build

# Run the application (MAUI shell; database auto-migrates on first run)
dotnet run --project src/StageFright.App/

# Run all tests
dotnet test

# Run a specific test project
dotnet test tests/StageFright.Core.Tests/
dotnet test tests/StageFright.Data.Tests/
dotnet test tests/StageFright.UI.Tests/
dotnet test tests/StageFright.Integration.Tests/
dotnet test tests/StageFright.Reports.Tests/

# Run a single test by name filter
dotnet test --filter "FullyQualifiedName~MemberServiceTests"

# EF Core migrations (startup-project is the MAUI app)
dotnet ef migrations add <Name>       --project src/StageFright.Data/ --startup-project src/StageFright.App/
dotnet ef database update             --project src/StageFright.Data/ --startup-project src/StageFright.App/
dotnet ef migrations remove           --project src/StageFright.Data/ --startup-project src/StageFright.App/
```

The solution file is `StageFrightCommunity.slnx` in the repo root.

NuGet package versions are centrally managed via the root `Directory.Packages.props` (`ManagePackageVersionsCentrally`). When adding a new package reference, add `<PackageReference Include="..." />` (no `Version` attribute) to the `.csproj` and add the matching `<PackageVersion Include="..." Version="..." />` entry to `Directory.Packages.props` — never pin a version directly in a `.csproj`.

During development the SQLite database is written to `FileSystem.AppDataDirectory/stagefright.db` (the MAUI app-data directory, auto-created). Logs are written to rolling daily files under the same app-data directory.

---

## Architecture

### Project layout

| Project | Role |
|---------|------|
| `StageFright.App` | MAUI Blazor Hybrid host — composition root only. Hosts a single `BlazorWebView`; zero application logic. |
| `StageFright.Core` | Domain entities, enums, custom exceptions, repository/service contracts, application services (module slices). |
| `StageFright.Data` | Centralized DAL — `StageFrightDbContext`, EF Core migrations, one repository per entity, `UnitOfWork`. |
| `StageFright.Plugins.Contracts` | Extension-point interfaces consumed by both core and external plugins. Leaf assembly with no dependencies. |
| `StageFright.Reports` | Report infrastructure — `ReportProviderRegistry`, `PdfReportRenderer` (QuestPDF), `CsvReportExporter` (CsvHelper), shared `ReportData` model. |
| `StageFright.UI` | Razor class library — ALL Blazor UI. `App.razor` owns the router; `ShellLayout.razor` owns nav. |
| `tests/StageFright.Core.Tests` | xUnit unit tests for services and domain logic. |
| `tests/StageFright.Data.Tests` | Integration tests hitting SQLite in-memory connections. |
| `tests/StageFright.UI.Tests` | bUnit component tests. |
| `tests/StageFright.Integration.Tests` | Cross-layer user-journey tests. |
| `tests/StageFright.Reports.Tests` | Report-provider and PDF/CSV renderer tests. |
| `tests/StageFright.TestPlugin` | Sample plugin fixture (tile + report + entity). |

### Navigation

Blazor Router owns **all** navigation. Every screen has a `@page` directive. `NavigationManager.NavigateTo` is the only way to transition between pages. MAUI Shell routing is disabled — MAUI is a platform-only container. First-run detection redirects to `/setup` before the dashboard loads.

### Module structure inside `StageFright.Core`

Application logic lives in `StageFright.Core/Modules/<ModuleName>/`. Each module slice contains its services, request/response models, and menu/tile providers. Repositories are *not* module-owned; they live centrally in `StageFright.Data/Repositories/` (this is a spec-mandated deviation from pure vertical-slice, required by FR-042).

Current modules: `Agm`, `AuditTrail`, `Dashboard`, `Events`, `Finance`, `Members`, `Rehearsals`, `Settings`.

### Extension points (plugin contracts)

All extension points are defined as interfaces in `StageFright.Plugins.Contracts`:

- `IDashboardTileProvider` — provides one or more dashboard tiles.
- `ISettingsTabProvider` — adds a tab to the Settings page.
- `IMenuItemProvider` — contributes items to the navigation bar.
- `IReportProvider` — delivers a named report as `ReportData`.
- `IDataAccessProvider` — supplies a plugin `DbContext` that the migration runner merges into the same SQLite database.

MVP providers register in `MauiProgram.RegisterCoreServices`. External plugins are discovered at runtime from the `Plugins/` directory via `AssemblyLoadContext`; failures are caught, logged, and skipped — they never block startup.

### Finance / GL integrity

Every fee or payment write wraps fee creation + paired GL debit/credit + balance assertion in one `DbContext` ACID transaction. A `GLBalanceException` is thrown and the transaction rolled back if the sum of debits ≠ sum of credits. GL is the authoritative source for member balances: `outstanding = Σ(debits) − Σ(credits)` per member. Financial records (`Fee`, `Payment`, `Transaction`) are **immutable and never deleted** — corrections use GL reversing pairs.

### Reports pipeline

`IReportProvider` → `ReportData` (rows/columns/sections/subtotals) → `ReportViewer.razor` (modal "Generating…", synchronous) → `PdfReportRenderer` (QuestPDF) or `CsvReportExporter` (CsvHelper). Cancel appears after 5 s. All ten reports (`IncomeStatement`, `TrialBalance`, `AccountRegister`, `MemberAccountSummary`, `MemberList`, `Committee`, `BalanceSheet`, `BankReconciliation`, `TaxSummary`, `GeneralLedger`) follow this single pipeline. In QuestPDF-rendered checkbox-style cells (e.g. `AttendanceRollPdfRenderer`, `EventAttendanceSheetPdfRenderer`, `AgmAttendanceSheetPdfRenderer`), a checked box is a bordered `Container` with a centered "✓" glyph, never a solid filled box. `EventAttendanceSheetPdfRenderer` and `AgmAttendanceSheetPdfRenderer` are read-only, print-only sheets (spec 018) built outside the `IReportProvider` pipeline — like `AttendanceRollPdfRenderer`, their multi-column checkbox layout doesn't fit `ReportData`'s flat single-table model — and share their two-column page-composition mechanics via the internal `CheckboxSheetPdfBuilder` helper in `StageFright.Reports/Rendering/`. `AgmResultsPdfRenderer` (spec 026) is a further AGM-detail-scoped, print-only sheet built outside the `IReportProvider` pipeline — a plain position list rather than a checkbox roll, so it builds its QuestPDF document directly (following `PdfReportRenderer`'s page setup) instead of using `CheckboxSheetPdfBuilder`.

### Data grid standards

All tabular data uses `RadzenDataGrid<TItem>`, never plain `<table>` markup or a `table-responsive` wrapper div. Every grid instance follows the Members grid (`src/StageFright.UI/Pages/Members/MemberList.razor`) as the reference: `AllowSorting="true" AllowPaging="true" PageSize="15" class="rz-shadow-0"`. Grids needing a "select all" checkbox in a column header use a `HeaderTemplate` rather than a separate control outside the grid. `ReportViewer.razor` is the one exception — its dynamic columns, section headers, and subtotal/grand-total rows don't fit RadzenDataGrid's typed-column model, so it keeps hand-rolled paging (also fixed at a page size of 15) instead. A handful of grids use a smaller `PageSize` than 15 when the surrounding layout is space-constrained — `CommitteeSettingsTab`, one grid in `EventTypesTab`, and `MemberDetail.razor`'s Fee Payment History grid (spec 025, issue #305) — this is a deliberate, per-screen exception, not a new default.

### List box standards

All bordered list boxes (queued items, role lists, read-only summaries) use `BorderedListBox<TItem>` (`src/StageFright.UI/Shared/BorderedListBox.razor`), never a hand-rolled bordered `<div>`. It takes `Items`, a `RowTemplate`, an optional `OnRemove` (unset renders read-only; set adds a per-row remove button), and `EmptyText`. See the Setup Wizard's Chart of Accounts, Committee, and Review tabs for the reference usage.

### Toggle control standards

Every on/off toggle uses `<RadzenSwitch>` (`@bind-Value` + a `Change` callback, not `@bind:after`), never a hand-rolled Bootstrap `form-check form-switch` checkbox — see the Members List "show inactive" switch or the Settings page's theme toggle. `RadzenSwitch` renders no native `onchange`-wired `<input>`; drive it in bUnit via `cut.Find("[role=switch]").Click()` and assert state via `GetAttribute("aria-checked")`, not `.Change(bool)`/`HasAttribute("checked")`. The Setup Wizard's own theme control is a deliberate, spec-mandated exception (FR-022 of spec 017) — a Light/Dark `<select>` dropdown, not a switch — because the wizard's screen-shell had no cascaded state to toggle live the way Settings does; don't take it as a new default over `RadzenSwitch`.

### Dashboard tile sizing

Dashboard tiles opt into one of four sizes via `DashboardTileSize` (`StageFright.Plugins.Contracts`) — `OneByOne` (default), `OneByTwo`, `TwoByOne`, `TwoByTwo`, named RowsByColumns — by overriding `IDashboardTileProvider.TileSize`. `Dashboard.razor.cs` maps the enum to a `tile-size-*` CSS class already defined in `app.css`'s CSS-Grid layout; resizing a tile only needs the provider's `TileSize` override plus its own inner chart/layout sizing — no `Dashboard.razor` or grid CSS changes.

### Data model highlights

- **20 entities** in `StageFright.Core/Entities/`: `Member`, `CommitteeTerm`, `CommitteePositionRecord`, `CommitteeOfficeHolderType`, `AnnualGeneralMeeting`, `AgmAttendanceRecord`, `Rehearsal`, `AttendanceRecord`, `Event`, `EventType`, `ParticipationRecord`, `Account`, `Fee`, `Payment`, `Transaction`, `JournalEntry`, `BankReconciliation`, `ReconciliationLine`, `Settings`, `AuditTrailEntry`. `Category` was fully replaced by `Account` (see the `ConvertCategoriesToAccounts` migration).
- All PKs are `Guid`. All entities carry `CreatedAt`; most carry `UpdatedAt`.
- **Soft-delete** (`IsDeleted`, `DeletedAt`, `DeletedBy`) is present on every entity *except* `Fee`, `Payment`, `Transaction` (financial exemption — see "Finance / GL integrity" above), `JournalEntry` (immutable GL header, same exemption), `AuditTrailEntry` (governed by retention purge instead), `ReconciliationLine`, and `CommitteeTerm` — see each entity's doc-comment for its specific rationale.
- `AttendanceRecord` carries soft-delete fields but they are never set by any MVP workflow — records are permanently immutable once saved.

---

## Key rules (non-negotiable)

**One class per file.** Every C# class, interface, record, struct, or enum lives in its own file named exactly after the type. Private nested types are the only exception.

**Simple over clever code.** Always prefer the simplest approach to solving a problem. Keep code easily readable. Simple, readable code is better than clever/complex or difficult to read code.

**Blazor component structure.** Every `.razor` component MUST have a paired `.razor.cs` code-behind file containing all C# logic — `@code { }` blocks in `.razor` files are prohibited. A `.razor.css` CSS isolation file is added only when the component requires styles that are genuinely scoped to that component; most CSS belongs in the global stylesheet (`wwwroot/css/`).

**No custom JavaScript.** All business logic and UI interaction is in C#/Blazor. No `.js` files, no JS interop for business logic. Javascript that is part of an existing pre-written control or nuget package is permitted.

**Custom exceptions at every boundary.** Raw framework exceptions (`DbException`, `IOException`, etc.) must be caught and re-thrown as project-defined custom exceptions before crossing layer boundaries. Exception types live in `StageFright.Core/Exceptions/`.

**Exhaustive code-path test coverage.** Every reachable code path — success, validation failure, exception, boundary/null — must have automated tests before merge. Tests follow the `Should_[ExpectedBehavior]_When_[Condition]` naming convention. Test method names use `_Integration` suffix to distinguish integration tests from unit tests.

**Soft-delete everywhere (except finance).** Never hard-delete application data. Financial records (`Fee`, `Payment`, `Transaction`) are explicitly exempt — they carry no soft-delete fields and must never be deleted at all.

## Tech Stack & Conventions

This is a MAUI Blazor project using BlazorBootstrap and Radzen for charts/UI controls and double-entry accounting for finances; prefer existing patterns (e.g. month-name dropdowns, BlazorBootstrap charts) over custom SVG.

When summing financial amounts, only sum payment-related credit entries, not all GL credit entries, to avoid double-counting in double-entry accounting.

## Known Gotchas

Watch for MAUI WebView quirks: Settings tabs require the Bootstrap JS bundle and may need lazy rendering / StateHasChanged handling to avoid concurrent DbContext access and OnShown callback failures.

bUnit cannot simulate a form submit through a nested `<EditForm>`/`<form>` (e.g. a shared component like `AddAccountForm`/`OpeningBalanceEntryForm` used inside the Setup Wizard's own outer `EditForm`) — its inner `<form>` collapses when bUnit builds its AngleSharp DOM. This is a bUnit limitation, not a production bug (real Blazor rendering resolves nested forms via native nearest-ancestor targeting); in tests, invoke the child component's own `EventCallback` parameters directly (`cut.FindComponent<ChildTab>().Instance.OnSubmit.InvokeAsync(...)`) instead of simulating the nested submit.

`StageFright.UI.Tests` has at least two known-flaky tests asserting rendered markup does *not* contain "fee"/"Fee" (`ParticipationGridTests.DoesNotRender_FeeColumns`, `EventFormTests.DoesNotRender_FeeOrPaidFields`) — bUnit-rendered markup embeds random lowercase-hex GUIDs, and "fee" is a valid 3-hex-digit run, so these intermittently false-positive on an unrelated GUID. Treat a failure here as this known flake (re-run in isolation) rather than a regression, unless the diff actually touches Events/ParticipationGrid/EventForm.

Verifying a UI change in the real MAUI app requires launching with `WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS=--remote-debugging-port=9222` and driving the DOM over CDP; plain synthetic `element.click()` works for `@onclick` but does not reliably trigger Radzen's own interactive handlers (grid sort, expand/collapse) — those need a real dispatched mouse event (`Input.dispatchMouseEvent` at the element's `getBoundingClientRect()`). Screenshots must call `SetProcessDPIAware()` first or a scaled display silently crops the capture.

**`dotnet build`/`dotnet test` only show warnings for files actually recompiled that run** — an incremental build after a small change can look "clean" while a full rebuild (`dotnet build -t:Rebuild`, or delete `bin`/`obj`) surfaces hundreds more. Always judge warning counts from a full rebuild, not an incremental one.

Two systemic warning sources, if they reappear across many files at once, share one root cause each — fix at the source pattern, not file-by-file:
- **`xUnit1051`** ("should use `TestContext.Current.CancellationToken`") fires on every async call in a test method (EF Core calls, service/repository calls with an optional `CancellationToken` parameter, `File.WriteAllBytesAsync`, etc.) that doesn't pass one. Fix: pass `TestContext.Current.CancellationToken` (positionally, or `cancellationToken:` for extension methods like `ToListAsync` where it isn't the last parameter). `dotnet format analyzers --diagnostics xUnit1051` cannot bulk-apply this — xunit.analyzers' `UseCancellationTokenFixer` doesn't support Fix-All-in-Solution and `dotnet format` errors out; it has to be applied per-diagnostic (a one-off Roslyn script, not a hand pattern) or by hand.
- **`CS8602`/`CS8604`/`CS8600`/`CS8605`** (nullable-reference warnings) cluster heavily in files using NSubstitute's `Arg.Is<T>(x => ...)` predicate lambdas: NSubstitute's unconstrained generic `T` makes the lambda parameter nullable-oblivious under `#nullable enable`, so any member access or pass-through on it warns even though NSubstitute always invokes the predicate with the real non-null argument. Fix: append the null-forgiving operator (`!`) at the flagged expression, e.g. `r!.OrganizationName` or `lines!.Any(...)` — this is already the codebase's established convention (see existing `t.Description!.Contains(...)` calls), not a new pattern. Note the fix can be iterative: resolving the first `!` in a boolean chain can "unlock" a fresh warning on a later access in the same chain (narrowing doesn't always propagate past a fixed expression) — a full rebuild after fixing may reveal a few more of the same code, and that's expected, not a bug.
- **`CS8620`** on an NSubstitute `.Returns(Task.FromException<T>(...))` call means the generic argument `T` doesn't match the mocked method's *actual* nullability. `Returns<T>` infers one shared `T` across both the mocked-call receiver and the replacement task you hand it; if the interface method returns `Task<TResult?>` (e.g. `ISettingsService.GetAsync` → `Task<Settings?>`, "null before first-run setup") but you write `Task.FromException<TResult>(...)` with the non-nullable form, the two disagree only in nullability and the compiler warns on whichever side doesn't match the inferred `T`. Fix: match the mocked method's declared return type exactly, including its `?` — e.g. `Task.FromException<Settings?>(...)`, not `Task.FromException<Settings>(...)` — rather than reaching for the `!` operator (that fixes the *other* NSubstitute gotcha above, not this one). Check the real interface signature before typing the generic argument; don't assume non-null.

A misplaced `///` XML doc comment (e.g. attached to one parameter inside a multi-line record's positional-parameter list, or containing a bare `&`) throws `CS1587`/`CS1570`, and adding even one `<param>` tag to a member's doc comment makes the compiler require `<param>` tags for *all* of that member's parameters (`CS1573`) — for a record with many parameters, a plain `//` comment next to the parameter is simpler than fully documenting every parameter.

`StageFright.App` (the Windows head, `WindowsPackageType=None`) can show two `PRI249: Invalid qualifier` warnings from `WinAppSdkGenerateProjectPriFile` naming `SORTABLE-LIST`/`THEME-SWITCHER` — these come from the Blazor.Bootstrap package's own static JS assets (`blazor.bootstrap.sortable-list.js`, `blazor.bootstrap.theme-switcher.js`), which WinAppSDK's PRI resource indexer misreads as the `name.qualifier-value.ext` convention used by qualified assets (e.g. `logo.scale-200.png`); the warning is benign (build still succeeds, both files still get served) but not suppressible via `NoWarn`/`MSBuildWarningsAsMessages` since the native `makepri.exe` tool embeds "PRI249" as free text, not as a structured MSBuild warning code. Fixed by adding a `_GenerateProjectPriConfigurationFiles`-scoped `BeforeTargets` hook in `StageFright.App.csproj` that populates WinAppSDK's own exclusion item (`_AppxLayoutAssetPackageFiles`) from the two files' `PackagingOutputs`/`PriOutputs` entries — it removes them from PRI's qualifier scan only, not from Blazor's own static-web-asset copy to `wwwroot`. If new Blazor.Bootstrap component JS ever reintroduces a dash-containing filename that trips the same warning, extend that same `Condition` rather than re-deriving the mechanism from scratch.

`.github/workflows/debug-pre-release.yml` (manual-only, `workflow_dispatch`; was `debug-msix-release.yml` before it grew a macOS job) builds **Debug**-configuration packages of the `dev` branch for two platforms and publishes them together as the single rolling `dev-debug-build` pre-release (constant asset filenames, never accumulates history): `StageFrightCommunity-dev-DEBUG.msix` (Windows, self-signed MSIX) and `StageFrightCommunity-dev-DEBUG-mac.zip` (macOS, an **unsigned, universal** Mac Catalyst `.app` — `-p:CreatePackage=false -p:RuntimeIdentifier="maccatalyst-x64;maccatalyst-arm64"`, zipped with `ditto`, no signing secrets). It is three jobs: `build-windows-msix` and `build-mac-app` run in parallel and each upload their package as a CI artifact; `publish-release` `needs:` both, downloads the artifacts, and does the single `gh release delete dev-debug-build --cleanup-tag` then `gh release create` with both assets — a full delete + recreate, *not* `gh release upload --clobber`, because GitHub "immutable releases" (GA Oct 2025) permanently block replacing/deleting *or adding* an asset once a release is published (`--clobber` fails with `HTTP 422: Cannot delete asset from an immutable release`) while deleting a whole release stays allowed; the delete + recreate works whether or not repo-level release immutability is enabled. Because `publish-release` needs both builds, a break on **either** platform's tooling means no release that run (the previous one stays intact) rather than a half-updated release. If a `dev-debug-build` release was already published as immutable, it must be deleted once by hand — disabling the repo setting does not retroactively unlock it. Only the Windows job needs repo secrets: `MSIX_CERT_BASE64` (a self-signed code-signing cert, base64-encoded PFX bytes) and `MSIX_CERT_PASSWORD`. The cert's Subject **must exactly match** `Identity/@Publisher` in `Platforms\Windows\Package.appxmanifest` (currently `CN=StageFright Community`) or MSIX packaging fails — if that Publisher value is ever changed, a matching new cert must be generated and the secret rotated, and vice versa. The Mac `.app` is only ad-hoc-signed, so testers must clear the Gatekeeper quarantine flag (`xattr -dr com.apple.quarantine "StageFright Community.app"`, or right-click → Open); note `Platforms/MacCatalyst/Info.plist` still carries the MAUI-template default `UIRequiredDeviceCapabilities` = `arm64`, which could keep the Intel slice of the universal build from launching on Intel Macs — revisit that key if Intel support actually matters. `StageFright.App.csproj`'s local/CI `Release`+`Debug` builds are otherwise untouched (Windows still unpackaged, `WindowsPackageType=None`) — the workflow passes `WindowsPackageType=Package`/signing and the Mac RIDs purely as `dotnet publish -p:` command-line overrides, never persisted to the project file; the only project-file change is an inert `RuntimeIdentifierOverride`→`RuntimeIdentifier` `PropertyGroup` (the documented workaround for [WindowsAppSDK#3337](https://github.com/microsoft/WindowsAppSDK/issues/3337)) that only activates when that property is explicitly passed on the command line, as the Windows job does.
