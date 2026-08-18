# Phase 0 Research: Generic International Sales Tax

No `[NEEDS CLARIFICATION]` markers remained after `/speckit-specify` (all scoping ambiguity was resolved with the user beforehand — see spec.md's "Input" section). The items below are the concrete technical decisions needed to execute the plan, resolved by reading the existing implementation rather than external research, since this is a rename/generalization of code that already exists and works.

## Decision: Tax-inclusive split formula generalization

- **Decision**: Replace `GstCalculator.SplitInclusive(gross)` (hardcoded `gst = round(gross / 11, 2, AwayFromZero)`) with `TaxCalculator.SplitInclusive(gross, ratePercent)` where `tax = round(gross * ratePercent / (100 + ratePercent), 2, MidpointRounding.AwayFromZero)` and `net = gross - tax`.
- **Rationale**: `gross / 11` is the `ratePercent = 10` special case of `gross * 10 / 110`. The generalized formula must produce byte-identical results to the old one at rate 10 (verified by a test asserting `TaxCalculator.SplitInclusive(x, 10m) == GstCalculator.SplitInclusive(x)` for a range of `x` before the old type is deleted, per FR requirement that historical postings remain correct in spirit).
- **Alternatives considered**: Storing the rate as a fraction (e.g. `0.10m`) instead of percentage points (e.g. `10m`) on `Settings.TaxRate` — rejected because the spec's Assumptions section commits to percentage-as-entered (matching how a treasurer thinks and types), and every other numeric Settings field (`AnnualFee`, `AttendanceFee`) already stores the literal user-facing number, not a normalized fraction.

## Decision: Persisted representation of the renamed enum values

- **Decision**: `GstCode`/`Fee.GstCode`/`Transaction.GstCode`/`Settings.AnnualFeeGstCode`/`Settings.AttendanceFeeGstCode` are already configured with `HasConversion<string>()` (confirmed in `FeeConfiguration.cs`, `TransactionConfiguration.cs`, `SettingsConfiguration.cs`) — the database stores the enum member's **name**, not its ordinal. The migration therefore does a plain `UPDATE ... SET Column = 'NewName' WHERE Column = 'OldName'` value rewrite after the columns are renamed, not an ordinal remap.
- **Rationale**: This is simpler and safer than an ordinal-based remap and matches the existing configuration pattern exactly — no new conversion behavior is introduced, only the set of valid strings changes.
- **Mapping** (used by both the `Settings` per-fee-type columns and the `Fee`/`Transaction` line-level column): `"Gst"` → `"Taxable"`, `"GstFree"` → `"TaxExempt"`, `"InputTaxed"` → `"TaxExempt"` (nearest equivalent — no tax component, per spec Assumptions), `"BasExcluded"` → `"Excluded"`. `NULL` stays `NULL` (both old and new models use null/absent to mean "no code recorded").

## Decision: System account renaming — name only, not account number

- **Decision**: `SystemAccounts.GstCollectedId`/`GstPaidId` (fixed GUID constants) are renamed in C# to `TaxCollectedId`/`TaxPaidId`; their seeded `Account.Name` is updated from "GST Collected"/"GST Paid" to "Tax Collected"/"Tax Paid" via the migration. Their account **numbers** (2310, 2320) are left unchanged.
- **Rationale**: The GUIDs and numbers are internal keys other code and reports look up by; renaming only the display `Name` (a plain, always-mutable field on the non-exempt `Account` entity) avoids any risk to account-number-ordered report output or number-based lookups, while still removing all GST wording from what the user sees.
- **Alternatives considered**: Renumbering to a "Tax" range — rejected as unnecessary churn with no user-facing benefit; the number is an internal chart-of-accounts code, never described as GST-specific to the user.

## Decision: Debug seeder update scope

- **Decision**: `DebugDataSeeder.CreateAnnualFeeAccrualAsync` (the only seeder method touching tax fields) is updated to read `settings.IsTaxApplicable`/`settings.AnnualFeeTaxCode` and call the new `TaxCalculator`/`TaxCode`/`SystemAccounts.TaxCollectedId` names. No new sample scenario is invented — the seeder already reads whichever tax settings the user entered in the wizard immediately before seeding runs (`SetupService.InitializeAsync` persists `Settings` before `SeedAsync` is called), so this is a mechanical rename, not new seeding logic.
- **Rationale**: Matches FR-017 exactly and keeps the seeder's existing "seed reflects whatever was chosen in the wizard" behavior intact.

## Decision: Test-file rename strategy

- **Decision**: Test files for deleted types (`AbnValidatorTests.cs`, `AbnAttributeTests.cs`) are deleted outright. Test files for renamed types (`GstSettingsTabTests.cs` → `TaxSettingsTabTests.cs`, etc.) are renamed alongside their production type and their assertions/fixtures updated to the new field/enum names, preserving existing coverage rather than being rewritten from scratch — per CLAUDE.md's `Should_[ExpectedBehavior]_When_[Condition]` convention, most test *names* don't need to change, only the types/values they reference.
- **Rationale**: Matches constitution §11's non-negotiable coverage rule — every path already covered must stay covered through the rename.
