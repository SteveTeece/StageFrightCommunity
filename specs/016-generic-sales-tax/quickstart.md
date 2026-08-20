# Quickstart: Validating Generic International Sales Tax

## Prerequisites

- Repo built on this branch (`016-generic-sales-tax`): `dotnet restore && dotnet build`
- SQLite dev database reset if you want a truly clean first-run: delete `TestData/stagefright.db` (auto-recreated on next launch).

## Scenario 1 — New non-Australian organisation (US1)

1. `dotnet run --project src/StageFright.App/` with no existing database.
2. Setup wizard Organisation step: confirm there is **no ABN field**.
3. Sales Tax step: leave the toggle off → Next/Finish succeeds with no rate/fee-code prompts.
4. Re-run the wizard against a fresh DB; this time toggle "Sales tax applies" on, leave the rate blank, try Next → blocked with a validation message. Enter a rate (e.g. `15`) and set Annual Fee = Taxable, Attendance Fee = Tax-exempt → Next succeeds.
5. Finish. Confirm the dashboard loads (no ABN/GST wording anywhere).

**Expected**: `Settings.IsTaxApplicable = true`, `Settings.TaxRate = 15`, `AnnualFeeTaxCode = Taxable`, `AttendanceFeeTaxCode = TaxExempt`, `Abn` column absent from the schema.

## Scenario 2 — Settings page Sales Tax tab (US2)

1. From the running app, open Settings → confirm a "Sales Tax" tab exists (not "GST / BAS") and the General tab has no ABN field/notice.
2. Toggle tax off → confirm the confirmation prompt appears before it's saved; confirm.
3. On the General tab, change Annual Fee (don't save yet). Switch to Sales Tax, turn tax back on with a rate, save. Return to General, save.
4. **Expected**: both changes persisted — the Sales Tax change isn't lost by the later General save (cross-tab save safety, FR-008).

## Scenario 3 — Upgrade path (US3)

1. Seed a pre-upgrade-shaped database: an org with `IsGstRegistered = true`, an `Abn` value, `AnnualFeeGstCode = Gst`, and at least one historical `Fee`/`Transaction` row with `GstCode = InputTaxed`.
2. Run the app / apply the new migration.
3. **Expected**: `IsTaxApplicable = true`, `TaxRate = 10`, no `Abn` anywhere, the historical `InputTaxed` row now reads `TaxExempt`, and its dollar amounts are byte-identical to before migration.
4. Run `dotnet test tests/StageFright.Data.Tests/` — the new migration value-remap test(s) must pass.

## Scenario 4 — Tax Summary report (US4)

1. With tax applicable off, generate the "Tax Summary" report from the Reports menu → expect an explanatory message, no dollar figures.
2. Toggle tax on with a rate, post a taxable fee, generate the report again → expect plain-English rows (Total taxable sales, Total tax-exempt sales, Tax collected on sales, Tax paid on purchases, Net tax payable/refundable) with no "BAS"/"G1"/"1A" wording anywhere in the output (PDF and CSV).

## Scenario 5 — Debug sample data reflects the new model (FR-017)

1. In a Debug build, run the wizard, check "Load sample data", finish.
2. **Expected**: seeding completes without error; seeded `Fee`/`Transaction` rows use `TaxCode` values consistent with whatever was chosen in the wizard's Sales Tax step, posted to the renamed `SystemAccounts.TaxCollectedId` account.

## Full verification

```bash
dotnet build
dotnet test
```

Both must be green before the feature is considered complete (CLAUDE.md Build & Test Verification).
