# Assessment: Internationalising the sales-tax feature

**Status**: assessment complete — spike only, no implementation in this feature.
**Owner spec**: [`specs/028-international-accounting-standards`](../../specs/028-international-accounting-standards/spec.md)
(User Story 10, FR-029 / FR-030, FR-033) · **Origin**: GitHub issue #341 → sub-issue #350 (gap G10).

This is the written assessment FR-029 requires: what it would take to use the sales-tax feature
outside its current single-jurisdiction assumptions, with an **in-scope / out-of-scope decision and a
rough size for each required point**, and a follow-on issue for every in-scope point (FR-030).

Nothing here is built in spec 028. Spec 028 changes **no** tax posting mechanic and **no** stored tax
code value (FR-033); see [Constraint check](#constraint-check-fr-033) below.

---

## The current model (what spec 016 built)

Spec [016 — Generic International Sales Tax](../../specs/016-generic-sales-tax/spec.md) replaced the
Australia-specific ABN / GST-registration model with a country-neutral one:

- **`Settings.IsTaxApplicable`** — whether sales tax applies to the organisation at all.
- **`Settings.TaxRate`** — one flat percentage, organisation-wide, present only while tax applies.
- **`Settings.AnnualFeeTaxCode` / `Settings.AttendanceFeeTaxCode`** — the treatment applied to each
  fee type's accruals.
- **`Fee.TaxCode` / `Transaction.TaxCode`** — one of `Taxable`, `TaxExempt`, `Excluded`, stamped at
  posting time and never revisited (spec 016 FR-009 / FR-016).
- **`TaxCalculator.SplitInclusive`** — every amount is entered **tax-inclusive**; the tax component is
  `round(gross × rate ÷ (100 + rate))` to the currency's minor unit, and the net is the remainder so
  the parts re-sum to the gross exactly.
- **GL accounts** — `2310` "Tax Collected" and `2320` "Tax Paid", both seeded as
  `AccountType.Liability`. A taxable sale posts a `2310` credit; a taxable purchase posts a `2320`
  debit. The Tax Summary report nets the two: `net = (2310 net credit movement) − (2320 net debit
  movement)`, payable when positive, refundable when negative.

Spec 016 explicitly left multiple simultaneous rates, jurisdiction-specific rules, and rate
history / effective-dating out of scope (016 FR-009, 016 Assumptions).

---

## Sizing scale

| Size | Rough effort | Shape |
|------|--------------|-------|
| **S** | ≤ 1 day | one file / one migration + a handful of tests |
| **M** | 2–4 days | a new setting + a calculator branch + entry-form and report changes + tests |
| **L** | 1–2 weeks | a new model dimension (extra table / effective-dating) touching every posting path and every tax-aware report |
| **XL** | multi-week, own epic | multi-entity redesign; not a single feature |

---

## Point 1 — Rate changes over time (rate history / effective-dating)

**What it is.** A jurisdiction changes its VAT/GST rate. The organisation needs postings before the
change date to keep the old rate and postings on/after it to use the new rate — ideally without the
treasurer having to remember to edit the rate on the right day.

**Current behaviour.** One `Settings.TaxRate`. Changing it affects only **future** postings; already
posted `Fee` / `Transaction` rows are immutable and keep the amounts they were posted with (spec 016
edge cases, FR-010). There is no stored rate history and no effective-date lookup — the treasurer
edits the rate manually when it changes.

**Assessment.** The current "single current rate, future-only, immutable history" behaviour is
**already correct** for the common case. A rate change is rare for a community / amateur-theatre group
(a jurisdiction may move its rate once in a decade), and the immutable-ledger design means a
mis-timed manual edit only mis-rates postings made in the gap, which a reversing entry corrects.
True effective-dating means an effective-dated rate table, a rate-as-of-date lookup in
`TaxCalculator` and every posting path, and back-fill/repost tooling for the gap — a new model
dimension out of proportion to the need.

**Decision: OUT OF SCOPE.** Keep the single-current-rate model. Revisit only if a real deployment
reports pain. **Rough size if ever done: L.**

---

## Point 2 — Tax-exclusive entry

**What it is.** The treasurer enters the **net** amount and the system **adds** tax on top, rather
than entering a tax-inclusive gross and splitting it out. This is how US sales tax and many
tax-exclusive invoicing conventions work ("$100 + 8% tax = $108").

**Current behaviour.** Every amount — annual fee, attendance fee, manual income, expense payment — is
entered **tax-inclusive** and `TaxCalculator.SplitInclusive` works backwards from the gross. There is
no way to say "this figure is net of tax".

**Assessment.** A genuine usability gap for tax-exclusive jurisdictions, but **bounded**: a per-org
`Settings.TaxEntryMode` (`Inclusive` / `Exclusive`, default `Inclusive` so existing data is
untouched), a `SplitExclusive(net, rate)` companion to `SplitInclusive` (`tax = round(net × rate ÷
100)`, gross = net + tax), a mode branch in the five posting services and the two UI tax-hint sites,
and a label change on the entry forms and the tax-hint text. No change to the GL line structure, the
accounts, or `TaxCode`. Historical records are unaffected because the mode only decides how a
**newly entered** figure is interpreted.

**Decision: IN SCOPE (follow-on).** → **[Issue A](#follow-on-issues)**. **Rough size: M.**

---

## Point 3 — Balance-sheet classification of recoverable tax (accounts `2310` / `2320`)

**What it is.** Tax the organisation has **paid on purchases** and can **recover** from the tax
authority is economically an **asset** (a receivable from the authority), not a liability. Tax
**collected on sales** and owed to the authority is a liability. The balance sheet should classify
each on the correct side, and present the organisation's net tax position sensibly.

**Current behaviour.** Both `2310` "Tax Collected" **and** `2320` "Tax Paid" are seeded as
`AccountType.Liability`. While the organisation is in a net-**payable** position (collected exceeds
paid) the combined `2000`-range presentation reads acceptably. In a net-**refundable** position (tax
paid on purchases exceeds tax collected on sales — common for a group that buys a lot and sells
little), `2320` sits in Liabilities as a **negative liability**, so the balance sheet understates
liabilities and never shows the recoverable amount as the asset it is. The
[`BalanceSheetReportProvider`](../../src/StageFright.Reports/Providers/BalanceSheetReportProvider.cs)
groups purely by `AccountType`, so it inherits this.

**Assessment.** Bounded and worth fixing. Options, smallest first:
1. **Reclassify `2320` as `AccountType.Asset`** ("Tax Receivable"), via a data migration that
   re-types the seeded system account and moves its number into the asset range (or keeps `2320` as
   a documented asset exception). Balance-sheet grouping then falls out correctly. Touches the seed,
   one migration, `SystemAccounts` doc-comments, `AccountNumberAssignmentService` comments, and the
   Tax Summary net calc sign convention. **S–M.**
2. **Net-tax presentation line on the Balance Sheet** — leave the accounts as-is but compute the net
   `2310 − 2320` position and present it as a single "Net sales tax payable / (recoverable)" line on
   the correct side. Smaller migration risk, but the raw account balances still read oddly in the
   Chart of Accounts and Trial Balance. **S.**

Either keeps every stored amount and every `TaxCode` untouched — it is a **classification and
presentation** change only.

**Decision: IN SCOPE (follow-on).** → **[Issue B](#follow-on-issues)**. **Rough size: S–M**
(option 1 preferred; final option chosen on the issue).

---

## Point 4 — Multiple simultaneous rates or jurisdictions

**What it is.** One organisation that must charge **more than one rate at once** (standard / reduced /
zero-rated categories) or operates across **more than one tax jurisdiction** (different rates,
registration numbers, and returns per jurisdiction).

**Current behaviour.** Exactly one rate, one jurisdiction, one pair of clearing accounts. Each fee
type gets one `TaxCode`; there is no per-line rate, no rate category, and no jurisdiction dimension.

**Assessment.** This is the "separate body of work" the spec 028 Assumptions and spec 016 both carve
out. It needs a rate/category entity, per-line rate selection on every entry form, per-jurisdiction
clearing accounts and returns, and a redesign of the Tax Summary into a per-jurisdiction report — a
multi-entity redesign, not a single feature. A community / amateur-theatre group operates in one
jurisdiction and, in practice, at one rate; the demand does not justify the model cost.

**Decision: OUT OF SCOPE.** Not planned. If a concrete multi-jurisdiction deployment ever appears,
it is its own epic under a fresh parent issue. **Rough size if ever done: XL.**

---

## Summary

| # | Point | Decision | Size | Follow-on |
|---|-------|----------|------|-----------|
| 1 | Rate changes over time | **Out of scope** | L | — |
| 2 | Tax-exclusive entry | **In scope** | M | Issue A |
| 3 | Recoverable-tax balance-sheet classification (`2310` / `2320`) | **In scope** | S–M | Issue B |
| 4 | Multiple simultaneous rates / jurisdictions | **Out of scope** | XL | — |

---

## Follow-on issues

Two in-scope points require a follow-on GitHub issue each (FR-030). Both are filed against parent
**#341** and reference sub-issue **#350**.

> **Filing status.** `gh issue create` is blocked by this environment's action classifier (the same
> block recorded for T076 / T086a in the spec 028 Assumptions). The two issues are specified in full
> below, ready to paste, and are recorded in
> [`spec.md`](../../specs/028-international-accounting-standards/spec.md) Assumptions. **The maintainer
> creates them**; replace each `_pending_` link with the issue URL once filed.

### Issue A — Tax-exclusive amount entry — `_pending_`

**Title**: `[FEATURE] Support tax-exclusive amount entry (net + tax) alongside the current tax-inclusive entry`

**Body**:
> Parent: #341 · Follows the spike in #350 and
> `docs/assessments/sales-tax-internationalisation.md` (Point 2).
>
> Today every amount is entered tax-inclusive and `TaxCalculator.SplitInclusive` works backwards from
> the gross. Tax-exclusive jurisdictions (US sales tax and similar) quote and enter the net amount
> and add tax on top.
>
> **Scope**
> - `Settings.TaxEntryMode` enum (`Inclusive` / `Exclusive`), default `Inclusive`; chosen at setup
>   and on the Sales Tax settings tab; migration defaults every existing row to `Inclusive`.
> - `TaxCalculator.SplitExclusive(net, rate, minorUnitDigits)` → `tax = round(net × rate ÷ 100)`,
>   `gross = net + tax`.
> - Mode branch in `FeeService`, `IncomeEntryService`, `ExpensePaymentService`,
>   `ReactivationForgivenessService`, `AttendanceService`, and the two UI tax-hint sites
>   (`RecordIncome`, `ExpensePaymentPage`).
> - Entry-form and tax-hint labels reflect the active mode.
>
> **Out of scope**: any change to the GL line structure, the `2310` / `2320` accounts, or `TaxCode`.
> Historical records are unaffected — the mode only interprets a newly entered figure.
>
> **Acceptance**
> - An `Exclusive`-mode org entering `100` at `8%` posts net `100` / tax `8` / gross `108`.
> - An `Inclusive`-mode org is byte-identical to today.
> - Re-sum-to-gross exactness holds at 0, 2, and 3 minor digits.
>
> Rough size: **M**.

### Issue B — Recoverable input tax as a balance-sheet asset — `_pending_`

**Title**: `[FEATURE] Classify recoverable input tax (account 2320) correctly on the Balance Sheet`

**Body**:
> Parent: #341 · Follows the spike in #350 and
> `docs/assessments/sales-tax-internationalisation.md` (Point 3).
>
> `2320` "Tax Paid" is seeded as `AccountType.Liability`, but tax paid on purchases and recoverable
> from the authority is an **asset**. In a net-refundable position the Balance Sheet shows `2320` as
> a negative liability and never presents the recoverable amount as an asset.
>
> **Preferred approach** — reclassify the seeded `2320` system account as `AccountType.Asset`
> ("Tax Receivable"): data migration to re-type (and optionally renumber into the asset range),
> update `SystemAccounts` / `AccountNumberAssignmentService` doc-comments, and the Tax Summary
> net-calc sign convention. `BalanceSheetReportProvider` then groups it correctly with no provider
> change.
> **Alternative** — keep the accounts and add a computed "Net sales tax payable / (recoverable)"
> presentation line on the Balance Sheet.
>
> **Constraint**: no stored monetary amount and no `TaxCode` value changes — classification and
> presentation only. The spec 028 AUD zero-drift regression must still pass.
>
> **Acceptance**
> - A net-refundable org's Balance Sheet shows the recoverable tax as an asset.
> - A net-payable org's Balance Sheet shows the tax owed as a liability.
> - Trial Balance still ties exactly.
>
> Rough size: **S–M**.

---

## Constraint check (FR-033)

FR-033 / US10 AC-3: spec 028 must change no existing tax posting mechanic and no stored tax code
value. Verified as task **T089** — see the branch-diff review recorded in
[`spec.md`](../../specs/028-international-accounting-standards/spec.md) Assumptions and the
[`AudZeroDriftTests`](../../tests/StageFright.Integration.Tests/InternationalAccounting/AudZeroDriftTests.cs)
stored-value assertions. In summary, the only tax-adjacent change on this branch is the optional
`minorUnitDigits` **rounding-precision** parameter added to `TaxCalculator.SplitInclusive` (default
`2`, so an AUD / 2-decimal dataset is byte-identical); the GL line structure, the `2310` / `2320`
accounts, the `TaxCode` enum and its stored values, the tax-inclusive entry model, and the Tax
Summary net arithmetic are all untouched.
