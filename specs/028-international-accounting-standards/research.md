# Phase 0 Research: International accounting-practice readiness

Ten decisions resolve the design choices the spec leaves open. Each records what was chosen, why, and
what was rejected. There are no remaining `NEEDS CLARIFICATION` items in the spec.

---

## Decision 1 — Currency formatting is a process-wide configured static, not an injected service

**Decision**: Keep `MoneyFormatter` a static class with its existing `Format` / `FormatWithCode` call
surface, and add `MoneyFormatter.Configure(SupportedCurrency)` called once during startup in
`MauiProgram` — immediately after the display culture is resolved and applied. Formatting reads the
configured currency from an immutable static field.

**Rationale**:
* The organisation currency is fixed for the life of the dataset (FR-002) — a single currency per
  process is a correct model, so process-wide state is not a hazard here.
* It exactly mirrors the pattern already in `MauiProgram`, where the resolved culture is pushed onto
  `CultureInfo.DefaultThreadCurrentCulture` / `CurrentCulture` before the `BlazorWebView` renders.
* `MoneyFormatter` is consumed at ~20 call sites in `StageFright.UI` and in `BankReconciliationService`,
  plus the `StageFright.Reports` providers (which are constructed by DI but format money in private
  helpers). Converting all of them to inject an `IMoneyFormatter` is a large, churny change for no
  behavioural gain over configuring the existing helper.
* `CultureProvider` (spec 027) already exists as the seam for a future in-session live switch; a later
  story can add currency re-configuration there without touching call sites.

**Alternatives considered**:
* *Injected `IMoneyFormatter` everywhere* — rejected: ~20+ call-site edits, static report-provider
  helpers need refactoring, and it buys nothing while the currency cannot change mid-session.
* *Pass the currency into every `Format` call* — rejected: every call site would need the current
  `Settings`, defeating the point of a formatting helper.

---

## Decision 2 — ISO 4217 metadata ships as an internal curated catalog, no NuGet dependency

**Decision**: Add `SupportedCurrency` (`Code`, `Symbol`, `MinorUnitDigits`, `DisplayName`) and a
static `CurrencyCatalog` holding a curated list of supported currencies with a `TryGet(code)` /
`Get(code)` lookup and an `All` enumeration. It covers a representative shipped set — `AUD`, `USD`,
`EUR`, `GBP`, `NZD`, `CAD`, `JPY` (0 minor digits), `KWD` and `BHD` (3 minor digits) — and is
extended by adding a row.

**Rationale**:
* .NET has no built-in ISO-4217-keyed lookup of symbol + minor-unit digits.
  `NumberFormatInfo.CurrencyDecimalDigits` and `RegionInfo.ISOCurrencySymbol` are keyed by *culture*,
  not by currency code, and give the wrong answer when the display region differs from the money
  currency (the whole problem this feature fixes).
* The catalog directly parallels `SupportedLanguagesCatalog` from spec 027 — the same "add a row, no
  code change elsewhere" extensibility, the same testing shape.
* Keeping it internal honours Constitution §7.1 (no new dependency) and `Directory.Packages.props`
  discipline.
* Zero- and three-decimal currencies in the seed set force the rounding and display paths (FR-005,
  Edge Cases) to be exercised, not just the 2-decimal default.

**Alternatives considered**:
* *`NodaMoney` / `NodaTime`-adjacent money library* — rejected: a dependency for a lookup table; the
  supported currency set is small and organisation-scoped.
* *Full ISO 4217 table (~180 entries)* — rejected: most are untested and unshippable; a curated set
  with an explicit extension path is honest about what is actually supported.

---

## Decision 3 — Money entry keeps `<input type="number">` and parses the value as invariant

**Decision**: The two hand-rolled money parsers (`JournalEntryPage.ParseAmount`,
`OpeningBalanceEntryForm.SetAmount`) switch to a shared `MoneyInput.Parse` that uses
`CultureInfo.InvariantCulture` with `NumberStyles.AllowDecimalPoint | AllowLeadingSign`. The inputs
stay `type="number"`.

**Rationale**:
* An HTML `<input type="number">` always exposes its `.value` as an invariant-format string
  (`"1.5"`), regardless of the page locale — this is the DOM/WHATWG behaviour and is why Blazor's own
  `@bind` on a numeric input binds through the invariant culture. The current bug is parsing that
  invariant string with `CultureInfo.CurrentCulture`: under `fr-FR`/`de-DE` the `.` is treated as a
  group separator and `1.50` becomes `150`.
* Parsing invariant makes the two forms interpret a typed amount identically to every other money
  field in the app (FR-008) and removes the period-as-grouping ambiguity entirely (FR-009) — there
  is no grouping separator to misread because the browser never emits one.
