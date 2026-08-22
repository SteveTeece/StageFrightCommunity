# Phase 0 Research: Audit Trail Retention Fix & Customization

## D1: Resolve via interface, not concrete type, using `GetRequiredService`

**Decision**: Change `MauiProgram.RunStartupSequence` to resolve `IAuditTrailService` via `GetRequiredService<IAuditTrailService>()` instead of `GetService<AuditTrailService>()`.
**Rationale**: DI only registers `services.AddScoped<IAuditTrailService, AuditTrailService>()` — resolving the concrete type always returns null. `GetRequiredService` makes a genuine misconfiguration throw into the existing failure-tolerant `catch`, so a broken resolution can never again produce a false "purge complete" log (FR-003).
**Alternatives considered**: (a) Also register the concrete class so `GetService<AuditTrailService>()` resolves — rejected, perpetuates resolving-by-concrete-type instead of fixing the root cause. (b) Keep `GetService` (nullable) against the interface — rejected, a future misconfiguration would silently no-op again, the exact defect being fixed.

## D2: Add `PurgeOlderThanAsync` to `IAuditTrailService`

**Decision**: Declare `Task PurgeOlderThanAsync(DateTime cutoff, CancellationToken ct = default)` on `IAuditTrailService` (previously only on the concrete `AuditTrailService`).
**Rationale**: D1 requires calling this method through the interface.
**Alternatives considered**: Cast the resolved interface back to the concrete class — rejected, defeats the purpose of resolving via the interface.

## D3: Retention stored as whole years (`AuditRetentionYears : int`, 1–7, default 1)

**Decision**: Add `Settings.AuditRetentionYears` as an `int`, validated 1–7, default 1.
**Rationale**: Issue #291 speaks in whole years ("default 1 year… up to 7 years"). An `int` with inline range validation mirrors every other bounded numeric `Settings` field (`MinimumMemberAge`/`MaxAgeRangeYears` in `SettingsService.SaveAsync`; `MembershipRenewalMonth`/`CommitteeRenewalMonth` in `SetupFormModel`/`SetupService`).
**Alternatives considered**: `TimeSpan` or a raw "months" int — rejected, adds a unit-conversion step the source issue's own vocabulary doesn't need.

## D4: Cutoff computed inline in `MauiProgram.cs`, not pushed into the service

**Decision**: `MauiProgram.cs` computes `DateTime.UtcNow.AddYears(-retentionYears)` where `retentionYears = (await settingsService.GetAsync())?.AuditRetentionYears ?? 1`, then passes that `DateTime` to `PurgeOlderThanAsync` — unchanged signature.
**Rationale**: `MauiProgram` already computed the (hardcoded) cutoff inline before this fix, so the change is localized to one call site. The `1` fallback satisfies FR-010 (purge runs before Settings exists, if ever reachable).
**Alternatives considered**: Move cutoff computation into `AuditTrailService.PurgeOlderThanAsync(int retentionYears)`, giving it an `ISettingsService` dependency — rejected, grows a thin logging wrapper's constructor for no reader benefit over passing an already-computed `DateTime`.

## D5: Expose retention in both the setup wizard and the Settings → General tab

**Decision**: Add the retention dropdown to `SetupWizard.razor` (Step 2) and `GeneralSettingsTab.razor`, both validated 1–7.
**Rationale**: Every other bounded first-run numeric setting in this codebase (renewal months, age range, committee seat target) is wizard-settable *and* later editable from Settings; a wizard-only field would be the sole exception. Recorded as an Assumption in `spec.md` (User Story 3) and confirmed at the review-spec gate.
**Alternatives considered**: Wizard-only, matching issue #291's literal wording — rejected per the approved spec Assumption.

## D6: Update the Settings backup DTO for the new field

**Decision**: Add `SettingsBackupDto.AuditRetentionYears` (`ProtoMember(18)`, the next free slot) and update `BackupService.MapSettings`/`MapSettingsFromDto`.
**Rationale**: `SettingsBackupDto`'s doc comment commits to mirroring the `Settings` singleton "1:1"; omitting a field being introduced in this very change would silently drop it on every future backup/restore — the same silent-failure pattern as #275, in a new field this change owns.
**Alternatives considered**: Skip the backup DTO — rejected for the field this change adds; pre-existing gaps in that DTO (`Abn`, GST codes, etc.) predate this change and are left alone per `spec.md`'s Assumptions (out of scope).
