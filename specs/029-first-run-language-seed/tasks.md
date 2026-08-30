---

description: "Task list for First-Run Language Selection & Optional Sample-Data Seeding"
---

# Tasks: First-Run Language Selection & Optional Sample-Data Seeding

**Branch**: `029-first-run-language-seed`

**Input**: [spec.md](./spec.md), [plan.md](./plan.md), [research.md](./research.md), [data-model.md](./data-model.md), [contracts/language-switch-and-preference-contract.md](./contracts/language-switch-and-preference-contract.md), [quickstart.md](./quickstart.md)

**Tests**: Included — CLAUDE.md's "Exhaustive code-path test coverage" rule and plan.md's Constitution Check (§11 Testing Standards, "planned") both require every new code path to carry automated coverage.

**Story order (priority)**: US1 (P1) → US2 (P2) → US3 (P3). US1 is the MVP: the first-run `/language-select` screen, the startup routing that reaches it, and the wizard cleanup that makes its acceptance scenarios true. US2 makes the same in-session switch mechanism reachable from Settings and removes the restart notice. US3 is strictly additive — it puts a working "Load sample data" path onto the screen US1 already built.

---

## Phase 1: Setup

No setup tasks — this feature is additive within the existing five-project solution. It adds no new project, no new NuGet package, and no new build configuration (plan.md "Project Structure" / "Structure Decision"). Proceed directly to Phase 2.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: The no-database preference store and the in-session culture switch — both consumed by every user story (research.md Decisions 1, 2, 4).

**⚠️ CRITICAL**: No user-story work can begin until this phase is complete.

- [x] **T001** [P] Add `ILanguagePreferenceStore` (`string? Get()`, `void Set(string cultureCode)`) — synchronous, doc-commented as never-throwing, matching `ISystemCultureProvider`/`IDeviceThemePreferenceProvider`'s shape — in `src/StageFright.Core/Contracts/ILanguagePreferenceStore.cs`
- [x] **T002** [P] Implement `MauiLanguagePreferenceStore : ILanguagePreferenceStore`, backed by `Microsoft.Maui.Storage.Preferences.Default` under a fixed constant key, with `Get`/`Set` each wrapped in try/catch so a read/write failure is swallowed rather than thrown (depends on T001) in `src/StageFright.App/MauiLanguagePreferenceStore.cs`
- [x] **T003** Register `services.AddSingleton<ILanguagePreferenceStore, MauiLanguagePreferenceStore>()` in `RegisterCoreServices`, beside the existing `ISystemCultureProvider`/`IDeviceThemePreferenceProvider` registrations (depends on T001, T002) in `src/StageFright.App/MauiProgram.cs`
- [x] **T004** [P] Add a synchronous `Switch(CultureInfo culture)` method to `CultureProvider`: set `CultureInfo.DefaultThreadCurrentCulture`, `DefaultThreadCurrentUICulture`, `CurrentCulture`, `CurrentUICulture` to `culture`, update the public `CurrentCulture` property, call `StateHasChanged()`; update the class doc comment, which currently says switching "changes nothing" and is now wrong, in `src/StageFright.UI/Layout/CultureProvider.razor.cs`
- [x] **T005** Add an `ILanguagePreferenceStore` constructor dependency to `LanguageProvider` and insert its `Get()` result as ladder step 2 (between the existing explicit-`Settings.LanguageCode` step and the OS-language step), renumbering the OS step to 3 and the `en-AU` fallback to 4, reusing the existing `SafeCulture`/catalog-`Find` try/catch/fall-through shape for the new step exactly as the other steps already do (depends on T001) in `src/StageFright.Core/Modules/Localization/LanguageProvider.cs`
- [x] **T006** [P] Extend `LanguageProviderTests` with cases for the new step 2: a recorded preference naming a shipped language wins over the OS language; a preference that is null, blank, or names an unshipped language falls through to step 3 unchanged (depends on T005) in `tests/StageFright.Core.Tests/Localization/LanguageProviderTests.cs`
- [x] **T007** [P] Add a new bUnit test file asserting `CultureProvider.Switch` updates the public `CurrentCulture` property and that a re-render triggered by `Switch` propagates to a descendant reading `CurrentCulture` as a cascading value (depends on T004) in `tests/StageFright.UI.Tests/Layout/CultureProviderTests.cs`

**Checkpoint**: The preference store and the in-session switch mechanism exist, are DI-registered, and are covered by tests. User-story work can begin.

