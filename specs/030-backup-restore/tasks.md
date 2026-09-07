# Tasks: Restore From Backup During First-Run Setup

**Feature dir**: `specs/030-backup-restore/` | **Branch**: `030-backup-restore` | **Size**: `oversized`
**Inputs**: `plan.md`, `spec.md`, `data-model.md`, `research.md`, `contracts/` (README, backup-service-contracts, first-run-flow-contract, backup-envelope-schema)

Line format: `- [ ] **T###** [P?] [US#] Description · exact/file/path`
`[P]` = independent of the other tasks in its wave (different file, no incomplete dependency). `[US#]` maps to a user story.

> **Already delivered ahead of task generation** — commit `7d2cc93` ("fix(030): close the .sfbak backup format drift") implemented the bulk of **User Story 4** (the `.sfbak` format completeness pass). Those tasks (T029–T034) are retained for traceability and are checked off with the commit noted. All of US1, US2, US3, the FR-019 full-semver check, and docs remain to do.

---

## Phase 1: Setup

Shared tooling prerequisite for the native Save dialog (US2). No baseline "build is green" task — the branch already builds.

**Wave 1 — single task:**

- [ ] **T001** Add the `CommunityToolkit.Maui` dependency: `<PackageVersion Include="CommunityToolkit.Maui" Version="…" />` in `Directory.Packages.props`, `<PackageReference Include="CommunityToolkit.Maui" />` in `src/StageFright.App/StageFright.App.csproj` (no `Version` attr — central management), and `builder.UseMauiCommunityToolkit()` in `MauiProgram.CreateMauiApp` · `Directory.Packages.props`, `src/StageFright.App/StageFright.App.csproj`, `src/StageFright.App/MauiProgram.cs`

---

## Phase 2: Foundational (BLOCKS US1, US2, US4)

Shared contracts, value types, platform seams, and the version-check swap that every story below builds on. No user-story work starts until this phase is done.

**Wave 1 — independent (different new files):**

- [ ] **T002** [P] `BackupSchema` static class — `const string CurrentSchemaVersion = "1.2.0"`; `bool IsRestorable(string? fileVersion)` = `false` when null/empty/unparseable **or** strictly greater than `CurrentSchemaVersion` on any semver component (a newer same-major build included), `true` when equal or older (FR-019) · `src/StageFright.Core/BackupSchema.cs`
- [ ] **T003** [P] `BackupVerificationException` — `IReadOnlyList<string> Discrepancies`, `string FilePath`, `DateTime Timestamp`, `Guid CorrelationId`; ctor shape mirrors `ImportException` (FR-023) · `src/StageFright.Core/Exceptions/BackupVerificationException.cs`
- [ ] **T004** [P] `BackupVerificationResult` — `sealed record (bool Passed, IReadOnlyList<string> Discrepancies, string FilePath)` (data-model §6). Note `CreateBackupAsync` only ever returns this with `Passed = true` (verification failure throws `BackupVerificationException`), so the UI's failure state is driven by the caught exception's `Discrepancies`, not by a `Passed = false` result · `src/StageFright.Core/Modules/Settings/Backup/BackupVerificationResult.cs`
- [ ] **T005** [P] `IRecoveryCopyStore` contract — `string GetRecoveryDirectory()`, never-throw shape like `ILanguagePreferenceStore` (FR-015) · `src/StageFright.Core/Contracts/IRecoveryCopyStore.cs`
- [ ] **T006** [P] `IBackupDestinationPicker` contract — `Task<BackupDestinationResult> SaveAsync(string suggestedFileName, Stream content, CancellationToken)`; plus `BackupDestinationResult` record (`Cancelled` / `Saved(path)` factories) in its own file (FR-009) · `src/StageFright.Core/Contracts/IBackupDestinationPicker.cs`, `src/StageFright.Core/Contracts/BackupDestinationResult.cs`
- [ ] **T007** [P] `BackupFileNameBuilder` static — `const string FallbackBaseName = "StageFright"`; `string Build(string? organisationName, DateOnly date)` → `"<sanitised org> backup <yyyy-MM-dd>.sfbak"`, literal word `backup` always present (Verbatim Constraint), `Path.GetInvalidFileNameChars()` stripped, whitespace runs collapsed + trimmed, blank/whitespace org → fallback, date formatted `InvariantCulture "yyyy-MM-dd"` (FR-010) · `src/StageFright.Core/Modules/Settings/BackupFileNameBuilder.cs`

