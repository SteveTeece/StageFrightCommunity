# Phase 1 Data Model: Audit Trail Retention Fix & Customization

No new entities. One field addition to the existing `Settings` singleton.

## Settings (extended)

| Field | Type | Constraints | Default | Notes |
|---|---|---|---|---|
| `AuditRetentionYears` | `int` | 1–7 inclusive | `1` | Number of years audit trail entries are retained before the startup purge hard-deletes them (FR-006, FR-007). Validated in `SettingsService.SaveAsync` and `SetupService.Validate` — out-of-range throws `ValidationException`. EF Core default value `1` via `SettingsConfiguration`, so existing rows backfill to the documented default on migration. |

**State transitions**: None — a plain scalar setting, changed only by `SettingsService.SaveAsync` (post-setup) or set once by `SetupService.InitializeAsync` (first-run). No workflow/lifecycle states.

**Relationships**: None new. The field is read (not owned) by the audit purge at startup (`MauiProgram.RunStartupSequence` → `ISettingsService.GetAsync()` → `IAuditTrailService.PurgeOlderThanAsync(cutoff)`), consistent with `Settings` being a passive configuration record other modules read.

**Backup/restore parity**: `SettingsBackupDto` gains the mirrored field (`ProtoMember(18)`) so the value round-trips through backup export/import, per research D6.