---

## Phase 3: User Story 1 - Choose the display language on first launch (Priority: P1) 🎯 MVP

**Goal**: A dedicated `/language-select` screen appears before the setup wizard on a clean install, lists every shipped language, applies the chosen language to the running session with no restart, and is never shown again once a preference is recorded. The setup wizard drops its language selector and sample-data option outright and its step list never varies (spec Acceptance Scenario 7).

**Independent Test**: Install clean, launch, confirm a non-default language on the first-run screen, and verify — with no restart — that the setup wizard and all subsequent screens render in that language; relaunch and verify no re-prompt and the same language.

### Tests for User Story 1

- [x] **T008** [P] [US1] New bUnit test file for the language-selection path of `FirstRunLanguageScreen`: lists every catalog language by endonym; pre-selects the `ILanguageProvider`-resolved default; confirming calls `ILanguagePreferenceStore.Set(selectedCode)` then `CultureProvider.Switch(...)` then navigates to `/setup` (no sample-data control registered) in `tests/StageFright.UI.Tests/Pages/Setup/FirstRunLanguageScreenTests.cs`
- [x] **T009** [P] [US1] New bUnit test file for `App`'s startup routing: substituting `IStartupDiagnosticService`, `ISetupService` and `ILanguagePreferenceStore`, assert — startup error takes priority over everything; setup incomplete + no recorded preference → navigates to `/language-select`; setup incomplete + a recorded preference → navigates straight to `/setup`; setup already complete → no redirect to either, regardless of the preference store's value in `tests/StageFright.UI.Tests/AppRoutingTests.cs`
- [x] **T010** [US1] Rewrite `SetupWizardTests`: delete `CheckSampleDataAndAdvanceToReviewAsync` and every test built on it (seeding-overlay rendering, tab-bypass/disabled-tab behavior, queue-discard-on-check behavior); change the Finish-without-opening-balance test to assert the error fires unconditionally (no more sample-data bypass); add a case asserting the `SetupRequest` passed to `SetupService.InitializeAsync` carries `LanguageCode` equal to the cascaded `CultureProvider.CurrentCulture.Name`, not a wizard field in `tests/StageFright.UI.Tests/Pages/Setup/SetupWizardTests.cs`
- [x] **T011** [US1] Delete `SetupWizardNoSeederTests` — the wizard no longer resolves `IDebugDataSeeder` at all, so "a wizard rendered without a registered debug seeder" is no longer a distinct case; fold its still-relevant `Tabs_AreNeverDisabled_WhenSeederNotRegistered` assertion into `SetupWizardTests` as an unconditional "tabs are never disabled" case (depends on T010) — delete `tests/StageFright.UI.Tests/Pages/Setup/SetupWizardNoSeederTests.cs`
- [x] **T012** [US1] Extend `ReviewTabTests`: delete the `DebugSeederAvailable`/`SeedWithTestData`-parameterized cases (`SeedDataChoice_RendersReadOnlyRow_WhenDebugSeederAvailable` and its siblings) since those parameters no longer exist on `ReviewTab` in `tests/StageFright.UI.Tests/Pages/Setup/Tabs/ReviewTabTests.cs`
- [x] **T013** [US1] Delete `SampleDataTabTests` — `SampleDataTab` is deleted in this story — `tests/StageFright.UI.Tests/Pages/Setup/Tabs/SampleDataTabTests.cs`
- [x] **T014** [US1] Rewrite `LanguagePickerRenderTests`: delete `SetupLanguageStep_ListsEndonymsAndPreSelectsTheDefault` and `SetupLanguageStep_WritesTheChoiceOntoTheModel` (both render the now-deleted `LanguageSelectionTab`); leave the four `GeneralSettingsTab_*` cases in place for US2 to edit in `tests/StageFright.UI.Tests/Localization/LanguagePickerRenderTests.cs`

### Implementation for User Story 1