**⟶ Wait for Wave 1 to finish, then:**

**Wave 2 — MAUI seam implementations (independent; each depends on a Wave-1 contract):**

- [ ] **T008** [P] `MauiRecoveryCopyStore : IRecoveryCopyStore` — returns `Path.Combine(FileSystem.AppDataDirectory, "recovery")`, directory created on demand · `src/StageFright.App/MauiRecoveryCopyStore.cs`
- [ ] **T009** [P] `MauiBackupDestinationPicker : IBackupDestinationPicker` — delegates to `CommunityToolkit.Maui.Storage.FileSaver.Default.SaveAsync(...)`; `IsSuccessful` → `Saved(result.FilePath)`, user dismiss → `Cancelled`, platform error wrapped as `DataAccessException` before returning into Core · `src/StageFright.App/MauiBackupDestinationPicker.cs`

**⟶ Wait for Wave 2 to finish, then:**

**Wave 3 — wire-up + version swap (different files):**

- [ ] **T010** [P] Register `IRecoveryCopyStore`→`MauiRecoveryCopyStore` and `IBackupDestinationPicker`→`MauiBackupDestinationPicker` as singletons in `MauiProgram.RegisterCoreServices` (beside the existing backup-service block) · `src/StageFright.App/MauiProgram.cs`
- [ ] **T011** [P] Swap `BackupService.ValidateVersion` to call `BackupSchema.IsRestorable`; remove `SupportedMajorVersion`; reword `Validation_Backup_UnsupportedSchemaVersion` to the "update the application and retry" message; empty/unparseable still rejected; update the `IBackupService.ImportAsync` XML doc comment ("major-version check" → "full semantic-version check") (FR-004, FR-019) · `src/StageFright.Core/Contracts/IBackupService.cs`, `src/StageFright.Core/Modules/Settings/BackupService.cs`, `src/StageFright.Core/Modules/Localization/Resources/ValidationResource.resx`

**⟶ Wait for Wave 3 to finish, then:**

**Wave 4 — single task (same file as T011):**

- [ ] **T012** `BackupService.ImportAsync` — inject `IRecoveryCopyStore`; write the pre-restore recovery copy (`StageFright-Recovery-<yyyyMMdd-HHmmss>.sfbak`) into `GetRecoveryDirectory()` via the existing `ExportAsync` path on **every** restore (first-run included), replacing `GenerateCheckpointPath`; standardise the term to "pre-restore recovery copy" in code comments and update the `IBackupService` interface XML doc ("pre-import checkpoint" → "pre-restore recovery copy") (FR-015) · `src/StageFright.Core/Contracts/IBackupService.cs`, `src/StageFright.Core/Modules/Settings/BackupService.cs`

**Checkpoint:** Foundational plumbing exists and is registered; the version check rejects newer files; restores write a durable recovery copy. No user-visible behaviour yet.

---

## Phase 3: User Story 1 — Incoming treasurer restores from a backup on first run (P1) 🎯 MVP

**Goal:** A clean install can be populated from a `.sfbak` file during first-run setup — a pre-wizard screen after `/language-select`, a checkbox, a file pick, a contents summary, confirm, restore, restart advice — with cancel/failure falling back to the normal wizard.

**Independent Test:** On a clean install: launch → choose a language → tick "restore from backup" → choose a valid `.sfbak` → confirm the summary → verify a restart-required screen with no continue path; restart → app opens on `/dashboard` (not the wizard), fully populated, setup treated as complete. Separately: cancel at the summary → `/setup` is available, nothing changed.

### Tests (write first, must fail)

**Wave 1 — independent (different test files):**

