# Phase 1 Data Model: Generic International Sales Tax

## `Settings` (singleton row, `src/StageFright.Core/Entities/Settings.cs`)

| Field | Before | After | Notes |
|---|---|---|---|
| `Abn` | `string?` | *(removed)* | Column dropped by migration. |
| `IsGstRegistered` | `bool` | `IsTaxApplicable` (`bool`) | Column renamed; value carries forward unchanged (a rename preserves data automatically). |
| *(none)* | — | `TaxRate` (`decimal?`) | New column. `NULL` when `IsTaxApplicable` is false. Percentage points (e.g. `10.00m` means 10%). Migration backfill: `TaxRate = 10` for every row where the old `IsGstRegistered = true` (the rate that was implicitly hardcoded before); stays `NULL` otherwise. |
| `AnnualFeeGstCode` | `GstCode?` (string-backed) | `AnnualFeeTaxCode` (`TaxCode?`, string-backed) | Column renamed; stored string value remapped per the table below. |
| `AttendanceFeeGstCode` | `GstCode?` (string-backed) | `AttendanceFeeTaxCode` (`TaxCode?`, string-backed) | Same as above. |

Validation (`SettingsService.SaveAsync`, `SetupService.InitializeAsync`):
- `TaxRate` MUST be present and `> 0` when `IsTaxApplicable` is `true`; MUST be `null` when `IsTaxApplicable` is `false` (mirrors today's `IsGstRegistered`/GST-code null-forcing rule in `SetupService.Validate()`).
- `Abn`'s `[Abn]`/`[Required]` validation attributes, `AbnValidator`, and `AbnAttribute` are deleted entirely — no replacement validation for a business-registration identifier.

## `TaxCode` enum (replaces `GstCode`, `src/StageFright.Core/Enums/TaxCode.cs`)

| New value | Old value it replaces | Meaning |
|---|---|---|
| `Taxable` | `Gst` | Tax component applies — `TaxCalculator.SplitInclusive` splits out a tax amount. |
| `TaxExempt` | `GstFree`, `InputTaxed` | No tax component; counted as ordinary sales/expense. `InputTaxed` collapses into this value (spec Assumptions: nearest equivalent). |
| `Excluded` | `BasExcluded` | Outside tax reporting entirely (transfers, journals, opening balances). |

## `Fee` / `Transaction` (`src/StageFright.Core/Entities/{Fee,Transaction}.cs`)

| Field | Before | After |
|---|---|---|
| `GstCode` | `GstCode?` (string-backed) | `TaxCode` (`TaxCode?`, string-backed) — column renamed, existing values remapped (see table below). |

These entities remain financially immutable — the migration remaps only the `TaxCode` string value; `Amount`/`DebitAmount`/`CreditAmount`/dates/member/account links are untouched.

## Migration-time value remap (applies to `Fee.TaxCode`, `Transaction.TaxCode`, `Settings.AnnualFeeTaxCode`, `Settings.AttendanceFeeTaxCode`)

| Old stored string | New stored string |
|---|---|
| `"Gst"` | `"Taxable"` |
| `"GstFree"` | `"TaxExempt"` |
| `"InputTaxed"` | `"TaxExempt"` |
| `"BasExcluded"` | `"Excluded"` |
| `NULL` | `NULL` (unchanged) |

## `Account` (system accounts only, `src/StageFright.Core/Modules/Finance/SystemAccounts.cs` + seeded `Account` rows)

| Field | Before | After | Notes |
|---|---|---|---|
| `SystemAccounts.GstCollectedId` (GUID constant `...0004`) | C# name `GstCollectedId` | `TaxCollectedId` | Same GUID value — only the C# constant name changes. |
| `SystemAccounts.GstPaidId` (GUID constant `...0005`) | C# name `GstPaidId` | `TaxPaidId` | Same GUID value. |
| Seeded `Account.Name` for `...0004` | `"GST Collected"` | `"Tax Collected"` | Migration `UPDATE`; account number `2310` unchanged. |
| Seeded `Account.Name` for `...0005` | `"GST Paid"` | `"Tax Paid"` | Migration `UPDATE`; account number `2320` unchanged. |

## `TaxCalculator` (replaces `GstCalculator` + `GstConstants`, `src/StageFright.Core/Modules/Finance/TaxCalculator.cs`)

```
SplitInclusive(decimal gross, decimal ratePercent) -> (decimal Net, decimal Tax)
    tax = round(gross * ratePercent / (100 + ratePercent), 2, AwayFromZero)
    net = gross - tax
```

No more `GstConstants.Rate`/`InclusiveDivisor` — the rate is always supplied by the caller from `Settings.TaxRate`.

## Report: `TaxSummaryReportProvider` (replaces `BasSummaryReportProvider`)

Same underlying GL math (driven by `SystemAccounts.TaxCollectedId`/`TaxPaidId` movements and `TaxCode`-coded income/expense lines), relabeled rows:

| Old ATO label | New plain-English row |
|---|---|
| G1 Total sales (including GST) | Total taxable sales |
| G3 GST-free sales | Total tax-exempt sales |
| G11 Non-capital purchases (including GST) | *(dropped — not part of the generic model; see below)* |
| 1A GST on sales | Tax collected on sales |
| 1B GST on purchases | Tax paid on purchases |
| Label 9 Net GST payable/refundable | Net tax payable/refundable (grand total row) |

`G11` (non-capital purchases including GST) is dropped from the generic report — it's an ATO-specific BAS label with no universal equivalent and isn't named in spec FR-014's required row set (taxable sales, tax-exempt sales, tax collected, tax paid, net payable/refundable). `ReportId` changes from `"bas-summary"` to `"tax-summary"`; `ReportName` from `"BAS Summary"` to `"Tax Summary"`.

## Debug seeder (`DebugDataSeeder.CreateAnnualFeeAccrualAsync`)

No new fields — same shape, renamed references: reads `settings.IsTaxApplicable`/`settings.AnnualFeeTaxCode` (was `IsGstRegistered`/`AnnualFeeGstCode`), compares against `TaxCode.Taxable` (was `GstCode.Gst`), calls `TaxCalculator.SplitInclusive(settings.AnnualFee, settings.TaxRate ?? 0m)` (was the parameterless `GstCalculator.SplitInclusive`), and posts to `SystemAccounts.TaxCollectedId`/`TaxCollectedNumber` (was `GstCollectedId`/`GstCollectedNumber`). Since `Settings` is already saved by the wizard before seeding runs, whatever the user entered (tax applicable or not, rate, per-fee treatment) flows through unchanged.
