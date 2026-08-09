# Tasks: Audit Trail Retention Fix & Customization

**Input**: `plan.md`, `data-model.md`, `contracts/audit-retention-contract.md`, `spec.md`

## Wave 1 — Foundational (interface + entity)

- [x] **T001** [P] Add `PurgeOlderThanAsync(DateTime cutoff, CancellationToken ct = default)` to `IAuditTrailService` + `src/StageFright.Core/Contracts/IAuditTrailService.cs`
- [x] **T002** [P] Add `AuditRetentionYears` (int, default 1) property to `Settings` entity + `src/StageFright.Core/Entities/Settings.cs`

**⟶ Wait for T001, T002**

## Wave 2 — Depends on Wave 1

- [x] **T003** [P] EF config: `HasDefaultValue(1)` for `AuditRetentionYears` + `src/StageFright.Data/Configurations/SettingsConfiguration.cs`
- [x] **T004** [P] Validate `AuditRetentionYears` (1–7) in `SettingsService.SaveAsync` + `src/StageFright.Core/Modules/Settings/SettingsService.cs`
- [x] **T005** [P] Add `AuditRetentionYears = 1` to `SetupRequest` record + `src/StageFright.Core/Modules/Settings/SetupRequest.cs`
- [x] **T006** [P] Fix `MauiProgram` audit purge DI resolution (`GetRequiredService<IAuditTrailService>`) + retention-driven cutoff, success-only log + `src/StageFright.App/MauiProgram.cs`
- [x] **T007** [P] Add `AuditRetentionYears` (`[Range(1,7)]`, default 1) to `SetupFormModel` + `src/StageFright.UI/Pages/Setup/SetupFormModel.cs`
- [x] **T008** [P] Add `AuditRetentionYears` (`ProtoMember(18)`) to `SettingsBackupDto` + `src/StageFright.Core/Modules/Settings/Backup/SettingsBackupDto.cs`
- [x] **T009** [P] Update doc comment (cutoff is caller-supplied, not "12 months") + `src/StageFright.Core/Modules/AuditTrail/AuditTrailService.cs`

**⟶ Wait for Wave 2**

## Wave 3 — Depends on Wave 2

- [x] **T010** Validate + assign `AuditRetentionYears` from request in `SetupService.InitializeAsync`/`Validate` + `src/StageFright.Core/Modules/Settings/SetupService.cs`
- [x] **T011** [P] Update `BackupService.MapSettings`/`MapSettingsFromDto` for `AuditRetentionYears` + `src/StageFright.Core/Modules/Settings/BackupService.cs`
- [x] **T012** [P] Add retention `InputSelect` (id `auditRetentionYears`, 1–7) to Step 2 + Review step + `src/StageFright.UI/Pages/Setup/SetupWizard.razor`
- [x] **T013** [P] Add retention `InputSelect` (id `auditRetentionYears`, 1–7) + `src/StageFright.UI/Pages/Settings/GeneralSettingsTab.razor`

**⟶ Wait for Wave 3**

## Wave 4 — Depends on Wave 3

- [x] **T014** Wire `_model.AuditRetentionYears` into the `SetupRequest` built in `HandleValidSubmitAsync` + `src/StageFright.UI/Pages/Setup/SetupWizard.razor.cs`

**⟶ Wait for Wave 4**

## Wave 5 — EF Core migration (depends on Wave 1–4 compiling)

- [x] **T015** Generate migration `AddAuditRetentionYearsToSettings` via `dotnet ef migrations add` + `src/StageFright.Data/Migrations/`

**⟶ Wait for Wave 5**

## Wave 6 — Tests (parallel-safe, depends on Wave 5)

- [x] **T016** [P] New `AuditTrailServiceTests` — `LogAsync` + `PurgeOlderThanAsync` (removes-old/keeps-recent/no-entries/failure-tolerant) + `tests/StageFright.Core.Tests/Modules/AuditTrail/AuditTrailServiceTests.cs`
- [x] **T017** [P] `SettingsServiceTests`: `AuditRetentionYears` valid/boundary(1,7)/below-range/above-range + `tests/StageFright.Core.Tests/Modules/Settings/SettingsServiceTests.cs`
- [x] **T018** [P] `SetupServiceTests`: same validation at setup time + `tests/StageFright.Core.Tests/Modules/Settings/SetupServiceTests.cs`
- [x] **T019** [P] `SetupServiceIntegrationTests`: default (1) and custom retention persist correctly + `tests/StageFright.Data.Tests/Settings/SetupServiceIntegrationTests.cs`
- [x] **T020** [P] New migration test: column exists, default value 1 on existing rows + `tests/StageFright.Data.Tests/Migrations/AddAuditRetentionYearsToSettingsMigrationTests.cs`
- [x] **T021** [P] `SetupWizardTests`: dropdown present/default 1/selected value flows to request + `tests/StageFright.UI.Tests/Pages/Setup/SetupWizardTests.cs`
- [x] **T022** [P] `GeneralSettingsTabTests`: dropdown present/save round-trip/out-of-range rejected + `tests/StageFright.UI.Tests/Pages/Settings/GeneralSettingsTabTests.cs`
- [x] **T023** [P] `StartupSequenceTests`: regression test resolving the purge via a real `ServiceCollection` (mirrors `MauiProgram` registration) confirms `IAuditTrailService` resolves and purge honours a configured retention value + `tests/StageFright.Integration.Tests/Scenarios/StartupSequenceTests.cs`
- [x] **T024** [P] `BackupServiceTests`: `AuditRetentionYears` round-trips through backup export/import + `tests/StageFright.Core.Tests/Modules/Settings/BackupServiceTests.cs`

**⟶ Wait for Wave 6**

## Wave 7 — Polish

- [x] **T025** `dotnet build` + full `dotnet test` (no `--no-build`); report results
