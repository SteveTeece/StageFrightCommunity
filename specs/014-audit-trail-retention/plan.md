# Implementation Plan: Audit Trail Retention Fix & Customization

**Branch**: `014-audit-trail-retention` | **Date**: 2026-08-09 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/014-audit-trail-retention/spec.md`

## Summary

`MauiProgram.RunStartupSequence` resolves the audit purge via `scope.ServiceProvider.GetService<AuditTrailService>()` — the concrete class — but DI only registers `IAuditTrailService`, so the reference is always null, the purge is skipped, and a misleading "purge complete" line is logged anyway. The fix resolves `IAuditTrailService` (via `GetRequiredService`, so a genuine misconfiguration surfaces through the existing failure-tolerant `catch` instead of silently no-op'ing), which first requires adding `PurgeOlderThanAsync` to that interface. The purge's hardcoded 12-month cutoff is replaced with a new `Settings.AuditRetentionYears` (int, 1–7, default 1), read at startup and validated everywhere it's writable — the setup wizard (issue #291's explicit ask) and, for consistency with every other bounded first-run numeric setting in this codebase, the Settings → General tab as well.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design — no changes since first pass.*

| Principle | Assessment |
|---|---|
| §3.2.1 / §4.5 One class per file | PASS — no new types are introduced; all changes are additions to existing classes/interfaces/components, each already in its own file. |
| §3.4 / §3.5 Soft-delete everywhere (finance exempt) | N/A — `AuditTrailEntry` purge is a documented hard-delete exemption (log-record, per Constitution §3.4); unaffected by this change. `Settings` carries reserved-but-unused soft-delete fields, unchanged. |
| §4.1 Vertical slice modules | PASS — all changes stay inside the existing `AuditTrail` and `Settings` modules; no new module needed. |
| §4.7 Blazor code-behind + CSS isolation | PASS — the two touched components (`SetupWizard.razor`, `GeneralSettingsTab.razor`) already have paired `.razor.cs` files; no `@code` blocks added, no new `.razor.css` needed. |
| §5 Custom exceptions at boundaries | PASS — reuses the existing `ValidationException` for the new 1–7 range check, matching every other Settings validation rule (`SettingsService.SaveAsync`, `SetupService.Validate`). |
| §11 Exhaustive test-path coverage | PASS — plan carries unit coverage (`AuditTrailServiceTests`, `SettingsServiceTests`, `SetupServiceTests`), integration coverage (`StartupSequenceTests`, `SetupServiceIntegrationTests`), and bUnit coverage (`SetupWizardTests`, `GeneralSettingsTabTests`) for every new code path, including the DI-resolution regression itself. |
| Data model migrations (CLAUDE.md) | PASS — new `Settings.AuditRetentionYears` column ships via a proper EF Core migration (`dotnet ef migrations add`), not a hand-edited snapshot. |
| Finance/GL integrity (CLAUDE.md) | N/A — non-financial; no GL transactions touched. |

No violations — Complexity Tracking table omitted.

## Project Structure

### Documentation (this feature)

```text
specs/014-audit-trail-retention/
├── plan.md              # This file
├── research.md          # Phase 0 — 6 decisions
├── data-model.md         # Phase 1 — Settings field addition
├── contracts/
│   └── audit-retention-contract.md   # Phase 1 — interface & UI identifier contract
└── checklists/
    └── requirements.md
```

### Source code (repository root)

```text
src/StageFright.Core/
├── Contracts/
│   └── IAuditTrailService.cs            # extended: + PurgeOlderThanAsync
├── Entities/
│   └── Settings.cs                      # extended: + AuditRetentionYears (int, default 1)
└── Modules/
    ├── AuditTrail/
    │   └── AuditTrailService.cs         # doc comment only (cutoff is now caller-supplied, not "12 months")
    └── Settings/
        ├── SettingsService.cs           # extended: validate AuditRetentionYears 1-7
        ├── SetupRequest.cs              # extended: + AuditRetentionYears (default 1)
        ├── SetupService.cs              # extended: validate + assign AuditRetentionYears
        └── Backup/
            └── SettingsBackupDto.cs     # extended: + AuditRetentionYears (ProtoMember 18)
    (BackupService.cs mappers updated to carry the new field)

src/StageFright.Data/
├── Configurations/
│   └── SettingsConfiguration.cs         # extended: HasDefaultValue(1) for AuditRetentionYears
└── Migrations/
    └── <timestamp>_AddAuditRetentionYearsToSettings.cs   # new column, default 1

src/StageFright.App/
└── MauiProgram.cs                       # RunStartupSequence: resolve IAuditTrailService, compute cutoff from Settings.AuditRetentionYears (fallback 1)

src/StageFright.UI/Pages/
├── Setup/
│   ├── SetupFormModel.cs                # extended: + AuditRetentionYears (Range 1-7, default 1)
│   ├── SetupWizard.razor                # extended: retention dropdown (Step 2) + Review step line
│   └── SetupWizard.razor.cs             # extended: pass AuditRetentionYears into SetupRequest
└── Settings/
    └── GeneralSettingsTab.razor         # extended: retention dropdown (1-7 years)

tests/StageFright.Core.Tests/Modules/
├── AuditTrail/
│   └── AuditTrailServiceTests.cs        # new — first dedicated unit test for this service
└── Settings/
    ├── SettingsServiceTests.cs          # extended — AuditRetentionYears validation (in-range, boundary, out-of-range)
    └── SetupServiceTests.cs             # extended — same validation at setup time

tests/StageFright.Data.Tests/
├── Settings/SetupServiceIntegrationTests.cs   # extended — persisted default + custom retention value
└── Migrations/AddAuditRetentionYearsToSettingsMigrationTests.cs   # new — column + default value

tests/StageFright.UI.Tests/Pages/
├── Setup/SetupWizardTests.cs            # extended — retention dropdown present, default selection, value flows to request
└── Settings/GeneralSettingsTabTests.cs  # extended — retention dropdown present, save round-trip, out-of-range rejected

tests/StageFright.Integration.Tests/Scenarios/
└── StartupSequenceTests.cs              # extended — purge resolves via DI container (regression test for the #275 bug) and honours a configured retention value

tests/StageFright.Core.Tests/Modules/Settings/
└── BackupServiceTests.cs                # extended — AuditRetentionYears round-trips through backup/restore
```

**Structure Decision**: No new projects, modules, or entities. This is a contained fix + field addition inside the existing `AuditTrail` and `Settings` slices, following their established patterns exactly (bounded numeric Settings field, `ValidationException` at the service boundary, EF Core migration for the schema change, wizard step + Settings tab for the UI).
