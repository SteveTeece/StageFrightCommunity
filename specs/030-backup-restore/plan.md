# Implementation Plan: Restore From Backup During First-Run Setup

**Branch**: `030-backup-restore` | **Date**: 2026-09-07 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/030-backup-restore/spec.md`

## Summary

A brand-new install can currently only be populated by finishing the full setup wizard and then restoring from **Settings → Backup & Restore**. This feature adds a restore path to the first-run flow — a dedicated pre-wizard screen, after the display-language screen, with a checkbox to load a backup instead of configuring by hand — and closes two correctness gaps in the existing `.sfbak` capability so the handover is faithful: the backup format has fallen behind the data model (three finance record types — `JournalEntry`, `BankReconciliation`, `ReconciliationLine` — and roughly a dozen organisation-settings fields added since spec 016/028/029 are silently dropped, and `Fee.TaxCode` / `Transaction.TaxCode` / `Transaction.JournalEntryId` are missing from their backup DTOs), and there is no proof a Mac↔Windows round trip preserves data exactly. It also adds a post-write self-check to backup creation (read the file back from disk, recompute the restore-confirmation counts, verify them against the just-captured live data and the file's own recorded counts) and lets the user choose the backup's destination and filename through the operating system's native Save dialog, defaulting the name to `<organisation> backup <yyyy-MM-dd>`.

The work extends the existing `BackupService` / `BackupRepository` / `BackupEnvelope` pipeline rather than replacing it; the `.sfbak` protobuf serialisation, the pre-restore recovery copy, the atomic PK-upsert transaction, and the GL/tax structures are all unchanged. One new dependency is introduced: `CommunityToolkit.Maui` (`FileSaver`) for the native Save dialog on both the Windows and Mac Catalyst heads — the app has no save-dialog mechanism today (it writes backups straight to *My Documents*).

## Technical Context

**Language/Version**: C# 13 / .NET 10 (`net10.0`, `net10.0-windows10.0.19041.0`, `net10.0-maccatalyst`)

**Primary Dependencies**: MAUI Blazor Hybrid; `protobuf-net` 3.2.56 (`.sfbak` serialisation); EF Core 10 + SQLite; `Microsoft.Extensions.Localization`; Radzen / Blazor.Bootstrap (UI); **new:** `CommunityToolkit.Maui` (`FileSaver` — native Save As dialog)

**Storage**: SQLite (`FileSystem.AppDataDirectory/stagefright.db`); backup files are single self-contained `.sfbak` protobuf binaries at a user-chosen path; pre-restore recovery copies are `.sfbak` files written to a stable app-data location

**Testing**: xUnit v3 (`_Integration` suffix for cross-layer), bUnit (component), NSubstitute; new cross-platform round-trip test and an entity↔DTO field-parity guard test

**Target Platform**: Windows 10 (19041+) unpackaged head and Mac Catalyst 15+; OS-independence of the backup file is a hard requirement (FR-013 / FR-014)

**Project Type**: Single desktop application, layered (`App` / `Core` / `Data` / `UI` / `Reports` / `Plugins.Contracts`)

**Performance Goals**: No wall-clock target. The restore must never block the UI thread and must show continuously advancing progress; the round-trip test uses a representative multi-year, mid-size-group dataset (a few hundred members, thousands of fee/payment/GL rows) with no timing assertion.

**Constraints**: Existing `.sfbak` files must still load (append-only protobuf field numbers; new collections absent → treated as empty, never a completeness failure). No change to GL double-entry structure, the `2310`/`2320` tax accounts, `TaxCode`, or money formatting. The application never relaunches itself — the user is advised to restart. Backups stay unencrypted (FR-020).

**Scale/Scope**: 20 members in the `BackupSnapshot` (17 today — 16 collections plus the `Settings` singleton — plus 3 new: `JournalEntry`, `BankReconciliation`, `ReconciliationLine`); 20 `*BackupDto` types; ~30 files touched; one new NuGet dependency; two new first-run screens; one new Core exception; one new Core file-dialog seam.

## Constitution Check

*GATE: re-checked after Phase 1 design — still PASS.*

| Principle | Assessment |
|-----------|------------|
| §3.1 Clean Code / §3.2 SOLID | **PASS** — extends the existing backup slice along its current seams (`IBackupService`, `IBackupRepository`, `BackupEnvelope`); new responsibilities (native save dialog, post-write verification, recovery-copy location) are separate injected abstractions, not lumped onto `BackupService`. |
| §3.2.1 One class per file (NON-NEGOTIABLE) | **PASS** — each new backup DTO (`JournalEntryBackupDto`, `BankReconciliationBackupDto`, `ReconciliationLineBackupDto`), the new exception, the new seam interfaces and their MAUI implementations, and each new `.razor`/`.razor.cs` pair get their own file. |
| §3.4 Soft-delete pattern | **PASS** — export reads with `IgnoreQueryFilters()` (archived rows included, FR-011/FR-016); restore is PK-upsert, never a delete. |
| §3.5 Member & financial data preservation / §3.6 financial corrections | **PASS** — `Fee` / `Payment` / `Transaction` / `JournalEntry` remain immutable and undeleted; restore only inserts/updates by PK inside one transaction; adding `Fee.TaxCode` / `Transaction.TaxCode` / `Transaction.JournalEntryId` to the DTOs *stops* silent financial data loss. |
| §4.3 Settings System | **PASS** — the existing **Backup & Restore** built-in tab in `SettingsPage.razor` is reused; the first-run restore screen follows the established pre-wizard screen pattern (spec 029's `/language-select`). |
| §4.7.1 Code-behind (MANDATORY) | **PASS** — new screens (`FirstRunRestoreScreen`, `RestartRequiredScreen`) ship as paired `.razor` + `.razor.cs`; no `@code` blocks. |
| §4.7.2 CSS isolation | **PASS** — no component-scoped styling expected; the overlay/blocking styles reuse existing global classes (`setup-seeding-overlay` pattern). |
| §5.2 Custom exceptions / §5.3 exception-boundary translation | **PASS** — new `BackupVerificationException` in `StageFright.Core/Exceptions/`; file-I/O and picker failures are caught and re-thrown as project exceptions (`DataAccessException` / `ImportException` / `BackupVerificationException`) before crossing a layer boundary, matching the existing `ExportAsync` catch. |
| §6 Logging / OpenTelemetry | **PASS** — the existing `ILogger<BackupService>` structured-logging calls are extended; backup creation and restore each write an `AuditTrailEntry` (FR-017) using the already-defined `AuditAction.Export` / `AuditAction.Import`. |
| §7.1 Technology Stack / §7.2 permitted libraries | **PASS with a note** — `CommunityToolkit.Maui` is a new dependency. It is the standard, .NET-team-maintained MAUI answer for a native Save dialog on Windows + Mac Catalyst and is added centrally via `Directory.Packages.props` + a `<PackageReference>` in `StageFright.App.csproj` per the CLAUDE.md package rule, plus `.UseMauiCommunityToolkit()` in `MauiProgram`. No Complexity Tracking entry is required — this is a permitted-library addition, not a principle violation. |
| §7.3 Prohibited (no custom JS) | **PASS** — all logic is C#/Blazor; `<InputFile>` (restore selection) and `FileSaver` (backup destination) are framework/package features, not hand-written JS. |
| §11 Testing Standards (coverage NON-NEGOTIABLE) | **PASS** — every reachable path is covered: first-run restore happy path, cancel-at-summary, corrupt file, newer-version rejection (incl. newer same-major build), older-version acceptance, verification-failure on a truncated/corrupted file, cross-platform + cross-culture/timezone round trip, and an entity↔DTO field-parity guard test (SC-008). |

No violations → **Complexity Tracking omitted.**

## Project Structure

### Documentation (this feature)

```text
specs/030-backup-restore/
├── plan.md              # This file
├── research.md          # Phase 0 — decisions & rationale
├── data-model.md        # Phase 1 — backup DTO/envelope/snapshot shapes, first-run state machine
├── contracts/           # Phase 1 — C# seam contracts + first-run routing/UI contract + .sfbak schema
│   ├── README.md
│   ├── backup-service-contracts.md
│   ├── first-run-flow-contract.md
│   └── backup-envelope-schema.md
├── checklists/          # (pre-existing)
└── tasks.md             # Phase 2 — created by /speckit-companion-tasks, NOT here
```

### Source code (repository root)

```text
src/StageFright.Core/
├── Contracts/
│   ├── IBackupService.cs              # CHANGED — verification semantics; new CreateBackupAsync overload / result
│   ├── IBackupRepository.cs           # CHANGED — snapshot now covers 16 entity types; upsert ordering contract
│   ├── IBackupDestinationPicker.cs    # NEW — native Save-As seam (suggested filename + stream → chosen path | cancelled)
│   └── IRecoveryCopyStore.cs          # NEW — stable location for the pre-restore recovery copy
├── Exceptions/
│   └── BackupVerificationException.cs # NEW — post-write read-back / count check failed
├── Modules/Settings/
│   ├── BackupService.cs               # CHANGED — read-back verification, AuditAction.Export, full version comparison,
│   │                                  #           3 new entity mappers, 12 new Settings fields, recovery-copy store
│   ├── BackupFileNameBuilder.cs       # NEW — "<sanitised org> backup yyyy-MM-dd.sfbak", empty-name fallback constant
│   └── Backup/
│       ├── BackupEnvelope.cs          # CHANGED — ProtoMember 24/25/26 + counts keys
│       ├── BackupSnapshot.cs          # CHANGED — 3 new IReadOnlyList<> members
│       ├── BackupManifest.cs          # (unchanged; already carries counts/date/version)
│       ├── BackupVerificationResult.cs# NEW — Passed + discrepancy list (the "backup summary" self-check output)
│       ├── SettingsBackupDto.cs       # CHANGED — 12 new ProtoMembers
│       ├── FeeBackupDto.cs            # CHANGED — TaxCode
│       ├── TransactionBackupDto.cs    # CHANGED — TaxCode, JournalEntryId
│       ├── JournalEntryBackupDto.cs   # NEW
│       ├── BankReconciliationBackupDto.cs  # NEW
│       └── ReconciliationLineBackupDto.cs  # NEW
src/StageFright.Data/Repositories/
└── BackupRepository.cs                # CHANGED — read + upsert JournalEntries / BankReconciliations / ReconciliationLines
                                       #           (FK-safe ordering); IgnoreQueryFilters on the soft-deletable pair
