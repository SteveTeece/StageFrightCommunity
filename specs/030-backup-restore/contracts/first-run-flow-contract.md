# Contract — First-run flow (routes, routing rule, UI identifiers)

State machine: `data-model.md` §8. This file pins the strings and identifiers consumers/tests bind to.

---

## Routes (Blazor `@page`)

| Route | Component | Layout | Notes |
|---|---|---|---|
| `/language-select` | `FirstRunLanguageScreen` (existing) | `ShellLayout` | unchanged; Confirm now goes to `/first-run-restore` |
| `/first-run-restore` | `FirstRunRestoreScreen` (**new**) | `ShellLayout` | the FR-001 pre-wizard restore choice |
| `/restart-required` | `RestartRequiredScreen` (**new**) | `ShellLayout` | terminal; no navigation away except closing the app |
| `/setup` | `SetupWizard` (existing) | `ShellLayout` | unchanged |

## `App.razor.cs` routing rule (changed)

In `OnInitializedAsync`, when `!Diagnostics.HasStartupError` and `!await SetupService.IsSetupCompleteAsync()`:

```
target = string.IsNullOrWhiteSpace(LanguagePreferenceStore.Get())
    ? "/language-select"
    : "/first-run-restore";      // was "/setup"
Nav.NavigateTo(target, forceLoad: false);
```

So the restore option is reachable both on a first launch (via `/language-select` → Confirm) and on a later launch that recorded a language but never finished setup. `/setup` is never the direct first-run target any more — it is always reached through `/first-run-restore` (checkbox off → Continue) or the debug seed path.

## `FirstRunLanguageScreen.razor.cs` (changed)

`HandleConfirmAsync`: the non-seed branch calls `Nav.NavigateTo("/first-run-restore")` instead of `"/setup"`. The `_seedWithTestData` branch (`SeedSampleDataAndNavigateAsync` → `/dashboard`) is **unchanged**.

## `FirstRunRestoreScreen` — element identifiers (bUnit)

| id / selector | element | behaviour |
|---|---|---|
| `#restore-from-backup` | `<input type="checkbox">` | **must be a checkbox**, not `RadzenSwitch` (Verbatim Constraint). Unchecked by default. |
| `#restore-file` | `<InputFile accept=".sfbak">` | shown only while the checkbox is checked; copies the pick to a temp `.sfbak`, then `GetManifestAsync` |
| `.first-run-restore-summary` | container | shown once a manifest is read: per-record-type counts (`> 0` only), `GeneratedAt` (local), originating `ApplicationVersion` |
| `#confirm-restore` | button | visible after the summary; runs `ImportAsync` on `Task.Run` with a `Progress<string>` indicator |
| `#cancel-restore` | button | clears the manifest + file, checkbox returns to unchecked-equivalent state, nothing changed |
| `#continue-setup` | button | enabled when the checkbox is unchecked; `Nav.NavigateTo("/setup")` |
| `.first-run-restore-error` | alert | validation / corrupt-file / newer-version message; DB untouched; screen stays put |
| `.first-run-restore-progress` | status region | advancing progress text during `ImportAsync` (mirrors `setup-seeding-overlay`) |

On `ImportAsync` success → `Nav.NavigateTo("/restart-required")`. On failure → `.first-run-restore-error`, `#continue-setup` still available.

## `RestartRequiredScreen` — identifiers

| id / selector | element |
|---|---|
| `.restart-required` | root container; renders a single instruction to close and reopen the application |
| (none) | **no** "Continue" / "Go to dashboard" / retry control — the only way forward is relaunching (FR-007) |

## `BackupRestoreTab` (Settings) — additions

| id / selector | element | behaviour |
|---|---|---|
| existing `#restore-file`, confirm/cancel | unchanged flow | on successful `ImportAsync` → `Nav.NavigateTo("/restart-required")` (was an inline success message) |
| `.backup-unencrypted-notice` | alert near the Create button | FR-020 notice: file is unencrypted, holds member personal data + financial history |
| `.backup-verify-result` | alert | renders `BackupVerificationResult` — "verified" (`Passed`) or "failed — do not rely on this file" with `Discrepancies` |
| Create button | — | calls `IBackupService.CreateBackupAsync` (native Save dialog, default filename from `BackupFileNameBuilder`); a user cancel is a no-op, not an error |

## DI registration (`MauiProgram.RegisterCoreServices` / platform)

```csharp
builder.UseMauiCommunityToolkit();                                  // new
services.AddSingleton<IBackupDestinationPicker, MauiBackupDestinationPicker>();  // new
services.AddSingleton<IRecoveryCopyStore, MauiRecoveryCopyStore>();             // new
// IBackupService / IBackupRepository already registered (Phase 13 block) — unchanged lines
```