- [ ] **T013** [P] [US1] `FirstRunRestoreScreenTests` — `#restore-from-backup` is a plain checkbox, unchecked by default; `#restore-file` shown only when checked; file → `.first-run-restore-summary` (per-type counts `>0`, `GeneratedAt` local, originating `ApplicationVersion`); `#confirm-restore` → success routes to `/restart-required`; `#cancel-restore` clears manifest+file, nothing changed; `#continue-setup` → `/setup`; corrupt file and newer-version file → `.first-run-restore-error`, screen stays, DB untouched (FR-001–FR-005, FR-007, FR-008) · `tests/StageFright.UI.Tests/Pages/Setup/FirstRunRestoreScreenTests.cs`
- [ ] **T014** [P] [US1] Extend `AppRoutingTests` — setup incomplete + no language preference → `/language-select`; setup incomplete + preference recorded → `/first-run-restore` (never `/setup` directly); setup complete → unchanged (`/dashboard`) (FR-001, FR-006) · `tests/StageFright.UI.Tests/AppRoutingTests.cs`
- [ ] **T015** [P] [US1] `FirstRunRestoreJourneyTests` (integration, `_Integration` suffix) — confirmed first-run restore is all-or-nothing (partial failure ⇒ exact pre-restore state); after success `IsSetupCompleteAsync()` is true, next launch routes to `/dashboard` not the wizard, and the restored `Settings.LanguageCode` wins over the no-database language preference (FR-005, FR-006, Edge Case "language recorded outside the database") · `tests/StageFright.Integration.Tests/Scenarios/FirstRunRestoreJourneyTests.cs`

### Implementation

**Wave 1 — independent (different new files):**

- [ ] **T016** [P] [US1] `BlankLayout` + `RestartRequiredScreen` + `RestartRequiredScreenTests` — (1) `BlankLayout.razor` + `.razor.cs` in `src/StageFright.UI/Layout/`: a `LayoutComponentBase` that wraps `@Body` in the same `<CultureProvider><ThemeProvider>` chrome as `ShellLayout` but renders **no** `<nav class="shell-sidebar">`, no sidebar links, and no theme toggle — a deliberately chrome-free shell so a terminal screen has no navigation surface (FR-007); paired code-behind, no `@code` block. (2) `RestartRequiredScreen.razor` + `.razor.cs`: `@page "/restart-required"`, `@layout BlankLayout`, `.restart-required` root, a single "close and reopen the application" instruction, and **no** continue / go-to-dashboard / retry control of its own; paired code-behind, no `@code` block. (3) `RestartRequiredScreenTests` (bUnit): renders `RestartRequiredScreen` and asserts the instruction is present and **no** `<a>` / `<button>` / `NavLink` routes to `/dashboard`, `/setup`, or `/first-run-restore`; a companion test renders `BlankLayout` with a stub `Body` fragment and asserts it emits no `.shell-sidebar` and no sidebar `NavLink`s (FR-007) · `src/StageFright.UI/Layout/BlankLayout.razor`, `src/StageFright.UI/Layout/BlankLayout.razor.cs`, `src/StageFright.UI/Pages/Setup/RestartRequiredScreen.razor`, `src/StageFright.UI/Pages/Setup/RestartRequiredScreen.razor.cs`, `tests/StageFright.UI.Tests/Pages/Setup/RestartRequiredScreenTests.cs`
- [ ] **T017** [P] [US1] Add first-run-restore + restart-screen keys to the **neutral** `SetupResource.resx` (other cultures fall back key-by-key): checkbox label, file-pick prompt, summary field labels, confirm/cancel/continue captions, error text, advancing-progress text, restart instruction · `src/StageFright.UI/Resources/Strings/SetupResource.resx`

**⟶ Wait for Wave 1 to finish, then:**

**Wave 2 — single task (depends on the `/restart-required` route and the resx keys):**

- [ ] **T018** [US1] `FirstRunRestoreScreen.razor` + `.razor.cs` — `@page "/first-run-restore"`, `ShellLayout`; `#restore-from-backup` plain `<input type="checkbox">` (Verbatim Constraint — the one deliberate exception to the RadzenSwitch rule), unchecked by default; `#restore-file` `<InputFile accept=".sfbak">` gated on the checkbox, copies the pick to a temp `.sfbak` then `GetManifestAsync`; `.first-run-restore-summary`; `#confirm-restore` runs `ImportAsync` on `Task.Run` with a `Progress<string>` driving `.first-run-restore-progress` (mirrors `setup-seeding-overlay`); success → `Nav.NavigateTo("/restart-required")`; `#cancel-restore`; `#continue-setup` (enabled while unchecked) → `/setup`; `.first-run-restore-error` on invalid/corrupt/newer-version, DB untouched, `#continue-setup` still available; all text via `IStringLocalizer<SetupResource>` (FR-001, FR-002, FR-003, FR-005, FR-007, FR-008) · `src/StageFright.UI/Pages/Setup/FirstRunRestoreScreen.razor`, `src/StageFright.UI/Pages/Setup/FirstRunRestoreScreen.razor.cs`

