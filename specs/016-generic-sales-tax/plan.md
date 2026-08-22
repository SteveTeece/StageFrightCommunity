# Implementation Plan: Generic International Sales Tax (Replacing ABN & GST)

**Branch**: `016-generic-sales-tax` | **Date**: 2026-08-18 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/016-generic-sales-tax/spec.md`

## Summary

Replace the Australia-specific ABN field and GST-registration model with a generic, user-configurable sales-tax model, throughout the setup wizard, the Settings page, the built-in tax summary report, the Debug-build sample-data seeder, and every affected class/field/file name in the codebase — while preserving every historical Fee/Payment/Transaction record's exact dollar amounts and giving it a valid, meaning-preserving tax label after upgrade. Technical approach: rename `GstCode`→`TaxCode` (4 values→3), `Settings.IsGstRegistered`→`IsTaxApplicable`, add `Settings.TaxRate` (nullable decimal, replacing the hardcoded `GstConstants.Rate`), generalize `GstCalculator.SplitInclusive` to take the configured rate as a parameter, rename the GST system accounts and the BAS report, and ship one EF Core migration that renames the affected columns (values are already string-backed, so historical rows keep their meaning via a value-remap `UPDATE`, not a schema rewrite) and drops `Abn`.

## Technical Context

**Language/Version**: C# 14, .NET (MAUI Blazor Hybrid) — unchanged, matches the existing solution.

**Primary Dependencies**: EF Core (SQLite provider), Blazor, Radzen.Blazor, BlazorBootstrap, QuestPDF (PDF reports), CsvHelper (CSV export) — all already in use; no new package references needed.

**Storage**: SQLite via `StageFrightDbContext` / EF Core Code-First migrations (`src/StageFright.Data/Migrations/`). One new migration renames/adds/drops the affected `Settings`, `Fee`, `Transaction`, and `Account` columns/values.

**Testing**: xUnit (`StageFright.Core.Tests`, `StageFright.Data.Tests`, `StageFright.Reports.Tests`), bUnit (`StageFright.UI.Tests`), cross-layer journey tests (`StageFright.Integration.Tests`) — all five existing test projects are touched by this feature.

**Target Platform**: Windows desktop and macOS desktop (MAUI) — unchanged.

**Project Type**: Existing multi-project MAUI Blazor Hybrid desktop application (see CLAUDE.md project layout table) — not a greenfield structure choice; this feature works entirely within the established project layout.

**Performance Goals**: N/A — no new performance-sensitive code path; tax splitting remains an O(1) decimal calculation identical in cost to today's `GstCalculator.SplitInclusive`.

**Constraints**: Every Fee/Payment/Transaction record already posted before this ships MUST keep its exact dollar amounts and balance (CLAUDE.md Finance/GL integrity, constitution §3.5/§3.6) — the migration may only rewrite the *label* describing a historical row's tax treatment, never a dollar figure. `dotnet build` and the full `dotnet test` suite (without `--no-build`) MUST be green before the task is considered complete (CLAUDE.md Build & Test Verification).

**Scale/Scope**: ~45 files identified via `grep` for GST/ABN/BAS across `src/` touch this feature (entities, enums, one static calculator/constants pair, one system-accounts registry, one EF migration + configurations, 3 repositories/services, ~6 Blazor page pairs, 1 report provider, the debug seeder), plus the mirrored set of test files.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **§3.2.1 / §4.5 One Class Per File**: Every rename (`GstCode`→`TaxCode`, `GstCalculator`→`TaxCalculator`, `GstConstants` retired, `GstSettingsTab`→`TaxSettingsTab`, `BasSummaryReportProvider`→`TaxSummaryReportProvider`, `AbnValidator`/`AbnAttribute` deleted) is a file rename/replacement, not a merge — each new type keeps its own file named after itself. **PASS**.
- **§4.7 Blazor Component Patterns**: `SetupWizard.razor`/`.razor.cs` and the renamed `TaxSettingsTab.razor`/`.razor.cs` keep their paired code-behind structure; no `@code` blocks introduced. **PASS**.
- **§3.4/§3.5/§3.6 Soft-Delete & Financial Immutability**: `Settings` remains the exempt singleton config row (no soft-delete fields touched). `Fee`/`Payment`/`Transaction` remain exempt from soft-delete and are never edited or deleted by this feature's application code — the one migration-time `UPDATE` that rewrites their `TaxCode` string value is a **one-time schema-driven relabeling of an enum's on-disk representation**, not a business-logic edit: no `DebitAmount`/`CreditAmount`/`Amount` column is touched, no row is added or removed, and the mapping is deterministic and lossless (documented in `data-model.md`). This is flagged explicitly here because it is the one place this feature comes close to the immutability gate — see Complexity Tracking below for the full justification. **PASS, with documented justification**.
- **§5 Custom Exceptions at Boundaries**: `SettingsService.SaveAsync`/`SetupService.InitializeAsync` continue to raise `ValidationException` for tax-rate/tax-code validation failures, matching today's `Abn`/GST validation pattern. **PASS**.
- **§8 Plug-in Architecture**: `ISettingsTabProvider` and `IReportProvider` contracts themselves are unchanged — only their concrete first-party implementations (`TaxSettingsTab`, `TaxSummaryReportProvider`) are renamed/updated. No plugin-facing contract breaks. **PASS**.
- **§11 Testing Standards**: Every renamed/changed type keeps (or gains) `Should_[ExpectedBehavior]_When_[Condition]` tests; the migration gets a `StageFright.Data.Tests` coverage pass verifying historical-value remap correctness. **PASS** (enforced during `speckit-tasks`/`speckit-implement`).

No unjustified violations — Complexity Tracking documents the one deliberate, justified exception above.

## Project Structure

### Documentation (this feature)

```text
specs/016-generic-sales-tax/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md         # Phase 1 output
├── quickstart.md         # Phase 1 output
└── tasks.md              # Phase 2 output (/speckit-tasks — not created by this command)
```

No `contracts/` directory: this feature does not add or change any plugin-facing interface (`ISettingsTabProvider`, `IReportProvider`, `IDataAccessProvider`, etc. are all unchanged) — only first-party implementations behind those existing contracts are renamed/updated, so there is no new contract surface to document.

### Source Code (repository root)

This feature works entirely inside the existing StageFright solution layout (see CLAUDE.md's project table) — no new projects, no structural changes. Affected areas per project:

```text
src/StageFright.Core/
├── Entities/Settings.cs                          # Abn removed; IsTaxApplicable/TaxRate/*TaxCode added
├── Entities/Fee.cs, Entities/Transaction.cs       # GstCode → TaxCode
├── Enums/GstCode.cs → Enums/TaxCode.cs            # 4 values → 3 (Taxable/TaxExempt/Excluded)
├── Modules/Finance/GstConstants.cs (deleted)      # hardcoded rate retired
├── Modules/Finance/GstCalculator.cs → TaxCalculator.cs  # takes rate as a parameter
├── Modules/Finance/SystemAccounts.cs              # GstCollected*/GstPaid* → TaxCollected*/TaxPaid*
├── Modules/Settings/AbnValidator.cs (deleted)
├── Modules/Settings/AbnAttribute.cs (deleted)
├── Modules/Settings/SetupService.cs, SetupRequest.cs, SettingsService.cs  # field renames/validation