src/StageFright.App/
├── MauiProgram.cs                     # CHANGED — .UseMauiCommunityToolkit(); register IBackupDestinationPicker,
│                                      #           IRecoveryCopyStore
├── MauiBackupDestinationPicker.cs     # NEW — CommunityToolkit FileSaver implementation
├── MauiRecoveryCopyStore.cs           # NEW — FileSystem.AppDataDirectory/recovery
└── StageFright.App.csproj             # CHANGED — <PackageReference Include="CommunityToolkit.Maui" />
src/StageFright.UI/
├── App.razor.cs                       # CHANGED — first-run target: /language-select | /first-run-restore (never /setup direct)
├── Pages/Setup/
│   ├── FirstRunLanguageScreen.razor.cs   # CHANGED — Confirm now navigates to /first-run-restore (seed path unchanged)
│   ├── FirstRunRestoreScreen.razor       # NEW — @page "/first-run-restore" — checkbox + <InputFile> + summary + confirm
│   ├── FirstRunRestoreScreen.razor.cs    # NEW
│   ├── RestartRequiredScreen.razor       # NEW — @page "/restart-required" — terminal, no continue path (FR-007)
│   └── RestartRequiredScreen.razor.cs    # NEW
└── Pages/Settings/
    ├── BackupRestoreTab.razor            # CHANGED — native Save dialog, unencrypted notice, verified/failed result,
    └── BackupRestoreTab.razor.cs         #           restart advice, default-filename rule (shared with first-run)