**⟶ Wait for Wave 2 to finish, then:**

**Wave 3 — route the first-run flow through the new screen (different existing files):**

- [ ] **T019** [P] [US1] `App.razor.cs` `OnInitializedAsync` — when `!Diagnostics.HasStartupError` and `!await SetupService.IsSetupCompleteAsync()`, target `= LanguagePreferenceStore.Get()` is blank ? `/language-select` : `/first-run-restore` (was `/setup`); `/setup` is never the direct first-run target (FR-001) · `src/StageFright.UI/App.razor.cs`
- [ ] **T020** [P] [US1] `FirstRunLanguageScreen.razor.cs` `HandleConfirmAsync` — non-seed branch navigates to `/first-run-restore` instead of `/setup`; the `_seedWithTestData` branch (`SeedSampleDataAndNavigateAsync` → `/dashboard`) is unchanged (FR-001) · `src/StageFright.UI/Pages/Setup/FirstRunLanguageScreen.razor.cs`

**Checkpoint:** US1 works end to end — a fresh install can restore from a backup during first-run and is told to restart; cancel/failure leaves the wizard available and no data changed. The MVP is demoable.

---

## Phase 4: User Story 2 — Outgoing treasurer creates a verified backup to a chosen location and name (P2)

**Goal:** From Settings, the treasurer picks a folder + filename (defaulted to `<org> backup <yyyy-MM-dd>.sfbak`) via the OS-native Save dialog; the file is written, immediately read back from disk, and its per-record-type counts verified against the just-captured live data and the file's own recorded counts; they see "verified" or "failed — do not rely on this file"; backup creation is audited; the unencrypted-file notice is shown. The Settings restore path adopts the same restart advice.

**Independent Test:** In a set-up app, start a backup → suggested filename is `<org> backup <date>.sfbak` → change the folder and (optionally) the name → complete → exactly one file at the chosen path holding the full dataset, and the app reports "verified" (read back, counts match). Truncate/corrupt that file and re-run the same operation → reported as failed. Successful **restore** from Settings → routes to `/restart-required`.

### Tests (write first, must fail)

**Wave 1 — independent (different test files):**

- [ ] **T021** [P] [US2] `BackupFileNameBuilderTests` — exact format, literal word `backup` always present, invalid-char removal, whitespace collapse, blank/whitespace org → `StageFright backup <date>.sfbak`, `yyyy-MM-dd` even under a comma-decimal / non-Gregorian `CultureInfo` (restore ambient culture in `finally`) (FR-010, SC-006) · `tests/StageFright.Core.Tests/Modules/Settings/BackupFileNameBuilderTests.cs`
- [ ] **T022** [P] [US2] `BackupSchemaTests` — equal & older versions restorable; newer major / newer minor / newer patch / newer same-major build all rejected; null / empty / `"x.y"` / non-numeric rejected (FR-019, SC-005) · `tests/StageFright.Core.Tests/Modules/Settings/BackupSchemaTests.cs`
- [ ] **T023** [P] [US2] `BackupServiceTests` additions — `CreateBackupAsync` happy path returns `Passed` + writes an `AuditAction.Export` entry; read-back count mismatch / internally-inconsistent counts / unreadable file → `BackupVerificationException` carrying discrepancies, file left on disk, **not** reported successful; user cancels the Save dialog → `OperationCanceledException`, no file, no audit; write/IO failure → `DataAccessException`, no partial/empty file; restore-confirmation counts and post-write-check counts come from one shared helper (FR-009, FR-017, FR-021–FR-024, SC-009) · `tests/StageFright.Core.Tests/Modules/Settings/BackupServiceTests.cs`
- [ ] **T024** [P] [US2] `BackupRestoreTabTests` (bUnit) — `.backup-unencrypted-notice` rendered; Create button calls `IBackupService.CreateBackupAsync` and a user cancel is a silent no-op; `.backup-verify-result` shows "verified" when `CreateBackupAsync` returns `Passed`, and "failed — do not rely on this file" + the caught `BackupVerificationException.Discrepancies` when it throws; successful restore navigates to `/restart-required` (FR-007, FR-018, FR-020) · `tests/StageFright.UI.Tests/Pages/Settings/BackupRestoreTabTests.cs`
- [ ] **T040** [P] [US2] `SettingsBackupJourneyTests` (integration, `_Integration` suffix) — drive `BackupRestoreTab` with a fake `IBackupDestinationPicker` and a real `BackupService` + real SQLite: Create → file written to the fake path with the `BackupFileNameBuilder` default name → read back → `BackupVerificationResult.Passed`, an `AuditAction.Export` row exists; then truncate the written file and re-run → `.backup-verify-result` shows the failure state; then a Settings-path restore of a valid file → navigation to `/restart-required`. Covers the US2 user journey end-to-end per constitution §11.2/§11.4/§11.5 (FR-007, FR-009, FR-017, FR-018, FR-021–FR-024) · `tests/StageFright.Integration.Tests/Scenarios/SettingsBackupJourneyTests.cs`