* The browser still renders the locale-appropriate numeric keyboard and digit grouping in the field
  UI, so a user in a comma-decimal region still gets a native, familiar entry experience; only the
  serialised value the code parses is invariant.

**Alternatives considered**:
* *Switch to `type="text"` + `CultureInfo.CurrentCulture` parse* — rejected: reintroduces the
  thousands-separator-versus-decimal ambiguity FR-009 explicitly forbids, loses the native numeric
  keypad, and diverges from how the rest of the app's `@bind` money fields already behave.
* *Custom locale-aware parser that strips grouping* — rejected: guessing whether `1.000` means one or
  one-thousand is exactly the failure mode the spec calls out; unnecessary once the input is
  invariant.

---

## Decision 4 — Period locking is enforced at the GL choke point, via a paired guard abstraction

**Decision**: `GLRepository.AddBalancedSetAsync` and `AddPairAsync` call a new `IClosedPeriodGuard`
for each posting line's `Date` before `SaveChangesAsync`, throwing `ClosedPeriodException` when the
date is on or before `Settings.ClosedThroughDate`. `ClosedPeriodGuard` reads the `Settings` singleton.

**Rationale**:
* Every financial mutation in the system — fee accrual, payment, expense, income, bank deposit,
  manual journal, opening balance, reactivation write-off — funnels through
  `AddBalancedSetAsync`/`AddPairAsync` (confirmed against the finance living spec and the service
  code). Guarding there gives FR-017's "any attempt … MUST be rejected" for free, with no risk of a
  new posting path being added later that forgets the check.
* It runs inside `UnitOfWork.ExecuteInTransactionAsync`, so a rejection rolls back the whole
  operation — no business row and no ledger line survives (FR-017's "no partial record").
* `GLRepository` already enforces a business invariant (Σdebits = Σcredits) pre-persistence and
  already translates framework exceptions; adding one more pre-persistence guard at the same seam is
  consistent with its established role, and the `IClosedPeriodGuard` abstraction keeps the rule
  itself testable in isolation and out of the repository's own logic.

**Alternatives considered**:
* *Check in each posting service* — rejected: seven-plus services, easy to miss one, and a future
  service would silently bypass the lock; contradicts "any attempt".
* *EF Core `SaveChanges` interceptor* — rejected: harder to test, obscures the rule, and would need
  to reconstruct which entity a `Transaction` belongs to for a good error message.
* *A carve-out for the setup wizard's opening balances* — rejected as unnecessary: setup completes
  before any period can be closed, so `ClosedThroughDate` is null during setup (FR-018 is satisfied
  without special-casing). The reasoning is recorded in the contract; the ambient-scope pattern from
  `AuditTrailSuppressionScope` is noted as the template if a bypass is ever genuinely needed.

---

## Decision 5 — Balance Sheet renders an explicit out-of-balance line; Trial Balance throws

**Decision**: `TrialBalanceReportProvider` keeps throwing `GLBalanceException` (the viewer already
turns it into a friendly "Try Again"), but with the `0.01` tolerance removed — any non-zero
difference is an error (FR-011). `BalanceSheetReportProvider` instead appends a bold, explicit
"Out of balance by <amount>" row whenever `totalAssets ≠ totalLiabilities + totalEquity`, so a clean
statement is never produced (FR-010).

**Rationale**:
* FR-010 permits *either* failing generation *or* an explicit out-of-balance line. The Balance Sheet
  balances by construction today (accumulated surplus is derived as net income), so a non-zero
  difference means a genuine data-integrity fault where the *magnitude* is the useful diagnostic —
  surfacing the number beats hiding it behind a generic retry.
* The Trial Balance already has the throw-and-retry pattern wired through the viewer; removing the
  tolerance is a one-line change plus message/localization updates, and "no tolerance band" is
  explicit in FR-011.
* Both outcomes are consistent with the reports pipeline's rule that a generation failure is caught
  centrally and is always retryable, never fatal.

**Alternatives considered**:
* *Balance Sheet also throws* — rejected: loses the out-of-balance amount, which is the one piece of
  information a reviewer needs.
* *Trial Balance renders a line instead of throwing* — rejected: it would diverge from the existing,
  tested viewer behaviour for that report and the spec's "treated as an error" wording.

---

## Decision 6 — Basis of accounting is one optional `ReportData` field, set by financial-statement providers

**Decision**: Add `ReportData.BasisOfAccounting` (`string?`, optional). `PdfReportRenderer`,
`CsvReportExporter` and `ReportViewer.razor` render it when present. The financial-statement providers
(Income Statement, Balance Sheet, Trial Balance, Tax Summary, Account Register, General Ledger, Bank
Reconciliation, Member Account Summary) set it from a single shared localized string.

**Rationale**:
* The pipeline's whole design is "one renderer-agnostic data model consumed by every renderer without
  provider-specific branching" — a new optional field is the idiomatic extension, and all three
  consumers pick it up once.
* One shared string keeps the wording consistent and correct across statements and gives translators a
  single key. The wording must describe the *hybrid* basis (member fees on accrual; other activity on
  cash) — FR-012 explicitly forbids claiming a single blanket basis.
* Non-financial reports (Member List, Committee) simply leave the field null.

**Alternatives considered**:
* *Each provider adds a "Basis of accounting" section row* — rejected: repeats the text in every
  provider, pollutes the data table, and CSV/column alignment gets awkward.
* *Renderer hard-codes the basis line* — rejected: the renderer must not know which reports are
  financial statements, and it could not be suppressed for Member List / Committee.

---

## Decision 7 — The audit-retention default change is a column-default-only migration

**Decision**: The migration that raises the `AuditRetentionYears` default from `1` to `5` alters only
the column's default constraint. It issues **no** `UpdateData` against the existing `Settings` row.

**Rationale**:
* FR-024 requires an existing dataset's already-configured retention value to be preserved. Since
  there is exactly one `Settings` row and it always has a value, touching only the schema default
  changes behaviour for *new* datasets (FR-023) while leaving the existing row exactly as the
  organisation set it.
* The 1–7 range and the field's user-configurability are unchanged (spec Assumptions).

**Alternatives considered**:
* *`UpdateData` to set existing rows to 5* — rejected: violates FR-024 for any organisation that
  deliberately chose a shorter retention.
* *Make the field nullable with a computed default* — rejected: unnecessary schema churn; the column
  is never null in practice.

---

## Decision 8 — A failed startup purge is recorded into the existing startup-diagnostic state

**Decision**: `MauiProgram` continues to catch and `Log.Error` a failed audit-retention purge, and
additionally records the failure into the same retrievable startup-diagnostic state the app already
uses for a failed database initialization, so the UI can surface it (FR-025).

**Rationale**:
* The Settings capability already has a "startup failures are captured for diagnostic display rather
  than failing silently" mechanism (database-init failure + expected DB location are retained for the
  UI). A purge failure is the same class of event — reusing that holder means one place the UI reads,
  no new plumbing, and startup still proceeds unaffected.