- [x] **T015** [P] [US1] Add new `SetupResource` keys for the first-run screen — `Setup_FirstRun_PageTitle`, `Setup_FirstRun_Heading`, `Setup_FirstRun_Subheading`, `Setup_FirstRun_ConfirmButton` — to the neutral file and the `en-US`/`fr-FR`/`qps-ploc` variants (the screen also reuses the existing `Setup_Language_Label`/`Setup_Language_HelpText` keys, which are not touched here) in `src/StageFright.UI/Resources/Strings/SetupResource.resx`, `SetupResource.en-US.resx`, `SetupResource.fr-FR.resx`, `SetupResource.qps-ploc.resx`
- [x] **T016** [US1] Create `FirstRunLanguageScreen` at `@page "/language-select"`: on init, resolve the pre-selected language via `ILanguageProvider.ResolveStartupCultureAsync()` mapped through `ISupportedLanguagesCatalog.Find`/`.Default` (mirrors the deleted `LanguageSelectionTab.OnInitializedAsync` logic exactly); render the language `<select>` (reusing `Setup_Language_Label`/`Setup_Language_HelpText`); on confirm, call `ILanguagePreferenceStore.Set(selectedCode)` then `[CascadingParameter] CultureProvider.Switch(CultureInfo.GetCultureInfo(selectedCode))`, then `Nav.NavigateTo("/setup")` — the sample-data branch is added in US3 (depends on T001, T002, T004, T015, T008) in `src/StageFright.UI/Pages/Setup/FirstRunLanguageScreen.razor`, `src/StageFright.UI/Pages/Setup/FirstRunLanguageScreen.razor.cs`
- [x] **T017** [US1] In `App.OnInitializedAsync`, after the existing startup-error check and only when `SetupService.IsSetupCompleteAsync()` is false, inject `ILanguagePreferenceStore` and route to `/language-select` when `Get()` is null/blank, otherwise to `/setup` as today; setup-complete behavior is unchanged (depends on T001, T003, T009) in `src/StageFright.UI/App.razor.cs`
- [x] **T018** [US1] Delete `LanguageSelectionTab` — `src/StageFright.UI/Pages/Setup/Tabs/LanguageSelectionTab.razor`, `src/StageFright.UI/Pages/Setup/Tabs/LanguageSelectionTab.razor.cs`
- [x] **T019** [US1] Delete `SampleDataTab` (depends on T013) — `src/StageFright.UI/Pages/Setup/Tabs/SampleDataTab.razor`, `src/StageFright.UI/Pages/Setup/Tabs/SampleDataTab.razor.cs`
- [x] **T020** [US1] Remove the `<LanguageSelectionTab>`/`<SampleDataTab>` markup from the Organisation Settings tab, and remove the `Disabled="@_seedWithTestData"` attribute from the Chart of Accounts, Opening Balances and Committee `<Tab>` elements (they are now always enabled) (depends on T018, T019) in `src/StageFright.UI/Pages/Setup/SetupWizard.razor`
- [x] **T021** [US1] In `SetupWizard`, remove `_seedWithTestData`, `_debugSeeder`, `IsTabBypassed`, `HandleSeedWithTestDataChanged`, and the `IDebugDataSeeder` resolution in `OnInitialized`; simplify `HandleNextAsync` to advance one tab at a time (drop the `IsTabBypassed` skip loop); change the Finish opening-balance guard to fire whenever `_queuedOpeningBalances.Count == 0` (no more sample-data exemption); add `[CascadingParameter] private CultureProvider? CultureProvider` and set `SetupRequest.LanguageCode` from `CultureProvider?.CurrentCulture.Name`; drop the sample-data-seeding branch of `HandleValidSubmitAsync` (depends on T020, T010) in `src/StageFright.UI/Pages/Setup/SetupWizard.razor.cs`
- [x] **T022** [US1] Drop the now-unwritten `LanguageCode` field from `SetupFormModel` (depends on T021) in `src/StageFright.UI/Pages/Setup/SetupFormModel.cs`
- [x] **T023** [US1] Remove the `DebugSeederAvailable`/`SeedWithTestData` parameters and the "Load sample data" summary row from `ReviewTab`; update `SetupWizard.razor`'s `<ReviewTab>` usage to drop those two attributes; remove the now-orphaned `Setup_Review_LoadSampleDataTerm` key from `SetupResource` (neutral + `en-US`/`fr-FR`/`qps-ploc`) (depends on T012, T021) in `src/StageFright.UI/Pages/Setup/Tabs/ReviewTab.razor`, `src/StageFright.UI/Pages/Setup/Tabs/ReviewTab.razor.cs`, `src/StageFright.UI/Pages/Setup/SetupWizard.razor`, `src/StageFright.UI/Resources/Strings/SetupResource*.resx`

**Checkpoint**: A clean install shows `/language-select` before the wizard; confirming a language applies it immediately with no restart; the wizard never shows a language selector or sample-data option and its step list never varies; a recorded preference is never re-prompted. User Story 1 is independently functional and testable.