### Implementation

**Wave 1 — single task (the service; one file pair):**

- [ ] **T025** [US2] `BackupService` + `IBackupService` — add `Task<BackupVerificationResult> CreateBackupAsync(CancellationToken)`: serialise the snapshot to a stream, hand it to `IBackupDestinationPicker.SaveAsync` with the `BackupFileNameBuilder.Build(orgName, DateOnly.FromDateTime(DateTime.Now))` suggested name, re-open the written file from disk, recompute per-record-type counts and assert three ways (re-read collection lengths == re-read `EntityCounts`; re-read `EntityCounts` == counts of the snapshot captured for this backup, archived rows included; `GeneratedAt` + originating version parse & present), write an `AuditAction.Export` audit entry on pass, throw `BackupVerificationException` (message: the file must not be relied upon) on any failure with the file left on disk, else return `BackupVerificationResult { Passed = true }`; `ExportAsync` also runs the same read-back check so a recovery copy is never silently bad; factor the count computation into one helper shared with `GetManifestAsync`; refresh the `IBackupService` XML doc comments to describe `CreateBackupAsync`, the retained internal `ExportAsync`, and the read-back verification (FR-009, FR-010, FR-017, FR-021–FR-024) · `src/StageFright.Core/Modules/Settings/BackupService.cs`, `src/StageFright.Core/Contracts/IBackupService.cs`

**⟶ Wait for Wave 1 to finish, then:**

**Wave 2 — Settings UI (different files):**

- [ ] **T026** [P] [US2] `BackupRestoreTab.razor` + `.razor.cs` — Create button calls `CreateBackupAsync` (native Save dialog + default filename; user-cancel = silent no-op, not an error); `.backup-unencrypted-notice` alert near Create (FR-020); `.backup-verify-result` renders the `BackupVerificationResult`; a successful **restore** now `Nav.NavigateTo("/restart-required")` instead of the inline success message (FR-007, FR-018) · `src/StageFright.UI/Pages/Settings/BackupRestoreTab.razor`, `src/StageFright.UI/Pages/Settings/BackupRestoreTab.razor.cs`
- [ ] **T027** [P] [US2] Add the unencrypted-file notice text and the verified / "failed — do not rely on this file" text to the **neutral** `SettingsResource.resx` (FR-020) · `src/StageFright.UI/Resources/Strings/SettingsResource.resx`

**Checkpoint:** US2 works — a verified, self-checked backup written to a user-chosen location and name, audited, with the unencrypted notice; Settings restore adopts the mandatory-restart screen. Backup + restore behaviour is now consistent across first-run and Settings (FR-018).

---

## Phase 5: User Story 3 — A backup moves between operating systems without data change (P2)

**Goal:** Prove a `.sfbak` create→restore round trip is byte-faithful across OS, regional format, and time zone. This is a correctness guarantee on the format (the format work landed in `7d2cc93`; the shared `DateTime` `[ProtoMember]` default is already in place), so it ships as a test with no new production code unless the test finds a real defect.

**Independent Test:** Create a backup from a representative multi-year, mid-size-group dataset; restore it into a fresh database while the restore half runs under a swapped `CultureInfo` (comma decimal separator, different date order) and `TimeZoneInfo`; assert the restored data is field-for-field identical to the source — records, values, `DateTime` instants/days, archived state, per-type counts.

### Tests (write first, must fail)

**Wave 1 — single task:**