Directory.Packages.props                  # CHANGED — <PackageVersion Include="CommunityToolkit.Maui" ... />

tests/
├── StageFright.Core.Tests/Modules/Settings/
│   ├── BackupServiceTests.cs          # CHANGED — verification pass/fail, version comparison, Export audit
│   ├── BackupFileNameBuilderTests.cs  # NEW — format, sanitisation, empty-name fallback (Verbatim Constraint: word "backup")
│   └── BackupDtoFieldParityTests.cs   # NEW — reflection guard: every entity property has a backup-DTO property (SC-008)
├── StageFright.Data.Tests/
│   └── BackupImportTests.cs           # CHANGED — 3 new entity types round-trip; upsert ordering; older-file tolerance
├── StageFright.Integration.Tests/Scenarios/
│   ├── V9_BackupRestoreTests.cs       # CHANGED — settings-field completeness, archived-row fidelity
│   └── CrossPlatformRoundTripTests.cs # NEW — culture + TimeZoneInfo swap across the restore half; field-for-field equality
└── StageFright.UI.Tests/Pages/Setup/
    └── FirstRunRestoreScreenTests.cs  # NEW — checkbox, file→summary→confirm, cancel path, restart-required routing
```

**Structure Decision**: Single layered desktop app — no new project. All backup logic stays in the `Settings` module of `StageFright.Core` with data access centralised in `StageFright.Data/Repositories/BackupRepository.cs` (the FR-042 deviation already in force); the two platform seams (`IBackupDestinationPicker`, `IRecoveryCopyStore`) are Core contracts with MAUI implementations in `StageFright.App`, matching how `ILanguagePreferenceStore` / `IDeviceThemePreferenceProvider` are wired. First-run screens live under `src/StageFright.UI/Pages/Setup/` beside `FirstRunLanguageScreen`.