---

## Phase 4: User Story 2 - Change the display language in Settings and see it apply at once (Priority: P2)

**Goal**: Saving a changed display language in Settings applies it to the running session immediately via the same `CultureProvider.Switch` mechanism, persists it to both `Settings.LanguageCode` and the no-database preference, and never shows a "restart required" notice at any point.

**Independent Test**: In a set-up app, change the language in Settings and save; verify the visible UI switches to the new language immediately without a restart, and that relaunching keeps that language.

### Tests for User Story 2

- [x] **T024** [P] [US2] Extend `GeneralSettingsTabTests`: saving with a changed `#languageCode` selection calls `ILanguagePreferenceStore.Set(newCode)` and `CultureProvider.Switch` (substitute a fake/stub `ILanguagePreferenceStore` and cascade a `CultureProvider`); saving without changing the language calls neither; no markup anywhere in the rendered component ever contains a restart-related notice, before or after a change or a save in `tests/StageFright.UI.Tests/Pages/Settings/GeneralSettingsTabTests.cs`
- [x] **T025** [US2] In `LanguagePickerRenderTests`, delete `GeneralSettingsTab_ShowsRestartNotice_AfterTheSelectionChanges` and rename/broaden `GeneralSettingsTab_ShowsNoRestartNotice_BeforeAnyChange` to also assert no restart notice appears after `cut.Find("#languageCode").Change(...)` (depends on T014) in `tests/StageFright.UI.Tests/Localization/LanguagePickerRenderTests.cs`

### Implementation for User Story 2

- [x] **T026** [P] [US2] Remove the `Settings_General_LanguageRestartNotice` key from `SettingsResource` (neutral + `en-US`/`fr-FR`/`qps-ploc`) in `src/StageFright.UI/Resources/Strings/SettingsResource*.resx`
- [x] **T027** [US2] In `GeneralSettingsTab`, drop the `LanguageChanged` property and `_initialLanguageCode` field; add `[Inject] ILanguagePreferenceStore` and `[CascadingParameter] private CultureProvider? CultureProvider`; in `HandleSaveAsync`, immediately after a successful `SettingsService.SaveAsync` where `_selectedLanguageCode` differs from the value loaded at init, call `ILanguagePreferenceStore.Set(_selectedLanguageCode)` and `CultureProvider?.Switch(CultureInfo.GetCultureInfo(_selectedLanguageCode))` (depends on T001, T003, T004, T026) in `src/StageFright.UI/Pages/Settings/GeneralSettingsTab.razor.cs`
- [x] **T028** [US2] Remove the `@if (LanguageChanged) { <div class="alert ...">@L["Settings_General_LanguageRestartNotice"]</div> }` block (depends on T027) in `src/StageFright.UI/Pages/Settings/GeneralSettingsTab.razor`

**Checkpoint**: Changing the language in Settings and saving re-renders the whole app in the new language in the same interaction, with no restart notice ever shown, and the choice survives a relaunch. User Stories 1 and 2 both work independently.

---

## Phase 5: User Story 3 - Load sample data from the first-run screen (Debug builds only) (Priority: P3)

