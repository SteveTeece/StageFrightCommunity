# Feature Specification: Generic International Sales Tax (Replacing ABN & GST)

**Feature Branch**: `016-generic-sales-tax`

**Created**: 2026-08-18

**Status**: Draft

**Input**: User description: "ABN Numbers and GST registration are applicable to Australia only. Remove the requirement to enter an ABN on the setup wizard, and make the tax system more generic and international. Also allow for the user to set a tax rate (if tax is applicable) in the startup wizard and the settings page per issue #300." (GitHub issue #300: "ABN is only applicable for Australia. Remove the ABN requirement and the question about GST registration (GST is Australia only) and replace with a question relating to sales tax. Also allow the user to enter the tax rate (if any) on the startup wizard and on the settings screen.")

This feature supersedes the GST/ABN-specific behavior introduced by `003-gst-registration-setup` for all *new* activity. That spec is left unchanged as a historical record of what was built then; this spec documents the generic replacement and how existing installations/data carry forward.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - First-run setup without ABN, with generic sales tax (Priority: P1)

As the person setting up StageFright Community for a new organisation anywhere in the world, I complete the setup wizard without being asked for an Australian Business Number, and — if my organisation charges sales tax — I record that it applies, at what rate, and which of the two fee types (annual, attendance) are taxable, instead of being asked an Australia-specific "GST registered" question.

**Why this priority**: This is the change issue #300 exists for — a non-Australian organisation currently cannot get past the setup wizard at all (it demands a checksum-valid ABN). Without this, the product is unusable outside Australia.

**Independent Test**: Run the wizard end-to-end for an organisation with sales tax applicable (a rate entered, both fee types set taxable/exempt) and for one where it doesn't apply; verify the resulting `Settings` row matches what was entered in each case, that no ABN field exists anywhere in the wizard, and that leaving tax rate blank while "tax applies" is toggled on blocks completion.

**Acceptance Scenarios**:

1. **Given** the wizard's Organisation step, **When** I view it, **Then** there is no ABN field and no ABN validation of any kind blocking progress.
2. **Given** the step that asks about tax, **When** I toggle "Sales tax applies to this organisation" on, **Then** a required tax-rate percentage field and the two per-fee-type (Annual Fee, Attendance Fee) taxable/exempt dropdowns appear, each defaulting to tax-exempt; toggling off hides them again and clears any values entered.
3. **Given** the tax toggle is on, **When** I leave the tax rate blank or enter a non-positive value and click Next, **Then** I cannot advance and see a validation message.
4. **Given** valid entries throughout, **When** I click Finish, **Then** the resulting `Settings` row has no ABN value stored anywhere, and has the tax-applicable flag, tax rate, and both per-fee tax treatments persisted exactly as entered — with the rate and both fee codes forced to empty/not-applicable if tax was toggled off, regardless of what was entered before toggling off.

---

### User Story 2 - Changing sales tax settings after setup (Priority: P2)

As a treasurer whose organisation starts or stops charging sales tax, or whose tax rate changes, I update that from the Settings page's "Sales Tax" tab without touching an Australia-specific "GST / BAS" tab, and my change doesn't get silently overwritten by a concurrent edit on another tab.

**Why this priority**: Organisations' tax obligations change over time (new registration threshold, rate change, deregistration); this must be editable after first-run setup, not just at setup time.

**Independent Test**: Open Settings, confirm there is no ABN field anywhere and the tab is labelled "Sales Tax" (not "GST / BAS"); toggle tax applicability, set/change the rate, confirm the existing warning-before-commit dialog still appears before the change is saved; verify a cross-tab save doesn't clobber the other tab's already-saved change.

**Acceptance Scenarios**:

1. **Given** the Settings page, **When** it loads, **Then** the General tab contains no ABN field and no Australia-specific tax wording, and a "Sales Tax" tab exists in the same tab position the "GST / BAS" tab previously occupied.
2. **Given** the Sales Tax tab, **When** I toggle whether tax applies, **Then** a confirmation prompt describing the effect on future postings and reporting appears (the same warn-before-commit pattern as today), and nothing is persisted until I confirm.
3. **Given** tax already applies, **When** I change the tax rate or either fee type's taxable/exempt treatment and save, **Then** the new values apply to postings made from that point forward; nothing already posted is altered.
4. **Given** I edit the General tab (e.g. change Annual Fee amount) without saving, then switch to the Sales Tax tab, change the rate, and save there, **When** I return to the General tab and click Save, **Then** the Sales Tax change I just saved is preserved, not overwritten by the General tab's stale in-memory copy (and vice versa for a General-tab change saved after a Sales Tax edit).

---

### User Story 3 - Existing installations upgrade cleanly (Priority: P2)

As an existing organisation that already uses StageFright Community with an ABN on file and GST registration configured, I upgrade to this version and find my historical financial records untouched and my settings automatically carried forward into the new generic model, without having to re-enter anything to keep using the application.

**Why this priority**: Financial records are immutable and must never be altered or lost; an organisation with real transaction history cannot be asked to accept data loss or broken reports as the price of this change.

**Independent Test**: Start from a database containing an organisation with an ABN on file, GST registration on, a configured GST treatment for each fee type, and posted historical fees/payments/transactions carrying GST codes; upgrade; verify no ABN is displayed or requested anywhere, the organisation's tax-applicable flag and rate reflect its prior GST registration, every historical financial record still displays a valid, meaningful tax treatment, and previously posted dollar amounts are unchanged.

**Acceptance Scenarios**:

1. **Given** an organisation that had `GST registered = true` before upgrading, **When** the upgrade completes, **Then** "sales tax applies" is on and the tax rate reflects the rate that was previously in effect for that organisation.
2. **Given** an organisation that had `GST registered = false` before upgrading, **When** the upgrade completes, **Then** "sales tax applies" is off and no tax rate is set.
3. **Given** historical fee/payment/transaction records posted under any of the previous GST treatments, **When** the upgrade completes, **Then** every one of those records still shows a valid tax treatment that preserves its original tax-or-not meaning, and its posted dollar amounts are unchanged.
4. **Given** an organisation that had an ABN on file, **When** the upgrade completes, **Then** that value is gone and no longer displayed, requested, or exported anywhere.

---

### User Story 4 - Tax summary reporting reflects the generic model (Priority: P3)

As a treasurer, I can generate a summary report of tax collected and paid that uses plain, universally-understandable language instead of Australian government form codes, so the report is useful regardless of which country my organisation operates in.

**Why this priority**: Lower priority than the data-entry changes because it doesn't block basic usability outside Australia, but the existing report is unusable/confusing for a non-Australian organisation and must be brought in line with the rest of this change.

**Independent Test**: Generate the report for an organisation with sales tax applicable and one where it isn't; verify the report shows plain-English rows (not Australian Business Activity Statement form codes) and a clear explanatory message when tax doesn't apply.

**Acceptance Scenarios**:

1. **Given** an organisation where sales tax does not apply, **When** I generate the report, **Then** it explains that tax isn't applicable and how to enable it, without showing dollar figures.
2. **Given** an organisation where sales tax applies, **When** I generate the report for a date range, **Then** it shows total taxable sales, total tax-exempt sales, tax collected on sales, tax paid on purchases, and a net amount payable or refundable, in plain English with no Australian tax-office terminology.

---

### Edge Cases

- What happens if an organisation toggles "sales tax applies" on but sets the rate to exactly 0? Treated as invalid input (same as leaving it blank) — a rate must be a positive number when tax is toggled on; 0% is expressed by turning tax off, not by a zero rate.
- What happens to a historical record that was posted under the old "input-taxed" GST treatment (a treatment being retired)? It is reclassified to the closest equivalent generic treatment (no tax component was charged) so it remains valid and displayable; its original posted amounts are untouched.
- What happens when an organisation changes its tax rate after having posted some transactions at the old rate? Only future postings use the new rate; previously posted transactions keep the amounts they were posted with (financial records are immutable and are never recalculated).
- What happens to an organisation's ABN that was already on file before upgrading? It is discarded; nothing about it is retained, displayed, or exported after the upgrade.
- What happens if a plugin or export currently references the old Australia-specific wording? Out of scope for this feature — only the first-party wizard, Settings page, and the one built-in tax report are covered (see Assumptions).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The setup wizard MUST NOT ask for, validate, store, or display an Australian Business Number (or any equivalent business-registration identifier) anywhere in its flow.
- **FR-002**: The setup wizard MUST replace its Australia-specific GST registration question with a generic question: whether sales tax applies to the organisation.
- **FR-003**: When the wizard's "sales tax applies" toggle is on, the wizard MUST require a positive tax rate (percentage) and MUST offer a taxable/exempt choice for each of the two fee types (Annual Fee, Attendance Fee), each defaulting to exempt.
- **FR-004**: When the wizard's "sales tax applies" toggle is off, the wizard MUST NOT collect or persist a tax rate or either fee type's tax treatment, and MUST discard any such values previously entered before the toggle was switched off.
- **FR-005**: The Settings page MUST provide a "Sales Tax" tab, in the position previously occupied by the "GST / BAS" tab, that lets a user view and change: whether tax applies, the tax rate, and each fee type's taxable/exempt treatment — after first-run setup has completed.
- **FR-006**: Toggling whether tax applies on the Settings page MUST require the user to confirm the change before it is saved, describing its effect on future postings and reporting (matching the confirm-before-commit behavior the GST toggle had).
- **FR-007**: The Settings page's General tab MUST NOT contain any ABN field, any Australia-specific tax wording, or any notice about a missing ABN.
- **FR-008**: A save made from the Sales Tax tab MUST NOT lose an unsaved-then-saved change made concurrently on the General tab (and vice versa) — matching the existing cross-tab save-safety guarantee.
- **FR-009**: The system MUST retain a single current tax rate and a single current taxable/exempt treatment per fee type that applies to all postings made from the moment they're saved onward; the system is not required to track multiple simultaneous rates, jurisdictions, or rate history/effective-dating.
- **FR-010**: Every fee, payment, and transaction record already posted before this feature ships MUST remain exactly as posted — same dollar amounts, same debit/credit balance — after upgrading; only the label describing its tax treatment may change, and only to an equivalent generic label.
- **FR-011**: An organisation that had sales-tax registration (under the prior Australia-specific model) enabled before upgrading MUST have "sales tax applies" enabled after upgrading, with a tax rate equal to the rate that was in effect for it beforehand.
- **FR-012**: An organisation that did not have sales-tax registration enabled before upgrading MUST have "sales tax applies" disabled and no tax rate set after upgrading.
- **FR-013**: Any ABN previously on file for an organisation MUST be discarded during the upgrade and never surfaced again.
- **FR-014**: The built-in tax summary report MUST use plain, country-neutral language (no Australian government form codes or terminology) to present: total taxable sales, total tax-exempt sales, tax collected on sales, tax paid on purchases, and a net amount payable or refundable for a chosen date range.
- **FR-015**: The built-in tax summary report MUST clearly explain, without showing dollar figures, that tax isn't applicable when the organisation has tax turned off.
- **FR-016**: The system MUST offer exactly three tax treatments for a fee or transaction — taxable, tax-exempt, and excluded from tax reporting entirely (transfers, journals, opening balances) — replacing the four Australia-specific treatments the prior model offered.
- **FR-017**: The Debug-build sample/demo data generator (the setup wizard's optional "Load sample data" step) MUST populate the sample organisation and its sample fees/transactions using the new generic sales-tax model — a sample tax rate and taxable/exempt fee treatments — instead of the retired ABN/GST fields, so demo data stays representative of what a real organisation now enters.

### Key Entities *(include if feature involves data)*

- **Organisation tax settings**: Whether sales tax applies to the organisation, its current tax rate (a percentage, present only while tax applies), and which of the two fee types are taxable versus exempt. Replaces the organisation's prior GST-registration flag, per-fee GST codes, and ABN.
- **Tax treatment**: A label stamped on a fee, payment, or transaction line describing how it was taxed at the time it was posted — taxable, tax-exempt, or excluded from tax reporting. Replaces the prior four-value GST treatment; historical records keep whichever treatment was in force when they were posted.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A person setting up a new, non-Australian organisation can complete the setup wizard end-to-end without being asked for, or blocked by, any Australia-specific business identifier.
- **SC-002**: An organisation with sales tax applicable can complete setup or a later Settings change with any positive tax rate it enters, not limited to a single fixed percentage.
- **SC-003**: 100% of financial records posted before this feature ships display an unchanged dollar amount and a valid, meaningful tax treatment after upgrading — zero historical records become unreadable or show incorrect amounts.
- **SC-004**: Every place in the setup wizard, Settings page, and built-in tax report that previously showed "ABN", "GST", or "BAS" wording shows generic sales-tax wording instead.
- **SC-005**: Switching between the General and Sales Tax settings tabs and saving from either one, in either order, never loses the other tab's already-saved change.

## Assumptions

- "Sales tax" is modeled as a single flat percentage rate applied organisation-wide, matching how the prior GST implementation worked (one hardcoded rate for the whole organisation) — support for multiple simultaneous rates, tiered rates, or jurisdiction-specific rules is out of scope.
- The three generic tax treatments (taxable, tax-exempt, excluded) are sufficient to describe any organisation's fees under a simple flat-rate sales tax model; the more specialized "input-taxed" concept from Australian GST does not need a generic equivalent and is retired, with historical records using it reclassified as tax-exempt (no tax component charged) since that preserves their original financial meaning.
- The setup wizard, the Settings page, the one built-in tax summary report, and the Debug-build sample/demo data generator are in scope. Any third-party plugin that surfaces GST/ABN-specific text, and any deeper redesign of the double-entry GL posting mechanics, are out of scope for this feature.
- Existing/upgrading organisations are not required to take any manual action to migrate their tax settings or historical data — the upgrade carries their prior GST-registration status and rate forward automatically.
- The tax rate is entered and displayed as a percentage (e.g. "10" meaning 10%), consistent with how a treasurer would naturally think about a sales tax rate.