- [ ] **T028** [US3] `CrossPlatformRoundTripTests` (integration; lives in `StageFright.Integration.Tests`, not `UI.Tests`, per the culture-mutation-race note; ambient culture restored in `finally`) — representative dataset (a few hundred members, thousands of fee/payment/GL rows, archived rows, `JournalEntry` rows, a finalised **and** a soft-deleted draft `BankReconciliation` with lines, non-default `CurrencyCode` / `LanguageCode` / `FinancialYearStartMonth` / tax config + `TaxEntryMode`); `ExportAsync` then `ImportAsync` into a fresh in-memory SQLite connection with `CultureInfo.CurrentCulture`/`CurrentUICulture` and `TimeZoneInfo` swapped for the restore; assert field-for-field equality incl. counts, values, `DateTime.Kind`/instants, archived flags; no timing assertion (FR-013, FR-014, FR-016, SC-002, SC-003) · `tests/StageFright.Integration.Tests/Scenarios/CrossPlatformRoundTripTests.cs`

### Implementation

- No production tasks. If T028 fails, treat the offending DTO member's wire form as the defect and fix it there (`src/StageFright.Core/Modules/Settings/Backup/*BackupDto.cs`).

**Checkpoint:** An automated create→restore cycle under a swapped locale and time zone reproduces the source dataset exactly and fails on any divergence.

---

## Phase 6: User Story 4 — The backup captures the complete current state (P3)

**Goal:** No organisation setting or record type is silently dropped on backup/restore — currency, display language, financial-year start, tax applicability/rate/per-fee codes, tax-entry mode, and every GL journal entry and bank reconciliation (with lines), archived rows included; and a restored older database is accepted by startup rather than treated as unconfigured.

**Independent Test:** Set up an app with non-default currency, language, financial-year start, tax config and tax-entry mode; add journal entries and a finalised bank reconciliation; back up; restore into a fresh database and restart; assert every setting and record is present and identical, and startup treats the restored data as configured.

> **T029–T034 were implemented in commit `7d2cc93`** and are checked off; they remain here for the requirement→task map. Only T035 is outstanding.

### Implementation

**Wave 1 — delivered in `7d2cc93` (retained for traceability):**

- [x] **T029** [US4] `JournalEntryBackupDto`, `BankReconciliationBackupDto`, `ReconciliationLineBackupDto` — 1:1 with their entities, one class per file (FR-011) · `src/StageFright.Core/Modules/Settings/Backup/{JournalEntry,BankReconciliation,ReconciliationLine}BackupDto.cs` — *done in `7d2cc93`*
- [x] **T030** [US4] `BackupEnvelope` — append-only `[ProtoMember]` `24`/`25`/`26` collections + `EntityCounts` keys `JournalEntries` / `BankReconciliations` / `ReconciliationLines`; `DeserializeAndValidate` normalises the new nulls to `[]`; the three keys are **not** added to `ValidateCompleteness` so older files still restore (FR-011, FR-019) · `src/StageFright.Core/Modules/Settings/Backup/BackupEnvelope.cs`, `src/StageFright.Core/Modules/Settings/BackupService.cs` — *done in `7d2cc93`*
- [x] **T031** [US4] `SettingsBackupDto` +12 `[ProtoMember]`s (`10`, `19`–`29`) with entity-default fallbacks (`[DefaultValue(true)]` where a real `false` must round-trip); `FeeBackupDto.TaxCode` (`10`); `TransactionBackupDto.TaxCode` (`12`) + `JournalEntryId` (`13`) (FR-011, FR-012, FR-016) · `src/StageFright.Core/Modules/Settings/Backup/{Settings,Fee,Transaction}BackupDto.cs` — *done in `7d2cc93`*
- [x] **T032** [US4] `BackupSnapshot` +3 `IReadOnlyList<>` members; `BackupRepository.GetFullSnapshotAsync` reads them (`IgnoreQueryFilters().AsNoTracking()` for the reconciliation pair, plain `AsNoTracking()` for journal entries); `UpsertSnapshotAsync` upserts them in FK-safe order (JournalEntries before Transactions; Accounts + Transactions before BankReconciliations/ReconciliationLines; ReconciliationLines last) (FR-011, FR-016) · `src/StageFright.Core/Modules/Settings/Backup/BackupSnapshot.cs`, `src/StageFright.Data/Repositories/BackupRepository.cs` — *done in `7d2cc93`*
- [x] **T033** [US4] `BackupDtoFieldParityTests` — reflection guard: every persisted-entity scalar has a matching backup-DTO property (explicit allow-list for derived/navigation members) so a future entity field cannot silently fall out of the backup (SC-008) · `tests/StageFright.Core.Tests/Modules/Settings/BackupDtoFieldParityTests.cs` — *done in `7d2cc93`*
- [x] **T034** [US4] Round-trip coverage for the 3 finance types + the 12 settings fields + an archived draft reconciliation across `BackupServiceTests`, `BackupImportTests`, `V9_BackupRestoreTests` (schema assertion → `1.2.0`) (FR-011, FR-012, FR-016) · `tests/StageFright.Core.Tests/…`, `tests/StageFright.Data.Tests/BackupImportTests.cs`, `tests/StageFright.Integration.Tests/Scenarios/V9_BackupRestoreTests.cs` — *done in `7d2cc93`*

