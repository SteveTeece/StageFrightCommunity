# Feature Specification: International accounting-practice readiness

**Feature Branch**: `028-international-accounting-standards`

**Created**: 2026-08-29

**Status**: Draft

**Input**: GitHub issue #341 — "Ready the accounting practices for international distribution" — and its
eleven linked sub-issues #342–#352. User description: "Create a spec for issue #341. Include the
sub-issues in the spec. Do not proceed to plan or commit."

## Context

The finance module is a sound double-entry system, but it was built for a single Australian
organisation: the currency is hard-coded to Australian dollars, the financial year defaults to the
Australian convention, money entry breaks under comma-decimal regional formats, and several statements
lack the integrity checks and disclosures an outside reviewer expects. The goal is to make the
accounting practices safe and portable for community / amateur-theatre groups **outside Australia**,
judged against universal double-entry and bookkeeping good practice — not formal IFRS/GAAP compliance,
which remains each organisation's own accountant's responsibility.

Each user story below maps to one or more of the tracked sub-issues.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Run the books in the organisation's own currency (Priority: P1)

*(Sub-issue #342 — gap G1, blocker)*

A treasurer for a theatre group in another country installs the app. During first-run setup they
choose their currency. From then on every amount on every screen, report, PDF and CSV is shown in that
currency, with the right symbol, the right number of decimal places, and digit grouping that matches
their region. Nothing anywhere asserts Australian dollars.

**Why this priority**: Without this the product is unusable outside Australia — every displayed and
printed figure misrepresents the amount. It is the single blocking gap.

**Independent Test**: Complete setup selecting a non-Australian currency (including a zero-decimal one
such as Japanese yen), record a fee and a payment, and generate each financial report; confirm every
amount uses the chosen currency and decimal precision and no `$` / `AUD` appears.

**Acceptance Scenarios**:

1. **Given** a fresh install, **When** the treasurer completes setup and picks a currency, **Then**
   that currency is stored against the organisation and used for all money display thereafter.
2. **Given** an organisation configured with a zero-decimal currency, **When** any amount is
   displayed, **Then** it shows no fractional digits and still reconciles exactly.
3. **Given** an organisation configured with a comma-grouping region, **When** an amount over one
   thousand is displayed, **Then** grouping and symbol placement follow that region while the currency
   symbol stays the configured one.
4. **Given** an existing Australian-dollar dataset, **When** it is opened after this change, **Then**
   every amount still shows `$`, two decimal places, and the same stored values as before.

---

### User Story 2 - Enter amounts correctly in any regional number format (Priority: P1)

*(Sub-issue #343 — gap G2, live bug)*

A treasurer whose device is set to a region that uses a comma as the decimal separator enters amounts
into the manual journal and the opening-balance screen. The amount that is stored is exactly what they
typed — never scaled up or down.

**Why this priority**: This is a live data-corruption bug. A French-locale user shipping today can
enter `1.50` in a journal line and have `150` posted to the ledger. It must be fixed for any
non-English rollout.

**Independent Test**: With the device region set to French and to German, enter known amounts into the
manual journal and opening-balance forms and assert the stored ledger values are exact.

**Acceptance Scenarios**:

1. **Given** a French-locale device, **When** the user enters an amount into a manual journal line,
   **Then** the stored debit/credit equals the entered value to the cent.
2. **Given** any shipped region, **When** the user enters an amount into the opening-balance form,
   **Then** it is interpreted identically to the same input in every other money field in the app.
3. **Given** a region that groups with periods, **When** the user enters a plain decimal amount,
   **Then** no digit is treated as a thousands separator.

---

### User Story 3 - Trust that the statements are internally consistent (Priority: P2)

*(Sub-issues #344 gap G3, #348 gap G7)*

A treasurer or their accountant generates the Balance Sheet and the Trial Balance and can rely on the
software to refuse to produce a statement that does not add up.

**Why this priority**: These checks turn silent misstatement into a visible failure. They are small,
high-confidence safeguards on the two statements an external reviewer scrutinises first.

**Independent Test**: Generate the Balance Sheet and Trial Balance from a balanced ledger (both tie)
and from a deliberately corrupted ledger (both refuse or flag the discrepancy).

**Acceptance Scenarios**:

1. **Given** a balanced ledger, **When** the Balance Sheet is generated, **Then** total assets equal
   total liabilities plus equity and the statement is produced normally.
2. **Given** a ledger where assets no longer equal liabilities plus equity, **When** the Balance Sheet
   is generated, **Then** it fails or shows an explicit out-of-balance line rather than a clean
   statement.
3. **Given** a ledger whose total debits and total credits differ by one cent, **When** the Trial
   Balance is generated, **Then** it is treated as an error (no tolerance band).

---

### User Story 4 - Know the basis of accounting each statement uses (Priority: P2)

*(Sub-issue #345 — gap G4)*

Anyone reading a financial statement can see, on the statement itself, what basis of accounting it is
prepared on — including that member fees are recognised when levied while other income and expenditure
are recorded on payment.

**Why this priority**: A statement whose basis is unstated cannot be relied on by a reader who did not
build the system. Cheap to add; materially improves trust.

**Independent Test**: Generate the Income Statement, Balance Sheet and Tax Summary and confirm each
carries an accurate basis-of-accounting statement.

**Acceptance Scenarios**:

1. **Given** any financial statement, **When** it is generated, **Then** it displays a
   basis-of-accounting line.
2. **Given** the current hybrid treatment, **When** the basis line is read, **Then** it accurately
   describes both the accrual treatment of member fees and the cash treatment of other activity — it
   does not claim a single blanket basis.

---

### User Story 5 - Read a conventional bank reconciliation (Priority: P2)

*(Sub-issue #351 — gap G11)*

A treasurer prints the bank reconciliation and it follows the layout a bookkeeper or auditor expects:
balance per the bank statement, adjusted for outstanding deposits and outstanding payments, reconciled
to the balance per the general ledger at the statement date.

**Why this priority**: The current report proves the reconciliation a non-standard way and never shows
the ledger balance, so an external reviewer cannot follow it. Presentation-only, hence P2.

**Independent Test**: Finalise a reconciliation with known outstanding items and confirm the report
shows both balances, the adjusting items, and that the two sides agree.

**Acceptance Scenarios**:

1. **Given** a reconciliation with outstanding deposits and payments, **When** the report is
   generated, **Then** it shows "balance per bank statement", each adjusting item, an adjusted bank
   balance, and "balance per general ledger", and demonstrates they are equal.
2. **Given** outstanding items exist, **When** the reconciliation is computed, **Then** those items
   are carried into the arithmetic, not merely listed for information.
3. **Given** a finalised reconciliation, **When** it is viewed later, **Then** it is unchanged and
   cannot be edited, and finalisation still required the reconciliation to balance.

---

### User Story 6 - Protect reported prior years from back-dated changes (Priority: P3)

*(Sub-issue #346 — gap G5)*

After the committee has been shown a year's accounts, the treasurer closes that period. From then on
the software refuses any transaction dated into the closed period, so the reported result cannot
silently change.

**Why this priority**: Valuable governance control, but it depends on the organisation having a
reporting cycle established, so it follows the P1/P2 correctness work.

**Independent Test**: Set a closed-through date, then attempt to post fees, payments, expenses and
journals dated before and after it; confirm the earlier ones are rejected with no partial record and
the later ones succeed.

**Acceptance Scenarios**:

1. **Given** a closed-through date is set, **When** any financial transaction dated on or before it is
   submitted, **Then** it is rejected and no record (business row or ledger line) is persisted.
2. **Given** a closed-through date is set, **When** a transaction dated after it is submitted, **Then**
   it posts normally.
3. **Given** an organisation is still in first-run setup, **When** opening balances are entered,
   **Then** they are accepted (setup precedes the first close).

---

### User Story 7 - Choose the financial-year start as a real setup decision (Priority: P3)

*(Sub-issue #352 — gap G9)*

During setup the treasurer is asked when their financial year starts and can pick a start that is not
the first of a month. Every financial-year report and dashboard figure then uses that start.

**Why this priority**: Most jurisdictions are served by choosing a start month, which is already
possible in settings; the gap is that setup never asks and non-first-of-month starts are impossible.
Improvement rather than a blocker.

**Independent Test**: Complete setup choosing a non-first-of-month start, then generate the
financial-year-preset reports and confirm the ranges match the chosen start.

**Acceptance Scenarios**:

1. **Given** first-run setup, **When** the treasurer reaches the financial-year step, **Then** they
   must confirm or choose the start; there is no silent Australian default.
2. **Given** a financial year configured to start on a day other than the first, **When** any
   financial-year-preset report is generated, **Then** its date range begins on that day and ends the
   day before, twelve months on.
3. **Given** an existing Australian dataset (start month July, day 1), **When** it is opened after this
   change, **Then** its report ranges are unchanged.

---

### User Story 8 - Retain financial audit history for a defensible period (Priority: P3)

*(Sub-issue #347 — gap G6)*

A new organisation's financial audit trail is retained for a period consistent with common
record-keeping expectations, a failed purge is never silent, and every posting path — including
attendance-fee accruals — leaves an audit-trail entry.

**Why this priority**: The immutable ledger is the primary record, so this is a supporting-records
improvement rather than a correctness fix.

**Independent Test**: Check the default retention on a fresh dataset, force a purge failure and
confirm it is surfaced, and record attendance that accrues a fee and confirm an audit entry is
written.

**Acceptance Scenarios**:

1. **Given** a fresh dataset, **When** the audit-retention default is read, **Then** it is at least
   five years, and it remains user-adjustable.
2. **Given** an existing dataset with a configured retention value, **When** it is opened after this
   change, **Then** its configured value is preserved.
3. **Given** the expired-entry purge fails, **When** the app starts, **Then** the failure is logged
   and visible, not swallowed.
4. **Given** attendance is recorded that accrues a fee (paid or unpaid), **When** the accrual posts,
   **Then** an audit-trail entry is written for it.

---

### User Story 9 - Have the accounting policies written down (Priority: P3)

*(Sub-issue #349 — gap G8)*

A treasurer, auditor or contributor can read a single document that states the app's accounting
policies, and the finance capability's living specification reflects the current tax model rather than
the retired one.

**Why this priority**: Documentation; it depends on the other stories' decisions being settled, so it
comes last among the substantive stories.

**Independent Test**: Open the published accounting-policy document and verify each statement against
observed system behaviour; confirm the finance living spec no longer references retired tax concepts
and is no longer marked draft.

**Acceptance Scenarios**:

1. **Given** the repository, **When** the accounting-policy document is opened, **Then** it covers
   basis of accounting, revenue recognition, rounding, currency, record immutability and correction
   method, and audit-trail retention, and states that the reports are unaudited management accounts.
2. **Given** the finance capability's living specification, **When** it is read, **Then** it contains
   no reference to the retired registration-based tax model and is not marked draft.

---

### User Story 10 - Get a clear plan for internationalising sales tax (Priority: P3)

*(Sub-issue #350 — gap G10, spike)*

A maintainer receives a written assessment of what it would take to use the sales-tax feature outside
its current single-jurisdiction assumptions, with a scoped decision for each point.

**Why this priority**: Full multi-jurisdiction tax is a separate body of work; this feature only needs
to scope it, so it is a spike, not an implementation.

**Independent Test**: Read the assessment and confirm it records an in-scope / out-of-scope decision,
with rough sizing, for each required point, and that follow-on issues exist for whatever is taken
forward.

**Acceptance Scenarios**:

1. **Given** the assessment, **When** it is read, **Then** it addresses rate changes over time,
   tax-exclusive entry, the balance-sheet classification of recoverable tax (accounts `2310` / `2320`),
   and whether multiple simultaneous rates or jurisdictions are needed.
2. **Given** the assessment, **When** a decision is recorded for a point, **Then** it states in scope
   or out of scope with a rough size, and in-scope points have follow-on issues.
3. **Given** this feature, **When** it is delivered, **Then** no existing tax posting or stored tax
   amount has changed.

## Edge Cases

- **Zero-decimal and three-decimal currencies** — amounts, tax splits and rounding must be correct
  for currencies with 0 minor digits (e.g. yen) and 3 (e.g. dinar), not just 2.
- **Existing Australian datasets** — every story that changes behaviour must leave a pre-existing
  `AUD` dataset producing identical figures and identical stored values.
- **Region change after setup** — changing the device/display region must alter only grouping and
  symbol placement, never the currency or any stored amount.
- **Comma-decimal input with grouping** — an entered value like one-thousand-and-a-half must not be
  misread whether the user types a grouping separator or not.
- **Closed period boundary** — a transaction dated exactly on the closed-through date is inside the
  closed period and is rejected.
- **Reconciliation with no outstanding items** — the conventional report must still show both
  balances and prove they agree.
- **Attendance accrual that is immediately paid** — both the accrual and the automatic payment must
  each be represented in the audit trail.
- **Short first financial year** — an organisation founded mid-year (an optional inception date after
  the financial-year anchor): the first period runs from the inception date to the day before the
  next anchor and every financial-year-preset report labels it a part-year; later years are full
  twelve-month periods. An inception date on the anchor, or none at all, gives a full twelve-month
  first year with no label.

## Requirements

### Functional Requirements

**Currency (US1)**

- **FR-001**: First-run setup MUST require the organisation to select its currency, identified by an
  ISO 4217 code, with `AUD` as the default selection.
- **FR-002**: The selected currency MUST be fixed for the life of the dataset — no in-place currency
  change, no simultaneous multiple currencies, and no foreign-exchange translation.
- **FR-003**: Every monetary amount displayed, printed, or exported MUST use the configured currency's
  symbol and its standard minor-unit precision (0, 2, or 3 fractional digits), with digit grouping and
  symbol placement following the active regional format.
- **FR-004**: No screen, report, PDF, or CSV MUST display a currency symbol or currency code that does
  not match the configured currency.
- **FR-005**: Any amount that is apportioned or split (for example a tax component) MUST round to the
  configured currency's minor unit, and the parts MUST still re-sum to the original total exactly.
- **FR-006**: An existing `AUD` dataset MUST, after this change, display the same symbol, the same
  precision, and the same stored values as before.

**Money entry (US2)**

- **FR-007**: Entering a monetary amount MUST store the exact value the user intended, independent of
  the active regional number format.
- **FR-008**: The manual journal and opening-balance entry screens MUST interpret a typed amount
  identically to every other monetary input in the application.
- **FR-009**: A regional format that uses a comma as the decimal separator or a period as a grouping
  separator MUST NOT cause an entered amount to be scaled or misread.

**Statement integrity (US3)**

- **FR-010**: Generating the Balance Sheet MUST verify that total assets equal total liabilities plus
  equity and MUST NOT present a clean statement when they do not — it either fails generation or
  renders an explicit out-of-balance line.
- **FR-011**: Generating the Trial Balance MUST treat any non-zero difference between total debits and
  total credits as an error, with no tolerance band.

**Basis of accounting (US4)**

- **FR-012**: Each financial statement MUST state, on its face, the basis of accounting it is prepared
  on, described accurately — including where the treatment of member fees differs from that of other
  income and expenditure.

**Bank reconciliation (US5)**

- **FR-013**: The bank reconciliation report MUST present the conventional adjusted-balance form:
  balance per bank statement, adjusted by outstanding deposits and outstanding payments, reconciled to
  the balance per the general ledger as at the statement date, with both balances shown.
- **FR-014**: Outstanding items MUST be carried into the reconciliation arithmetic, not only listed.
- **FR-015**: Finalising a reconciliation MUST still require it to balance, and a finalised
  reconciliation MUST remain immutable.

**Period lock (US6)**

- **FR-016**: An organisation MUST be able to mark all financial periods up to and including a chosen
  date as closed.
- **FR-017**: Any attempt to create or post a financial transaction dated on or before the
  closed-through date MUST be rejected and MUST leave no partial record — no business row and no
  ledger line.
- **FR-018**: Opening balances entered during first-run setup MUST remain permitted regardless of any
  later closed-through date.

**Financial-year start (US7)**

- **FR-019**: The financial-year start MUST be an explicit choice presented during first-run setup,
  not a silent default.
- **FR-020**: The system MUST support a financial year that starts on a day other than the first of
  the month.
- **FR-021**: All financial-year-preset reports and dashboard figures MUST honour the configured
  financial-year start (month and day).
- **FR-022**: The system supports a first financial year shorter than twelve months — an organisation
  with an optional configured inception date later than its financial-year anchor — and labels that
  first period as a part-year on every financial-year-preset report. *(Delivered on this branch as
  spec 028 Phase 14 / issue #353 — see Assumptions.)*

**Audit trail (US8)**

- **FR-023**: The default financial audit-trail retention on a new dataset MUST be at least five
  years, and MUST remain user-configurable.
- **FR-024**: An existing dataset's already-configured retention value MUST be preserved through this
  change.
- **FR-025**: A failure to purge expired audit entries MUST be logged and surfaced, never silently
  discarded.
- **FR-026**: Every financial posting path — including attendance-fee accruals and their automatic
  payments — MUST write an audit-trail entry.

**Documentation (US9)**

- **FR-027**: The project MUST publish an accounting-policy document covering basis of accounting,
  revenue recognition, rounding, currency, record immutability and correction method, and audit-trail
  retention, and stating that the reports are unaudited management accounts.
- **FR-028**: The finance capability's living specification MUST be updated to the current tax model,
  MUST contain no reference to the retired registration-based tax model, and MUST no longer be marked
  draft.

**Sales-tax internationalisation (US10)**

- **FR-029**: The project MUST produce a written assessment of what is required to use the sales-tax
  feature outside its current single-jurisdiction assumptions, covering at least: rate changes over
  time, tax-exclusive entry, the balance-sheet classification of recoverable tax, and whether multiple
  simultaneous rates or jurisdictions are needed.
- **FR-030**: The assessment MUST record an in-scope / out-of-scope decision with rough sizing for
  each point, and MUST result in follow-on issues for every in-scope point.

**Cross-cutting guards**

- **FR-031**: No change in this feature MUST alter any previously stored monetary amount, tax amount,
  or general-ledger balance; only presentation, validation, and new configuration may change.
- **FR-032**: The existing double-entry guarantees MUST be preserved: balanced atomic postings, the
  general ledger as the single source of truth for balances, immutable financial records, and
  corrections made only by reversing entries.
- **FR-033**: This feature MUST NOT change any existing tax posting mechanics or stored tax code
  values.

## Key Entities

- **Organisation configuration** — the single record of organisation-wide financial settings. Gains: a
  currency (ISO 4217 code, default `AUD`, fixed after setup); a financial-year start expressed as a
  month and a day; a closed-through date for period locking; an optional inception date driving the
  part-year first financial year (FR-022 / #353). Already holds the financial-year start month,
  audit-retention period, and tax configuration.
- **Monetary amount** — a value expressed in the organisation's single currency, stored and rounded to
  that currency's minor unit; never carries its own currency dimension.
- **General-ledger transaction** — an immutable debit/credit line; unchanged by this feature and the
  authoritative source of every balance.
- **Financial statement** — a generated report (Balance Sheet, Income Statement, Trial Balance, Bank
  Reconciliation, Tax Summary, and the others). Gains a basis-of-accounting disclosure; the Balance
  Sheet and Trial Balance gain integrity checks; the Bank Reconciliation gains the conventional
  layout.
- **Audit-trail entry** — a record of a financial action, governed by the retention period; this
  feature lengthens the default, hardens the purge, and closes a coverage gap.
- **Accounting-policy document** — a new published reference describing how the app keeps the books.

## Success Criteria

### Measurable Outcomes

- **SC-001**: An organisation can complete first-run setup with a non-Australian currency and a
  non-Australian financial-year start and never need to change a setting afterwards to get correct
  money display and correct reporting periods.
- **SC-002**: With any shipped display region active, 0 monetary values anywhere in the app or its
  exports show a currency symbol or code other than the configured one.
- **SC-003**: Entering the local representation of "one and a half" into any money field — including
  the manual journal and opening balances — stores exactly 1.5 units in 100% of shipped regions.
- **SC-004**: 100% of existing Australian-dollar reference datasets produce identical report figures
  and identical stored values before and after the change.
- **SC-005**: Generating a Balance Sheet from an unbalanced ledger fails or flags the imbalance in
  100% of cases; a balanced ledger ties in 100% of cases.
- **SC-006**: A Trial Balance whose debits and credits differ by one cent fails to generate.
- **SC-007**: 100% of financial statements display an accurate basis-of-accounting line.
- **SC-008**: The bank reconciliation report shows both "balance per bank statement" and "balance per
  general ledger" and demonstrates their equality on every finalised reconciliation.
- **SC-009**: A financial transaction dated into a closed period is rejected in 100% of attempts, with
  no partial record created.
- **SC-010**: The audit-retention default on a new dataset is at least five years.
- **SC-011**: Recording attendance that accrues a fee produces an audit-trail entry in 100% of cases.
- **SC-012**: The accounting-policy document exists, and every statement in it matches observed system
  behaviour on review.
- **SC-013**: The sales-tax internationalisation assessment records a scoped decision for each of its
  four required points.
- **SC-014**: An organisation whose inception date falls after its financial-year start reports its
  first financial-year-preset period bounded at the inception date and labelled a part-year, while
  every later year is a full twelve months; an organisation with no inception date, or one on the
  anchor, is unchanged.

## Assumptions

- **Single currency per organisation.** Multi-currency, changing the currency after setup, and
  foreign-exchange translation are out of scope.
- **Benchmark is universal double-entry / bookkeeping good practice**, not formal IFRS/GAAP
  compliance; producing statutory financial statements remains each organisation's accountant's job.
- **52/53-week ("4-4-5") fiscal calendars are out of scope**; the financial year is a start month plus
  a start day, running twelve months.
- **A short first financial year (FR-022) is delivered.** US7 (#352) delivered only the month + day
  FY-start choice; the optional stub first year it deliberately left out was carried as follow-on
  issue **#353** *"[FEATURE] Support a sub-twelve-month first financial year, labelled as a
  part-year"* (parent #341, FY-start work #352, filed 2026-08-30, T076) and is now **implemented on
  this branch as part of spec 028 (Phase 14, tasks T095–T113)**: an optional `Settings.InceptionDate`
  captured at first-run setup and a first-period-aware `FinancialYearCalculator` overload. When the
  inception date is later than the financial-year anchor the first financial year opens on the
  inception date and is labelled a part-year on the Trial Balance, Income Statement, Tax Summary and
  Balance Sheet; every later year is a full twelve months. A null inception date (every pre-existing
  dataset) and an inception date on the anchor are unchanged — a full twelve-month first year, no
  label. Range calculation and presentation only: no stored monetary amount, tax amount or GL
  balance changes, and the AUD zero-drift regression (T013) still passes.
- **The `capabilities/settings/spec.md` living spec still carries retired ABN / GST-registration
  wording** (`IsGstRegistered`, per-fee `GstCode`, the "GST / BAS" tab, the ATO ABN checksum), stale
  since spec 016 replaced that model with `Settings.IsTaxApplicable` / `TaxRate` / `TaxCode`. FR-028
  scopes only the *finance* living spec, so this feature de-drafts and corrects
  `capabilities/finance/spec.md` (T085) and fixes the retention figure in
  `capabilities/audit-trail/spec.md` (T086), but the settings living spec's tax wording is carried
  forward as a separate follow-up — **#356** *"[DOCS] Update `capabilities/settings/spec.md` to the
  current tax model"* (parent #341), filed 2026-08-30 — rather than addressed here (T086a).
- **"At least five years" (FR-023) is an informed default** for common record-keeping expectations;
  the exact figure (five versus seven) is a configuration default, not a hard rule, and the existing
  1–7 year adjustable range is retained.
- **US10 / sub-issue #350 is a scoping spike.** Only the written assessment and the creation of
  follow-on issues are in scope here; no multi-jurisdiction tax implementation. The assessment is
  published at [`docs/assessments/sales-tax-internationalisation.md`](../../docs/assessments/sales-tax-internationalisation.md)
  (T087): of its four required points, **rate history / effective-dating** (size L) and **multiple
  simultaneous rates / jurisdictions** (size XL) are **out of scope**; **tax-exclusive amount entry**
  (size M) and the **balance-sheet classification of recoverable input tax, accounts `2310` / `2320`**
  (size S–M) are **in scope** and each has a follow-on GitHub issue, filed 2026-08-30 against parent
  #341 referencing spike #350 (T088): **#354** *"[FEATURE] Support tax-exclusive amount entry (net +
  tax) alongside the current tax-inclusive entry"* and **#355** *"[FEATURE] Classify recoverable input
  tax (account `2320`) correctly on the Balance Sheet"*. Both issue bodies are also reproduced in the
  assessment's *Follow-on issues* section.
- **FR-033 verification (T089).** A `git diff master...HEAD` review of every tax-adjacent file
  confirms the only tax-path change on this branch is the optional `minorUnitDigits` rounding-precision
  parameter on `TaxCalculator.SplitInclusive` (default `2`, so an AUD / 2-decimal dataset is
  byte-identical). The GL line structure, the `2310` / `2320` accounts, the `TaxCode` enum and its
  stored values, the tax-inclusive entry model, and the Tax Summary net arithmetic are untouched; the
  `AudZeroDriftTests` stored-value assertions (T013) hold against the final build.
- Financial reports remain synchronous, unaudited management accounts.
- The eleventh sub-issue count refers to #342–#352; two originally-deferred gaps (G9, G11) were
  promoted into #352 and #351 at the user's request and are in scope.

## Verbatim Constraints

These literal values are pinned by issue #341 / its sub-issues and by spec 027, and downstream steps
and the implementation MUST use them exactly:

- `AUD` — the default currency code, and the value existing datasets carry.
- `$` — the currency symbol an existing Australian-dollar dataset MUST continue to display.
- `ISO 4217` — the standard the currency-code selection MUST conform to.
- `FinancialYearStartMonth` — the existing organisation-configuration field the financial-year-start
  work extends.
- `AuditRetentionYears` — the existing organisation-configuration field whose default this feature
  raises.
- `2310` / `2320` — the general-ledger account numbers for collected and recoverable sales tax
  referenced by the sales-tax assessment.