* FR-025 asks for "logged **and** surfaced" — logging alone (today's behaviour) is not enough.

**Alternatives considered**:
* *New dedicated purge-failure banner service* — rejected: duplicates an existing mechanism.
* *Block startup on purge failure* — rejected: contradicts the audit-trail capability's rule that a
  purge failure never blocks startup.

---

## Decision 9 — A sub-twelve-month first financial year (FR-022) is deferred to a follow-on issue

**Decision**: FR-022 (support a first financial year shorter than twelve months, labelled a
part-year) is **not built in this feature**. It is captured as a follow-on GitHub issue.

**Rationale**:
* The spec Assumptions explicitly permit this: "A short first financial year (FR-022) is desirable
  but optional for this feature. If it is not built here it MUST be captured as a follow-on issue
  rather than dropped."
* The core internationalisation value (US1/US2 correctness, US7's non-first-of-month start) lands
  without it. A part-year needs its own model decisions (how the stub period is stored, how every
  FY-preset report and dashboard figure labels and bounds it) that would widen this already-large
  feature.

**Alternatives considered**:
* *Build a minimal stub-year now* — rejected: "minimal" here still touches every FY-preset report and
  the dashboard, with label and boundary semantics to design; better as a focused follow-up.

---

## Decision 10 — The bank-reconciliation "book balance" comes from `GetAccountBalanceAsync`

**Decision**: The rewritten `BankReconciliationReportProvider` sources the "balance per general
ledger" from the existing `IGLRepository.GetAccountBalanceAsync(accountId, statementDate)` and the
outstanding items from the existing `IGLRepository.GetUnreconciledByAccountAsync(accountId,
statementDate)`.

**Rationale**:
* Both methods already exist and are already keyed on `AccountId` (not the denormalized `GLAccount`
  string), so the reconciliation report inherits the pipeline's "aggregate by account identity" rule
  for free.
* No new repository method is needed; the change is confined to how the provider composes the
  adjusted-balance sections (FR-013/FR-014).

**Alternatives considered**:
* *Compute the ledger balance in the provider from raw transactions* — rejected: re-implements an
  existing, tested query and risks drifting from how balances are computed elsewhere.