**Wave 2 — outstanding:**

- [ ] **T035** [P] [US4] Extend `V9_BackupRestoreTests` — (a) build a synthetic pre-`1.2.0` envelope in-test (`new BackupEnvelope { SchemaVersion = "1.1.0", … }` with the three new collections omitted; the create path can no longer emit an old version), serialise it, restore it, and assert `ImportAsync` succeeds, the three new collections come back empty, the new `Settings` fields take their entity defaults, and the next `App` routing decision (a restored `Settings` row is present) targets `/dashboard`, not first-run; (b) restore a current `1.2.0` file with non-default currency / language / FY-start / tax config + `TaxEntryMode` + journal entries + a finalised reconciliation and assert every one is present and identical after a simulated restart (FR-012 acceptance 4, FR-019 older-version-accepted, FR-016) · `tests/StageFright.Integration.Tests/Scenarios/V9_BackupRestoreTests.cs`

**Checkpoint:** The backup format is complete and stays complete (parity guard), older files restore and are accepted by startup, and every configured setting survives a round trip.

---

## Phase 7: Polish

Cross-cutting docs and a full-suite validation against the Success Criteria. Doc updates are required in-task per the CLAUDE.md Spec & Docs Workflow.

**Wave 1 — independent (different files):**

- [ ] **T036** [P] `docs/ARCHITECTURE.md` — backup paragraph: correct the entity-count wording to the real `BackupSnapshot` member count after adding `JournalEntry`, `BankReconciliation`, `ReconciliationLine` (confirm the exact figure against `BackupSnapshot.cs` / `BackupRepository.GetFullSnapshotAsync` — currently 20 members: 19 collections plus the `Settings` singleton — do **not** copy a number from `plan.md` without checking); note first-run reachability via `/first-run-restore` and the post-write read-back verification · `docs/ARCHITECTURE.md`
- [ ] **T037** [P] `docs/SETUP.md` — first-run description now mentions the `/first-run-restore` screen and the restore-then-restart path; the pre-restore recovery copy is written under `FileSystem.AppDataDirectory/recovery` · `docs/SETUP.md`
- [ ] **T038** [P] `CLAUDE.md` — Navigation section: first-run detection reaches `/setup` **via** `/first-run-restore`, not directly, and `/restart-required` renders under a new chrome-free `BlankLayout` (no sidebar/nav) rather than `ShellLayout`, so a post-restore user has no navigation surface (FR-007); add `CommunityToolkit.Maui` (`FileSaver`) to the tech-stack notes; record the first-run restore checkbox as a second sanctioned exception to the RadzenSwitch toggle rule (alongside the wizard's Light/Dark `<select>`) · `CLAUDE.md`
- [ ] **T041** [P] SC-001 acceptance walk-through — on a clean install, a scripted or manually-timed run of language → tick restore → pick file → confirm → restart → dashboard, asserting the interaction completes in under 2 minutes with no reference to documentation, and recording the elapsed interaction time in the test / PR notes (SC-001) · `tests/StageFright.Integration.Tests/Scenarios/FirstRunRestoreJourneyTests.cs` (assertion + timing note)

**⟶ Wait for Wave 1 to finish, then:**

**Wave 2 — single task:**

- [ ] **T039** Full-rebuild validation — `dotnet build -t:Rebuild` (judge warnings from a full rebuild, not incremental) then `dotnet test` (no `--no-build`); confirm 0 warnings / 0 failures and walk SC-001…SC-009 against the delivered behaviour; report build + test results · repo root

---

## Dependencies & Execution Order

**Phase order:** Phase 1 Setup → Phase 2 Foundational → Phase 3 (US1) → Phase 4 (US2) → Phase 5 (US3) → Phase 6 (US4) → Phase 7 Polish.
Setup and Foundational block every story. Stories are independently testable once Foundational is done; US1 is the MVP. US3 and US4 depend only on Foundational (and the format work already in `7d2cc93`), so they can be built in parallel with US1/US2 by a fan-out host. Polish waits for whichever stories are in scope for the release.

**Within phases:**

- **Phase 1 — Setup:** T001 alone.
- **Phase 2 — Foundational:** Wave 1 `T002–T007` (independent new files) ⟶ Wave 2 `T008, T009` (MAUI impls, need the Wave-1 contracts) ⟶ Wave 3 `T010, T011` (DI in `MauiProgram`; version swap in `BackupService` — different files) ⟶ Wave 4 `T012` (same file as T011).
- **Phase 3 — US1:** Tests `T013, T014, T015` (independent) ⟶ Impl Wave 1 `T016, T017` (new screen file pair; resx) ⟶ Wave 2 `T018` (`FirstRunRestoreScreen` — needs the `/restart-required` route + resx keys) ⟶ Wave 3 `T019, T020` (route into it; `App.razor.cs` and `FirstRunLanguageScreen` — different files).
- **Phase 4 — US2:** Tests `T021, T022, T023, T024, T040` (independent) ⟶ Impl Wave 1 `T025` (`BackupService` + `IBackupService`) ⟶ Wave 2 `T026, T027` (`BackupRestoreTab` pair; `SettingsResource.resx` — different files).
- **Phase 5 — US3:** `T028` alone; no production task unless it fails.
- **Phase 6 — US4:** `T029–T034` already complete (`7d2cc93`) ⟶ `T035` alone.
- **Phase 7 — Polish:** Wave 1 `T036, T037, T038, T041` (three doc files + the SC-001 walk-through) ⟶ Wave 2 `T039` (full build + test, last).

**Parallel opportunities:** Foundational Wave 1 is six-way parallel; Wave 2 two-way. Each story's Tests wave is fully parallel. US1 impl Wave 3, US2 impl Wave 2, and Polish Wave 1 are each parallel across their listed files. Across stories, US3 (`T028`) and US4 (`T035`) can run alongside US1/US2 once Foundational is done.

## Requirement → Task Map

| Req | Tasks |
|---|---|
| FR-001 | T013, T018, T019, T020 |
| FR-002 | T013, T018 |
| FR-003 | T013, T018, T025 |
| FR-004 | T011, T013, T022, T023 |
| FR-005 | T015, T018 |
| FR-006 | T014, T015, T019 |
| FR-007 | T013, T016, T018, T024, T026, T040 |
| FR-008 | T013, T018 |
| FR-009 | T009, T023, T025, T040 |
| FR-010 | T007, T021, T025 |
| FR-011 | T029, T030, T031, T032, T033, T034 |
| FR-012 | T031, T034, T035 |
| FR-013 | T028 |
| FR-014 | T021, T028 |
| FR-015 | T005, T008, T012 |
| FR-016 | T028, T032, T034, T035 |
| FR-017 | T023, T025, T040 |
| FR-018 | T024, T026, T040 |
| FR-019 | T002, T011, T022, T030, T035 |
| FR-020 | T024, T026, T027 |
| FR-021 | T023, T025, T040 |
| FR-022 | T023, T025, T040 |
| FR-023 | T003, T023, T025, T040 |
| FR-024 | T023, T025, T026, T040 |

## Success Criteria → Task Map

| SC | Tasks | Notes |
|---|---|---|
| SC-001 | T013, T015, T041 | T041 is the explicit "under 2 minutes, unaided" acceptance check |
| SC-002 | T028, T034, T035 | full-fidelity round trip |
| SC-003 | T028 | cross-OS / cross-culture / cross-timezone round trip |
| SC-004 | T013, T015, T023 | cancel or failure leaves the DB unchanged |
| SC-005 | T011, T022, T023 | invalid / corrupt / incompatible detected before any DB change |
| SC-006 | T007, T021 | default filename identifies org + date, always valid |
| SC-007 | T013, T016, T018, T024 | restart advisory shown; no dashboard on pre-restore data |
| SC-008 | T033 (done `7d2cc93`) | reflection parity guard over every persisted entity + `SettingsBackupDto` |
| SC-009 | T023, T025 | every "successful" backup was read back and its counts verified |
