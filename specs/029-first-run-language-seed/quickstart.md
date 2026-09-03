# Quickstart: First-Run Language Selection & Optional Sample-Data Seeding

Validation scenarios proving the feature works end-to-end. Each maps to a spec user story; see `spec.md` for the full acceptance-scenario text and `contracts/language-switch-and-preference-contract.md` for the exact identifiers involved.

## Prerequisites

```bash
dotnet build
```

A clean install has no `stagefright.db` and no recorded language preference. To reset to that state during manual verification, run `delete-database.cmd` — it removes the MAUI app-data database **and** the sibling `Settings\preferences.dat` MAUI `Preferences` file that holds the `ILanguagePreferenceStore` (`DisplayLanguageCode`) key, so `/language-select` is shown again on the next launch. (On other platforms, or without the script, clear the `Preferences` store by uninstalling/reinstalling or by deleting that key directly.)

## Scenario 1 — First launch, choose a non-default language (US1, P1)

1. Launch the app on a clean install (no DB, no recorded preference).
2. **Expect**: `/language-select` renders before anything else — before `/setup`, before the dashboard.
3. **Expect**: every shipped language is listed by its own endonym; the OS display language is pre-selected if shipped, otherwise `en-AU`.
4. Select a different language (e.g. `fr-FR`) and confirm.
5. **Expect**: no restart occurs; the setup wizard opens immediately rendered in the chosen language (tab titles, labels, buttons).
6. Complete or abandon the wizard; relaunch the app.
7. **Expect**: `/language-select` does not reappear; the app goes straight to `/setup` (if setup is still incomplete) or the dashboard (if complete), in the previously chosen language.

**Run**: `dotnet test tests/StageFright.Integration.Tests/ --filter "FullyQualifiedName~V1_FirstRunSetupTests|FullyQualifiedName~V22_InSessionLanguageSwitchTests"`

## Scenario 2 — Change language in Settings, see it apply immediately (US2, P2)

1. In a set-up app (any language), open Settings → General tab.
2. Change the display-language selector to a different shipped language.
3. **Expect**: no "restart required" notice appears at any point — before, during, or after — while the selection differs from the saved value.
4. Save.
5. **Expect**: the visible UI (menu labels, this tab's own labels, dates/amounts) switches to the new language within the same interaction, with no restart.
6. Navigate to another screen (e.g. Dashboard).
7. **Expect**: it is already rendered in the new language.
8. Relaunch the app.
9. **Expect**: it starts in the newly saved language.

**Run**: `dotnet test tests/StageFright.UI.Tests/ --filter "FullyQualifiedName~GeneralSettingsTabTests"`

## Scenario 3 — Debug-only sample-data seeding from the first-run screen (US3, P3)

*Debug build only.*

1. Clean install; launch.
2. On `/language-select`, tick "Load sample data" and pick a language.
3. Confirm.
4. **Expect**: a progress overlay shows seeding progress (same visual pattern as the wizard's existing seeding overlay); the setup wizard is never shown.
5. **Expect**: on completion, the app opens directly on `/dashboard`, in the chosen language, with sample members/rehearsals/events/accounts/financial history present, and setup already marked complete.
6. **Expect (Release build)**: the "Load sample data" control is absent entirely; confirming the language proceeds straight into the full setup wizard.

**Run**: `dotnet test tests/StageFright.UI.Tests/ --filter "FullyQualifiedName~FirstRunLanguageScreenTests"`

## Scenario 4 — Sample-data seeding failure (US3 edge case)

1. Force `IDebugDataSeeder.SeedAsync` to throw (test-only substitute).
2. Confirm "Load sample data" on `/language-select`.
3. **Expect**: the failure is displayed on the same screen; `/dashboard` is never opened; the user is not left believing the app is ready.

**Run**: covered by `FirstRunLanguageScreenTests` (seeding-failure case).

## Scenario 5 — Setup wizard no longer offers language or sample data (US1/US3 retained behaviour)

1. Reach `/setup` (via Scenario 1, without ticking sample data).
2. **Expect**: no language selector appears anywhere in the wizard; no sample-data checkbox appears anywhere in the wizard, including the Review tab.
3. **Expect**: the wizard's step list is identical regardless of how `/language-select` was answered (no tab is ever disabled/skipped).

**Run**: `dotnet test tests/StageFright.UI.Tests/ --filter "FullyQualifiedName~SetupWizardTests"`

## Full suite

```bash
dotnet build
dotnet test
```
