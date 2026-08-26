# Repository Code Review

**Scope:** Full repository (all projects under `src/` and `tests/`), reviewed against the standards codified in `CLAUDE.md` — correctness, GL/finance integrity, security, architecture/conventions, test coverage, and documentation accuracy.

**Method:** 8 parallel finder passes (Finance/GL, Data layer, UI/Blazor, Reports pipeline, Security, Test coverage, Architecture/conventions, Documentation staleness), followed by dedup and spot-verification of the highest-impact claims by reading the actual source.

**Branch reviewed:** `023-merge-agms-events` (post feature-023 merge).

---

## Summary

| # | Severity | Area | File | Issue |
|---|----------|------|------|-------|
| 1 | Critical | Regression | `EventRepository.cs` | All Events list shows "—" for every event's type |
| 2 | Critical | Finance/GL | `PaymentService.cs` | Fee payments misrouted to "overpayment" when net balance ≤ 0 |
| 3 | Critical | Reports | `TaxSummaryReportProvider.cs` | "Total taxable sales" double-counts tax-exempt sales |
| 4 | Critical | Security/Finance | `BackupRepository.cs` | Restore bypasses GL-balance validation |
| 5 | Critical | Data layer | `BackupRepository.cs`, `BackupSnapshot.cs` | Backup/restore silently drops JournalEntry, BankReconciliation, ReconciliationLine |
| 6 | High | Security | `PluginLoader.cs` | No signature/hash verification of loaded plugin DLLs |
| 7 | High | Reports | `AccountRegisterReportProvider.cs` | Running balance mixes unrelated accounts together |
| 8 | High | Data layer | `OutstandingBalancesTile.razor.cs` | Concurrent DbContext access on Dashboard load |
| 9 | High | Reports/Plugins | `PluginLoader.cs` | One broken plugin `IReportProvider` crashes all reports |
| 10 | High | Finance/GL | `GLRepository.cs` | `GetAgingBucketsAsync` ignores GL, uses stale `PaidAtCreation` flag |
| 11 | Medium | Reports | `ReportViewer.razor` | Null-forgiving `SummaryRow!` crashes whole report render |
| 12 | Medium | Data layer | `SoftDeletableBaseRepository.cs` | `ArchiveAsync`'s "already archived" guard is unreachable |
| 13 | Medium | UI/Blazor | `MemberForm.razor.cs` | `OnInitializedAsync` can show/save stale member on route reuse |
| 14 | Medium | UI/Blazor | `EventDetail.razor.cs` | Same `OnInitializedAsync` issue (read-only impact) |
| 15 | Medium | UI/Blazor | `RehearsalList.razor.cs` | Only current calendar year's rehearsals are listed |
| 16 | Medium | Architecture | Multiple repositories | Raw DB exceptions leak past the DAL boundary |
| 17 | Medium | Data layer | `MemberBalanceService.cs` | N+1 query pattern on Dashboard balances tile |
| 18 | Low | Architecture | `DebugDataSeeder.cs` | Business/GL logic lives in `StageFright.App` (composition-root violation) |
| 19 | Low | Architecture | `DebugDataSeeder.cs` | 20-dependency constructor (god class) |
| 20 | Low | Architecture | `OpeningBalanceEntryForm.razor`, `JournalEntryPage.razor` | Hand-rolled `<table>` instead of `RadzenDataGrid` |
| 21–26 | Low | Test coverage | Finance/Agm/Events services | 6 untested guard/exception branches |
| 27–29 | Low | Docs | `CLAUDE.md`, `README.md`, `docs/ARCHITECTURE.md` | "Ten reports" stale — 11 exist |

---

## Critical