**Goal**: In Debug builds, the first-run screen also offers "Load sample data"; ticking it and confirming initialises the database, seeds the full sample dataset with progress shown, and opens straight on the dashboard — in the chosen language, setup already complete, wizard never shown. In Release builds (or wherever `IDebugDataSeeder` isn't registered) the control is absent and confirming proceeds into the wizard as in US1.

**Independent Test**: In a Debug build with a clean install, on the first-run screen tick "Load sample data", choose a language, confirm, watch the seeding progress, and verify the app opens the dashboard — with no restart — in that language, with sample members, rehearsals, events, accounts and financial history present, and that the setup wizard was skipped.

### Tests for User Story 3

- [ ] T029 [P] [US3] Extend `FirstRunLanguageScreenTests`: the "Load sample data" control renders only when `IDebugDataSeeder` resolves from the service provider, and is absent otherwise (with confirm then navigating to `/setup`); ticking it and confirming calls `SetupService.InitializeAsync` then `IDebugDataSeeder.SeedAsync` (with a progress reporter) then navigates to `/dashboard`; a `SeedAsync` failure displays the error, never navigates to `/dashboard`, and never calls `SetupService.InitializeAsync` a second time (depends on T016) in `tests/StageFright.UI.Tests/Pages/Setup/FirstRunLanguageScreenTests.cs`

### Implementation for User Story 3

- [ ] T030 [P] [US3] Add any additional `SetupResource` key the first-run seeding-failure path needs (reuse `Setup_SampleData_CheckboxLabel` and `Setup_Seeding_Message` as-is; add `Setup_FirstRun_SeedingError` for the failure message) to the neutral file and the `en-US`/`fr-FR`/`qps-ploc` variants in `src/StageFright.UI/Resources/Strings/SetupResource*.resx`
- [ ] T031 [US3] In `FirstRunLanguageScreen.razor.cs`, resolve `IDebugDataSeeder` optionally via `IServiceProvider.GetService` (only registered in Debug builds, same pattern the deleted `SetupWizard.OnInitialized` used); when "Load sample data" is ticked on confirm, build a placeholder `SetupRequest` from the same defaults `SetupFormModel` already carries (annual fee 0, currency `"AUD"`, tax off) plus a short non-localized placeholder organisation name, the chosen `LanguageCode`, and `Theme` from the cascaded `ThemeProvider`; call `SetupService.InitializeAsync(request)` then `IDebugDataSeeder.SeedAsync(progress)` inside the same `_seedingInProgress`/`_seedingProgress` try/finally shape `SetupWizard.HandleValidSubmitAsync` used; on success `Nav.NavigateTo("/dashboard")`; on failure show `Setup_FirstRun_SeedingError` and stay on `/language-select` (depends on T016, T029, T030) in `src/StageFright.UI/Pages/Setup/FirstRunLanguageScreen.razor.cs`
- [ ] T032 [US3] Add the "Load sample data" `RadzenSwitch` to `FirstRunLanguageScreen.razor`, visible only when the seeder resolved, and the seeding-progress overlay markup (reuse the existing `setup-seeding-overlay` CSS class and structure from the old `SetupWizard.razor`) (depends on T031) in `src/StageFright.UI/Pages/Setup/FirstRunLanguageScreen.razor`

**Checkpoint**: All three user stories are independently functional. A Debug build can go clean-install → sample-data-populated dashboard, in the chosen language, without ever seeing the wizard or restarting.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Bring the two docs this feature makes stale back in line (per CLAUDE.md's "Spec & Docs Workflow" rule) and verify the whole feature end-to-end.

- [ ] T033 [P] Update `CLAUDE.md`: the "Navigation" section's "First-run detection redirects to `/setup` before the dashboard loads" sentence now needs the `/language-select` screen ahead of it; the Localization section's closing sentence ("A change applies on next launch with a restart notice... `CultureProvider.razor.cs` is the seam for future in-session switching") is now false — replace it with a short description of the live in-session switch (`CultureProvider.Switch`, `ILanguagePreferenceStore`, no restart notice) in `CLAUDE.md`
- [ ] T034 [P] Update `docs/localization/adding-a-language.md`: the "first-run Setup Wizard" / "Setup Wizard language step" references (the language picker moved to `/language-select`), the `SetupResource` table row ("first-run Setup Wizard steps, including the language step"), and all of section 9 ("Startup language resolution and the restart notice" — the ladder gains the recorded-preference tier and there is no restart notice or next-launch delay any more) in `docs/localization/adding-a-language.md`
- [ ] T035 Run the quickstart.md validation scenarios (1–5) end-to-end and run a full `dotnet build` + `dotnet test` (not `--no-build`), reporting both results per CLAUDE.md's Build & Test Verification rule in `specs/029-first-run-language-seed/quickstart.md`
- [ ] T036 *(deferred until implementation)* Launch the app with `WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS=--remote-debugging-port=9222` on a clean install and visually verify `/language-select` renders before `/setup`, and that selecting a non-default language re-renders the wizard immediately with no restart, in both light and dark themes in `src/StageFright.UI/Pages/Setup/FirstRunLanguageScreen.razor`
- [ ] T037 *(deferred until implementation)* Visually verify a Settings → General language change applies immediately with no restart notice at any point, in both light and dark themes in `src/StageFright.UI/Pages/Settings/GeneralSettingsTab.razor`

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: None — no tasks.
- **Foundational (Phase 2)**: No dependencies — BLOCKS all user stories.
- **User Story 1 (Phase 3)**: Depends on Foundational (T001–T004 specifically). No dependency on US2/US3.
- **User Story 2 (Phase 4)**: Depends on Foundational (T001, T003, T004). Its test edits (T025) depend on US1's `LanguagePickerRenderTests` rewrite (T014) landing first, but its implementation (T026–T028) has no US1 dependency.
- **User Story 3 (Phase 5)**: Depends on US1's `FirstRunLanguageScreen` existing (T016) — it extends the same component and test file US1 created. Has no dependency on US2.
- **Polish (Phase 6)**: Depends on all three user stories being complete.

### User Story Dependencies

- **User Story 1 (P1)**: Can start after Foundational. No dependency on US2 or US3.
- **User Story 2 (P2)**: Can start after Foundational; independently testable, but T025 is a same-file sequential edit after T014 (US1).
- **User Story 3 (P3)**: Can start only after US1's T016 exists — it is a strict extension of the component and test file US1 creates, not an independent new surface.

### Within Each User Story

- Tests before the implementation they cover, per file.
- Delete-old-component tasks before the `SetupWizard` edits that stop referencing them.
- `SetupWizard.razor` edits before `SetupWizard.razor.cs` edits (markup no longer references the fields/handlers being removed).
- Story complete before moving to the next priority.

### Parallel Opportunities

- Foundational: T001, T002 sequentially (T002 needs T001's interface); T004 is independent of T001/T002/T003 and can run in parallel with them; T006/T007 can run in parallel with each other once their respective prerequisite lands.
- US1 tests T008, T009 are independent new files — parallel. T010–T014 each touch a different existing test file — parallel with each other and with T008/T009.
- US1 implementation: T015 (resx) is independent of the deletions T018/T019; T018 and T019 are independent of each other.
- US2: T024 and T026 are independent of each other; T025 depends on T014 (US1) landing first, not on T024.
- US3: T029 and T030 are independent of each other.
- Polish: T033 and T034 are independent doc edits — parallel.

---

## Parallel Example: User Story 1

```bash
# Tests — four independent files, launch together:
Task: "New FirstRunLanguageScreenTests.cs (language-selection path only)"
Task: "New AppRoutingTests.cs (startup redirect decision)"
Task: "Rewrite SetupWizardTests.cs (drop sample-data cases, add CultureProvider LanguageCode case)"
Task: "Rewrite LanguagePickerRenderTests.cs (drop SetupLanguageStep_* cases)"

# Implementation — independent deletions, launch together:
Task: "Delete LanguageSelectionTab.razor / .razor.cs"
Task: "Delete SampleDataTab.razor / .razor.cs"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 2: Foundational (preference store + `CultureProvider.Switch`).
2. Complete Phase 3: User Story 1 (`/language-select` screen, startup routing, wizard cleanup).
3. **STOP and VALIDATE**: Run quickstart.md Scenario 1 and Scenario 5. A clean install now reaches a fully-in-language wizard with no restart, and the wizard never shows a language or sample-data control.
4. Demo if ready — this alone fixes the literal issue #361 symptom on the first-run path.

### Incremental Delivery

1. Foundational → US1 (MVP: fixes the first-run half of issue #361, plus the wizard cleanup) → validate via quickstart Scenario 1 & 5.
2. Add US2 (fixes the Settings half of issue #361 — the actual reported bug) → validate via quickstart Scenario 2.
3. Add US3 (Debug-only convenience) → validate via quickstart Scenario 3 & 4.
4. Polish (docs + full-suite verification) → validate via quickstart's "Full suite" section.

### Parallel Team Strategy

With multiple developers, once Foundational is done: Developer A takes US1 (the larger slice — new screen, routing, wizard cleanup); Developer B waits for US1's T016 to land, then takes US3; Developer C takes US2 in parallel with A (Settings work touches none of US1's files except the shared `LanguagePickerRenderTests.cs`, where C's T025 just waits on A's T014).

---

## Notes

- [P] tasks touch different files and have no incomplete same-phase dependency.
- [US1]/[US2]/[US3] maps each task to its story for traceability; Setup/Foundational/Polish tasks carry no story label by design.
- `SetupWizardNoSeederTests.cs` and `SampleDataTabTests.cs` are deleted outright, not emptied — their entire premise (a wizard/tab variant gated on debug-seeder availability) stops existing in this feature (research.md Decision 5: "dead code that can silently regress... is worse than deletion").
- Commit after each task or logical group, per CLAUDE.md's Git/Commit Workflow.
- Run a full `dotnet build` + `dotnet test` (not `--no-build`) before considering any phase checkpoint met, per CLAUDE.md's Build & Test Verification rule.