src/StageFright.Data/
├── Migrations/                                    # one new migration: rename/add/drop columns + value remap
├── Configurations/SettingsConfiguration.cs, FeeConfiguration.cs, TransactionConfiguration.cs
├── Repositories/SettingsRepository.cs

src/StageFright.UI/Pages/
├── Setup/SetupWizard.razor(.cs), SetupFormModel.cs       # ABN step content removed; GST step → Sales Tax step
├── Settings/GstSettingsTab.razor(.cs) → TaxSettingsTab.razor(.cs)
├── Settings/GeneralSettingsTab.razor(.cs)                # ABN field/notice removed
├── Finance/RecordIncome*, ExpensePayment*                # GstCode references renamed

src/StageFright.Reports/Providers/BasSummaryReportProvider.cs → TaxSummaryReportProvider.cs

src/StageFright.App/Seeding/DebugDataSeeder.cs             # uses new Settings/TaxCode fields (FR-017)

tests/StageFright.Core.Tests/Modules/Settings/{AbnValidatorTests,AbnAttributeTests}.cs (deleted)
tests/**/*Gst*Tests.cs → tests/**/*Tax*Tests.cs            # renamed/rewritten alongside their production types
tests/StageFright.Data.Tests/                              # + new migration value-remap coverage
```

**Structure Decision**: No structural change to the solution — this is a rename-and-behavior-change feature confined to existing projects/folders, following the module ownership already established (`StageFright.Core/Modules/Finance` and `.../Settings` own the domain logic; `StageFright.Data/Repositories` centrally owns persistence per CLAUDE.md's spec-mandated deviation from pure vertical slice).

## Complexity Tracking

> Documenting the one Constitution Check item that needed justification (§3.5/§3.6 Financial Data Immutability).

| Violation (apparent) | Why Needed | Simpler Alternative Rejected Because |
|-----------------------|------------|----------------------------------------|
| Migration-time `UPDATE` rewriting the `TaxCode` string value on existing `Fee`/`Transaction` rows | The `GstCode`/`TaxCode` enum is stored as its member *name* (`HasConversion<string>()`), and this feature retires/renames enum members (`Gst`→`Taxable`, `GstFree`/`InputTaxed`→`TaxExempt`, `BasExcluded`→`Excluded`). Without rewriting the stored string, every historical row would deserialize to an undefined/error enum value the moment the C# type changes — silently breaking every report and screen that reads historical tax-coded rows. | Leaving the old enum type in place alongside the new one (so historical rows keep deserializing against the retired type) was rejected: it would mean shipping two parallel tax-code concepts indefinitely, contradicting the spec's "generic and international" goal and complicating every future read path with a legacy branch. A one-time, deterministic, dollar-amount-untouched value remap is the smaller and more honest cost — it changes a label, never a balance, and every mapping is captured in `data-model.md` for review. |