### 1. All Events list shows the wrong event type for every row (regression)
**File:** [src/StageFright.Data/Repositories/EventRepository.cs:16-28](src/StageFright.Data/Repositories/EventRepository.cs#L16-L28)

`EventRepository.GetAllAsync()` queries `_db.Events` with no `.Include(e => e.EventType)`, and the app has no lazy-loading proxies configured anywhere. `CombinedEventListService.MapEvent` (added by feature 023, `src/StageFright.Core/Modules/Events/CombinedEventListService.cs:42`) reads `e.EventType?.Name ?? "—"` — since `EventType` is never loaded, **every** row on the merged All Events grid (`EventList.razor`) now renders "—" in the Event Type column, regardless of the event's real type (Rehearsal/Concert/Social/etc.).

**Fix:** add `.Include(e => e.EventType)` to the override, or have `CombinedEventListService` call a repository method that already includes it (e.g. mirror `GetByIdWithDetailsAsync`).

### 2. Payments can be silently misrouted to "overpayment," leaving a paid fee marked outstanding forever
**File:** [src/StageFright.Core/Modules/Finance/PaymentService.cs:104](src/StageFright.Core/Modules/Finance/PaymentService.cs#L104)

The FIFO/selected-fee allocation block is gated on `outstandingBalance > 0m`. `outstandingBalance` is the member's **net** GL balance, which can be ≤ 0 even while a specific fee is still unpaid (e.g. an earlier untied overpayment credit nets the account to a credit balance). When that happens, a payment against a validly-selected outstanding fee skips the allocation loop entirely and is posted as an untied "Overpayment — cash received" credit instead of a `FeeId`-tagged credit against that fee.

**Failure scenario:** Member has a $150 untied overpayment credit, then accrues a $30 fee. Staff select that fee and record a $30 payment; validation passes (amount ≤ selected fee's remaining total), but because net balance is -120, the fee never receives its credit — `BuildOutstandingFeesAsync` (and any fee-level aging report) reports it outstanding forever even though it was paid.

**Fix:** gate the allocation loop on whether `fees.Count > 0` (i.e., there are still-unsettled fees to allocate against), not on the net GL balance.

### 3. Tax Summary report double-counts tax-exempt sales
**File:** [src/StageFright.Reports/Providers/TaxSummaryReportProvider.cs:82-101](src/StageFright.Reports/Providers/TaxSummaryReportProvider.cs#L82-L101)

`totalTaxableSales` (line 82-85) filters on `TaxCode.Taxable or TaxCode.TaxExempt`, i.e. it sums exempt income into the "taxable" bucket. That sum plus tax collected becomes `totalSales`, which is displayed as **"Total taxable sales"**, while the same exempt amount is *also* shown separately as "Total tax-exempt sales" a few rows down.

**Confirmed:** with $1000 net taxable sales (+$100 tax) and $500 tax-exempt sales, "Total taxable sales" shows $1600 (1500 + 100) instead of the correct $1100 (1000 net + 100 tax), while "Total tax-exempt sales" separately shows $500 — a user preparing a tax return from this report overstates taxable sales by the exempt amount.

**Fix:** filter `totalTaxableSales` to `TaxCode.Taxable` only.

### 4. Restoring a backup bypasses the GL double-entry balance invariant
**File:** [src/StageFright.Data/Repositories/BackupRepository.cs:55-90](src/StageFright.Data/Repositories/BackupRepository.cs#L55-L90) (`UpsertSnapshotAsync`, invoked from `BackupService.ImportAsync`)

Every other financial write path in the app wraps fee/payment/GL writes in one transaction and throws `GLBalanceException` if debits ≠ credits (per CLAUDE.md's Finance/GL integrity rule). `UpsertSnapshotAsync` blind-upserts `Fee`/`Payment`/`Transaction` rows straight from a deserialized `.sfbak` file with no re-validation of that invariant.

**Failure scenario:** A `.sfbak` file that passes schema/completeness checks but contains an unbalanced `Transaction` set (or GUID-colliding rows) is restored via Settings → Backup/Restore, silently corrupting the ledger that `outstanding = Σ(debits) − Σ(credits)` depends on everywhere else, with no error surfaced.

**Fix:** re-run the same debit/credit balance assertion used by `IGLRepository.AddPairAsync`'s callers against the full restored transaction set before committing, inside the same transaction.

### 5. Backup/restore silently drops journal entries and reconciliation history
**File:** [src/StageFright.Data/Repositories/BackupRepository.cs:24-53](src/StageFright.Data/Repositories/BackupRepository.cs#L24-L53), [src/StageFright.Core/Modules/Settings/Backup/BackupSnapshot.cs:10-29](src/StageFright.Core/Modules/Settings/Backup/BackupSnapshot.cs#L10-L29)

`BackupSnapshot`/`BackupRepository` never include `JournalEntry`, `BankReconciliation`, or `ReconciliationLine`. A full backup/restore cycle silently loses all GL journal headers and bank-reconciliation history with no warning anywhere in the flow.

**Fix:** add all three entity sets to `BackupSnapshot` and the corresponding get/upsert methods, or explicitly document the exclusion as an intentional limitation surfaced to the user before they rely on backup as a full-fidelity export.

---

## High

### 6. Plugin DLLs are loaded and executed with no integrity/trust verification
**File:** [src/StageFright.App/PluginLoader.cs:30](src/StageFright.App/PluginLoader.cs#L30)

`PluginLoader.DiscoverAndRegister` loads any `.dll` from the per-user `Plugins/` directory into an `AssemblyLoadContext` and instantiates/registers its types with no signature, hash, or publisher check, before `PluginMigrationRunner` runs the plugin's own EF Core migrations against the shared SQLite database.

**Failure scenario:** A crafted `.dll` implementing `IDataAccessProvider`/`IReportProvider` placed in `%LOCALAPPDATA%\...\Plugins\` (via malware, a malicious attachment, or a synced cloud folder) runs with the full privileges of the app process on next launch, gaining read/write access to all member PII and financial data and arbitrary code execution.

**Fix:** at minimum, require Authenticode signing and check the signer against an allow-list before loading; consider prompting the user on first load of a new/unrecognized plugin assembly.

### 7. Account Register report computes a meaningless cross-account running balance
**File:** [src/StageFright.Reports/Providers/AccountRegisterReportProvider.cs:28-65](src/StageFright.Reports/Providers/AccountRegisterReportProvider.cs#L28-L65)

The report has no account-selection filter and accumulates `runningBalance += CreditAmount - DebitAmount` across **every** GL transaction in the date range, regardless of account — despite the provider's own test-file header comment claiming an account filter exists and is tested (no such filter or test does).

**Failure scenario:** With activity in both a Cash and a bank account in the same period, the displayed "Running Balance" interleaves both accounts' movements, making the report unusable for reconciling any single account's true balance.

**Fix:** add an account filter (consistent with other filterable reports) and scope the running-balance accumulation to the selected account only.

### 8. Dashboard tile risks concurrent DbContext access
**File:** [src/StageFright.UI/Modules/Finance/OutstandingBalancesTile.razor.cs:35-39](src/StageFright.UI/Modules/Finance/OutstandingBalancesTile.razor.cs#L35-L39)

`Task.WhenAll` fires three service calls concurrently, all of which ultimately hit the same DI-scoped `StageFrightDbContext` — the exact "avoid concurrent DbContext access" gotcha already called out in `CLAUDE.md`'s Known Gotchas section for this MAUI Blazor Hybrid host.

**Failure scenario:** `InvalidOperationException: A second operation was started on this context...` (or a silent race with inconsistent results). The tile's own `catch { _error = true; }` swallows it, leaving a permanently broken tile with no diagnosable error surfaced.

**Fix:** await the three calls sequentially, or resolve a fresh scoped `DbContext`/repository set per concurrent call.

### 9. One misbehaving plugin report provider can break every report, not just its own
**File:** [src/StageFright.App/PluginLoader.cs:55-62](src/StageFright.App/PluginLoader.cs#L55-L62)

Plugin `IReportProvider` implementations are registered with `AddSingleton` unconditionally; only assembly load/type discovery is wrapped in try/catch. `ReportProviderRegistry`'s constructor takes `IEnumerable<IReportProvider>`, which the DI container eagerly constructs in full — so a plugin provider whose constructor throws (e.g. an unsatisfied dependency) breaks resolution of `ReportProviderRegistry` entirely, taking down the Reports menu for all core reports too.

**Fix:** register plugin providers via a factory that catches construction failures and logs/skips, consistent with the plugin contract's stated guarantee ("failures are caught, logged, and skipped — they never block startup").

### 10. Aging-buckets calculation ignores the GL and uses a stale flag
**File:** [src/StageFright.Data/Repositories/GLRepository.cs:191-209](src/StageFright.Data/Repositories/GLRepository.cs#L191-L209) (`GetAgingBucketsAsync`)

Buckets are computed from the immutable `Fee.PaidAtCreation` flag instead of GL-settled amounts, contradicting the codebase's GL-authoritative design and the method's own doc comment claiming consistency with other reports. A fee created unpaid (`PaidAtCreation = false`, true of all Annual fees) but later fully paid off via `PaymentService` still counts its full original amount as outstanding forever.

**Note:** currently has no production caller, so impact is latent — but it's a real bug waiting to corrupt any future aging-buckets feature built on it.

**Fix:** derive aging from GL-settled amounts per fee (the same pattern `MemberBalanceService`/`MemberAccountSummaryReportProvider` already use), not `PaidAtCreation`.

---

## Medium

### 11. `ReportViewer.razor` crashes instead of showing an error panel on a malformed report
**File:** [src/StageFright.UI/Shared/ReportViewer.razor:124](src/StageFright.UI/Shared/ReportViewer.razor#L124)

`section.SummaryRow!.Cells.Count` uses the null-forgiving operator with no runtime check. Any `IReportProvider` (including a plugin's) that sets `ReportData.SummaryColumns` but omits `SummaryRow` on one `ReportSection` throws an uncaught `NullReferenceException` inside the grid template — not caught by `ReportViewer.razor.cs`'s `try/catch`, which only wraps `GenerateAsync`, not rendering.

**Fix:** null-check `section.SummaryRow` and render an empty cell (or the "Unable to generate report" panel) instead of asserting non-null.

### 12. `ArchiveAsync`'s "already archived" guard is unreachable in the common case
**File:** [src/StageFright.Data/Repositories/SoftDeletableBaseRepository.cs:17-41](src/StageFright.Data/Repositories/SoftDeletableBaseRepository.cs#L17-L41)

`FindAsync` applies the soft-delete global query filter (`!IsDeleted`) whenever it falls back to a DB query — only a locally-tracked instance bypasses filters. So the method's own "already archived" `ValidationException` (line 25-26) only fires if the entity happens to already be tracked in that exact `DbContext` instance.

**Failure scenario:** Archive a `Member`, restart the app (or use any code path with a fresh `DbContext`), then archive the same already-archived `Member.Id` again — instead of "Member is already archived," the caller gets a misleading `EntityNotFoundException`.

**Fix:** query with `IgnoreQueryFilters()` (or a raw lookup) specifically for the already-archived check before relying on `FindAsync`.

### 13. `MemberForm` can save stale data over the wrong member on route reuse
**File:** [src/StageFright.UI/Pages/Members/MemberForm.razor.cs:23](src/StageFright.UI/Pages/Members/MemberForm.razor.cs#L23)

Unlike sibling routed detail pages (`AgmDetail`, `ReconciliationWorkspace`, `Dashboard`, etc., which deliberately use `OnParametersSetAsync`), `MemberForm` populates `_form` from the loaded `Member` in `OnInitializedAsync`, which only runs once per component instance. The component backs both `/members/new` and `/members/edit/{Id:guid}`.

**Failure scenario:** if the Blazor router reuses the component instance across two different `/members/edit/{id}` navigations (reachable via WebView2 back/forward), `_form` keeps Member A's stale field values displayed and editable while `Id` has already moved to Member B — saving would overwrite Member B's record with Member A's data.

**Fix:** move the load/populate logic to `OnParametersSetAsync`, matching the rest of the routed detail pages.

### 14. `EventDetail` shows a stale event on route reuse (read-only)
**File:** [src/StageFright.UI/Pages/Events/EventDetail.razor.cs:25](src/StageFright.UI/Pages/Events/EventDetail.razor.cs#L25)

Same root cause as #13 (`OnInitializedAsync` instead of `OnParametersSetAsync`), but read-only impact: a reused instance across two `/events/{id}` navigations displays the first event's data while the URL has moved to the second.

**Fix:** same as #13.

### 15. Rehearsal list hides every rehearsal from a prior calendar year
**File:** [src/StageFright.UI/Pages/Rehearsals/RehearsalList.razor.cs:41-54](src/StageFright.UI/Pages/Rehearsals/RehearsalList.razor.cs#L41-L54)

`OnInitializedAsync` filters past rehearsals with `r.Date.Year == today.Year`. There is no separate history/archive route for rehearsals.

**Failure scenario:** on Jan 2, a rehearsal recorded Dec 28 of the previous year (with attendance already taken) disappears from `/rehearsals` entirely, even though the record and its attendance roll still exist — the coordinator can no longer find or print it through the UI.

**Fix:** either drop the year filter (show all past rehearsals, paginated) or add an explicit year/date-range selector rather than hardcoding "this calendar year."

### 16. Raw database exceptions leak across the DAL boundary in several repositories
**Files:** `FeeRepository.cs:18-21,37-76`, `PaymentRepository.cs` (`GetByIdAsync`/`GetByMemberAsync`), `JournalEntryRepository.cs` (`GetByIdAsync`/`AnyOfTypeAsync`), `AccountRepository.cs` (`IsReferencedByTransactionsAsync`)

These read methods call EF Core directly with no try/catch, so a raw `SqliteException`/`DbException` (e.g. a locked database) propagates straight out of the DAL instead of the project-mandated `DataAccessException`, violating CLAUDE.md's "Custom exceptions at every boundary" rule. (Contrast with `EventRepository.GetAllAsync`, shown above, which does this correctly.)

**Fix:** wrap each method body in the same try/catch → `DataAccessException` pattern already used elsewhere in the same repositories (e.g. `EventRepository.GetAllAsync`).

### 17. N+1 query pattern on the Dashboard's outstanding-balances calculation
**File:** [src/StageFright.Core/Modules/Finance/MemberBalanceService.cs:34-97](src/StageFright.Core/Modules/Finance/MemberBalanceService.cs#L34-L97)

`GetAllMemberBalancesAsync` loops per member (2 queries each) then, per unpaid fee of that member, calls `GLRepository.GetByFeeAsync` again — roughly 200 + 200×3 ≈ 800+ SQL round-trips for a 200-member club with 2-3 fees each, every time the Dashboard renders.

**Fix:** batch-load GL transactions for all relevant fees in one query (e.g. `WHERE FeeId IN (...)`) and group in memory instead of querying per fee.

---

## Low — Architecture / Convention Violations

### 18. Business/GL logic lives in `StageFright.App` instead of `StageFright.Core`
**File:** [src/StageFright.App/Seeding/DebugDataSeeder.cs:29](src/StageFright.App/Seeding/DebugDataSeeder.cs#L29)

CLAUDE.md states `StageFright.App` is "composition root only... zero application logic." This 964-line class builds GL `Transaction`/`JournalEntry` entities directly and posts balanced GL sets (e.g. `CreateAnnualFeeAccrualAsync`), with its own doc comment admitting it duplicates `FeeService`'s GL-posting pattern rather than reusing it — any future change to fee-accrual GL rules has to be updated in two places or seed data silently drifts from real behavior.

**Fix:** move the GL-construction logic into `StageFright.Core/Modules/Finance` (or reuse `FeeService` directly) and have the seeder call into it.

### 19. `DebugDataSeeder` is a 20-dependency god class
**File:** [src/StageFright.App/Seeding/DebugDataSeeder.cs:63](src/StageFright.App/Seeding/DebugDataSeeder.cs#L63)

The constructor injects 11 services, 3 repositories, plus `UnitOfWork`/logger — coupling it to nearly every module in the app and making it untestable in isolation.

**Fix:** split into per-domain seed helpers (members, finance, events/AGM) composed by a thin orchestrator.

### 20. Two hand-rolled Bootstrap tables violate the RadzenDataGrid standard
**Files:** [src/StageFright.UI/Shared/OpeningBalanceEntryForm.razor:18-52](src/StageFright.UI/Shared/OpeningBalanceEntryForm.razor#L18-L52), [src/StageFright.UI/Pages/Finance/JournalEntryPage.razor:49-101](src/StageFright.UI/Pages/Finance/JournalEntryPage.razor#L49-L101)

CLAUDE.md's Data Grid standard is explicit: "never a plain `<table>` markup or a `table-responsive` wrapper div," naming `ReportViewer.razor` as the sole exception. Both of these render editable line-item tables as raw Bootstrap markup instead.

**Fix:** adopt `RadzenDataGrid`'s inline-edit template once and reuse it for both screens, bringing them in line with every other grid in the app.

---

## Low — Test Coverage Gaps

Six untested guard/exception branches, each a safety-critical validation path with no regression protection:

| # | File | Method | Untested branch |
|---|------|--------|------------------|
| 21 | [FeeService.cs:62](src/StageFright.Core/Modules/Finance/FeeService.cs#L62) | `ApplyAnnualFeesAsync` | `ValidationException` when settings are missing |
| 22 | [FeeService.cs:67](src/StageFright.Core/Modules/Finance/FeeService.cs#L67) | `ApplyAnnualFeesAsync` | `ValidationException` when no non-system Income account exists |
| 23 | [AgmService.cs:204](src/StageFright.Core/Modules/Agm/AgmService.cs#L204) | `RecordSpecialElectionAsync` | `DataIntegrityException` when outgoing record's `CommitteeTermId` is null |
| 24 | [CombinedEventListService.cs:42](src/StageFright.Core/Modules/Events/CombinedEventListService.cs#L42) | `MapEvent` | `EventType` null fallback ("—") — this is the exact code path broken by finding #1 |
| 25 | [ReactivationForgivenessService.cs:75](src/StageFright.Core/Modules/Finance/ReactivationForgivenessService.cs#L75) | `ApplyForgivenessAsync` | Silent `continue` on a `feeId` not belonging to the member |
| 26 | [EventService.cs:75](src/StageFright.Core/Modules/Events/EventService.cs#L75) | `RecordParticipationAsync` | `EntityNotFoundException` when `eventId` doesn't resolve |

Each has a sibling branch in the same method that *is* tested, so these are gaps in an otherwise-covered test file, not missing test files outright.

---

## Low — Documentation Staleness

All three stem from one root cause: spec 020 added `ChartOfAccountsReportProvider` (registered in `MauiProgram.cs:256` alongside the other 10) but the "ten reports" count was never updated anywhere it's repeated.

| # | File | Line |
|---|------|------|
| 27 | [CLAUDE.md:112](CLAUDE.md#L112) | "All ten reports... follow this single pipeline" — omits Chart of Accounts |
| 28 | [README.md:238](README.md#L238) | "Key Features" claims "Ten built-in reports" |
| 29 | [docs/ARCHITECTURE.md:176](docs/ARCHITECTURE.md#L176) | "All ten current reports follow this single pipeline," same enumeration |

**Fix:** update all three to list 11 reports, adding `ChartOfAccountsReportProvider` to each enumeration.

All other checked claims (20 entities, 8 modules, 10 custom exception types, soft-delete exemption list, Settings tab names, plugin extension-point count, constitution version) matched the current code exactly — no other staleness found. Spec `023-merge-agms-events` itself matches its implementation precisely at the spec-doc level (the discrepancy is a code bug — finding #1 — not a doc/spec mismatch).

---

## What checked out clean

- One class per file — verified across the repo (only private nested types share a file).
- No custom JavaScript — only vendor Bootstrap/Chart.js bundles exist.
- Blazor code-behind convention — every `.razor` has a paired `.razor.cs` except the framework-standard `_Imports.razor`.
- Centralized NuGet package versions — no `.csproj` pins a `Version=` attribute directly.
- Repositories live centrally — no repository classes exist under `StageFright.Core/Modules/`.
- No hardcoded secrets, SQL injection, path traversal, insecure crypto, or PII-in-logs found in the security pass; the only `ExecuteSqlRawAsync` calls are test-only and parameterized; temp-file paths use `Guid.NewGuid()` names.

---

## Suggested priority order

1. Fix #1 (All Events type regression) — cheapest fix, currently visibly broken in the UI you just shipped.
2. Fix #2 and #3 (Payment misrouting, Tax Summary double-count) — silent financial-data correctness bugs.
3. Fix #4 and #5 (Backup/restore integrity) — currently the only safety net if the primary DB is lost, and it's silently lossy/unsafe.
4. Triage #6 (plugin signing) as a product decision — may be acceptable risk for a single-user desktop app, but should be a conscious call, not an oversight.
5. Everything else can follow as normal backlog cleanup.
