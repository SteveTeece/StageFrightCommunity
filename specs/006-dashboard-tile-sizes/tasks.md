---

description: "Task list template for feature implementation"
---

# Tasks: Dashboard Tile Sizes

**Input**: Design documents from `/specs/006-dashboard-tile-sizes/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/IDashboardTileProvider.md

**Tests**: Included. Constitution §11.0 mandates exhaustive reachable-code-path coverage before merge,
so test tasks are not optional for this project.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of
each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

## Path Conventions

Single MAUI Blazor Hybrid project (constitution §4.1, §7.1): `src/`, `tests/` at repository root, as
laid out in `plan.md` → Project Structure.

---

## Phase 1: Setup

**Purpose**: Confirm a clean starting point before making changes

- [X] T001 Run `dotnet build` and `dotnet test` from the repo root on branch `006-dashboard-tile-sizes`
      to confirm a green baseline before implementation begins

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Add the `TileSize` capability to the tile contract and the Dashboard rendering
infrastructure that every user story depends on

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [X] T002 [P] Create `DashboardTileSize` enum (`OneByOne`, `OneByTwo`, `TwoByOne`, `TwoByTwo`) in
      `src/StageFright.Plugins.Contracts/DashboardTileSize.cs` per `data-model.md`
- [X] T003 Add the `TileSize` default interface member
      (`DashboardTileSize TileSize => DashboardTileSize.OneByOne;`) to
      `src/StageFright.Plugins.Contracts/IDashboardTileProvider.cs` per `contracts/IDashboardTileProvider.md`
      (depends on T002)
- [X] T004 [P] Add a contract-level regression test asserting a provider that does not override
      `TileSize` reports `DashboardTileSize.OneByOne`, alongside the existing `NavigateRoute`/`ActionText`
      default-member assertions in `tests/StageFright.Integration.Tests/Scenarios/V8_DashboardPluginTests.cs`
      (depends on T003)
- [X] T005 [P] Add a `GetTileSizeClass(DashboardTileSize size)` helper to
      `src/StageFright.UI/Pages/Dashboard/Dashboard.razor.cs` mapping each enum value to its CSS class
      (`tile-size-1x1`, `tile-size-1x2`, `tile-size-2x1`, `tile-size-2x2`) (depends on T002)
- [X] T006 [P] Add a `.sf-dash-grid` container rule (`display: grid`, `grid-template-columns`, `gap`)
      and `.tile-size-1x1/1x2/2x1/2x2` (`grid-column`/`grid-row` span) rules to
      `src/StageFright.App/wwwroot/app.css`, next to the existing `.sf-dash-tile` rules
- [X] T007 Update `src/StageFright.UI/Pages/Dashboard/Dashboard.razor`: replace the
      `row row-cols-1 row-cols-sm-2 row-cols-lg-3 g-2` container with `sf-dash-grid` in both the Core
      Metrics and Extensions sections, and apply `GetTileSizeClass(tile.TileSize)` to each tile card's
      CSS class list (depends on T005, T006)

**Checkpoint**: Build succeeds; existing `DashboardTests.cs` still passes unmodified (every tile
defaults to `OneByOne` and renders identically to today); any provider can now opt into a different
`TileSize`.

---

## Phase 3: User Story 1 - Glanceable dashboard with right-sized tiles (Priority: P1) 🎯 MVP

**Goal**: Tiles that declare a larger size render visibly bigger than the default 1x1 tiles, so
data-rich tiles stand out and simple tiles stay compact.

**Independent Test**: Configure one existing tile to a larger size, load the Dashboard, and confirm it
renders visibly larger than an unmodified 1x1 tile (e.g. Membership Summary), with both fully
readable.

### Tests for User Story 1 ⚠️

> Write these tests FIRST, ensure they FAIL before implementation

- [X] T008 [P] [US1] bUnit test `Should_ApplyDefaultSizeClass_When_ProviderDoesNotOverrideTileSize` in
      `tests/StageFright.UI.Tests/Pages/Dashboard/DashboardTests.cs`
- [X] T009 [P] [US1] bUnit test `Should_ApplyConfiguredSizeClass_When_ProviderOverridesTileSize` in
      `tests/StageFright.UI.Tests/Pages/Dashboard/DashboardTests.cs` (extend the provider test helpers
      to set a specific `TileSize`)

### Implementation for User Story 1

- [X] T010 [US1] Override `TileSize => DashboardTileSize.OneByTwo` on
      `src/StageFright.UI/Modules/Rehearsals/AttendanceTrendDashboardTileProvider.cs` so the chart tile
      renders double-width (depends on T003, T007)
- [X] T011 [US1] Add a case to
      `tests/StageFright.UI.Tests/Modules/Rehearsals/AttendanceTrendDashboardTileProviderTests.cs`
      asserting `TileSize == DashboardTileSize.OneByTwo` (depends on T010)

**Checkpoint**: User Story 1 is independently functional — loading the Dashboard shows the Attendance
Trend tile spanning two grid columns while Membership Summary and other unmodified tiles stay 1x1.

---

## Phase 4: User Story 2 - Tiles pack cleanly regardless of size mix (Priority: P2)

**Goal**: Tiles of mixed sizes arrange without overlap or gaps, and collapse cleanly to a single
column on narrow screens, in both the Core Metrics and Extensions sections independently.

**Independent Test**: Load the Dashboard with a mix of 1x1, 1x2, and 2x1 tiles present, confirm no
overlap/gaps, then narrow the window and confirm every tile stacks to one column.

### Tests for User Story 2 ⚠️

- [X] T012 [P] [US2] bUnit test asserting the Core Metrics and Extensions sections each render their
      own independent `.sf-dash-grid` container, in
      `tests/StageFright.UI.Tests/Pages/Dashboard/DashboardTests.cs`
- [X] T013 [P] [US2] bUnit test rendering a mix of 1x1/1x2/2x1/2x2 core tiles together and asserting
      each carries its own distinct `.tile-size-*` class simultaneously, in
      `tests/StageFright.UI.Tests/Pages/Dashboard/DashboardTests.cs`

### Implementation for User Story 2

- [X] T014 [US2] Add `grid-auto-flow: dense` to `.sf-dash-grid` in
      `src/StageFright.App/wwwroot/app.css` so mixed-size tiles backfill gaps instead of leaving them
      (depends on T006)
- [X] T015 [US2] Add a narrow-width `@media` rule for `.sf-dash-grid` in
      `src/StageFright.App/wwwroot/app.css` collapsing every `.tile-size-*` tile to a single
      full-width column (depends on T006)
- [X] T016 [US2] Override `TileSize => DashboardTileSize.TwoByOne` on
      `src/StageFright.UI/Modules/Finance/CashFlowDashboardTileProvider.cs` so the Dashboard exercises
      1x1 + 1x2 + 2x1 tiles together (depends on T003, T007)
- [X] T017 [US2] Add a case to
      `tests/StageFright.UI.Tests/Modules/Finance/CashFlowDashboardTileProviderTests.cs` asserting
      `TileSize == DashboardTileSize.TwoByOne` (depends on T016)

**Checkpoint**: User Stories 1 AND 2 both work independently — mixed-size tiles pack without gaps in
Core Metrics, narrowing the window collapses every tile to one column, and Extensions still packs
independently of Core Metrics.

---

## Phase 5: User Story 3 - Module owners choose the right size for a tile's content (Priority: P3)

**Goal**: Any core module or plugin tile provider can opt into one of the four sizes, and two
different providers (core and plugin) requesting larger sizes at the same time still render/pack
correctly.

**Independent Test**: Configure an existing tile (e.g. Attendance Trend) to a larger size and confirm
the Dashboard reflects the new size on next load without other tiles being affected.

### Tests for User Story 3 ⚠️

- [X] T018 [P] [US3] bUnit test asserting a plugin-provided tile (`DisplayOrder >= 100`) that overrides
      `TileSize` renders with the matching size class inside the Extensions section, in
      `tests/StageFright.UI.Tests/Pages/Dashboard/DashboardTests.cs`
- [X] T019 [P] [US3] xUnit test in
      `tests/StageFright.Integration.Tests/Scenarios/V8_DashboardPluginTests.cs` asserting the core
      `rehearsals-attendance-trend` tile and the plugin `test-tile` can both declare non-default
      `TileSize` values simultaneously without affecting `DisplayOrder`-based Core/Extensions grouping
      (depends on T010, T020)

### Implementation for User Story 3

- [X] T020 [US3] Override `TileSize => DashboardTileSize.OneByTwo` on
      `tests/StageFright.TestPlugin/TestTileProvider.cs` to prove a plugin-style assembly can opt into
      a non-default size (depends on T003)

**Checkpoint**: All three user stories are independently functional — a core module and the plugin
fixture both request larger sizes at the same time and the Dashboard still renders and packs
correctly.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Final verification across all stories

- [X] T021 [P] Run `dotnet build` and the full `dotnet test` suite from the repo root to confirm no
      regressions in any project (per CLAUDE.md Build & Test Verification)
- [X] T022 Execute the manual verification steps in `specs/006-dashboard-tile-sizes/quickstart.md`
      (visual check of tile sizing, gap-free packing, and narrow-window single-column collapse)
- [X] T023 [P] Review `src/StageFright.App/wwwroot/app.css` for any `.row.row-cols-*`/`.g-2` Bootstrap
      grid classes left over from the Dashboard's old layout that are no longer referenced anywhere,
      and remove them if unused

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately
- **Foundational (Phase 2)**: Depends on Setup completion — BLOCKS all user stories
- **User Stories (Phase 3-5)**: All depend on Foundational phase completion
  - Can proceed in parallel (if staffed) or sequentially in priority order (P1 → P2 → P3)
- **Polish (Phase 6)**: Depends on all desired user stories being complete

### User Story Dependencies

- **User Story 1 (P1)**: Can start after Foundational (Phase 2) — no dependency on other stories
- **User Story 2 (P2)**: Can start after Foundational (Phase 2) — independently testable; does not
  require US1's `AttendanceTrendDashboardTileProvider` change
- **User Story 3 (P3)**: Can start after Foundational (Phase 2); its cross-provider test (T019) reuses
  US1's `AttendanceTrendDashboardTileProvider` change (T010) and its own plugin change (T020) — it does
  not depend on US2

### Within Each User Story

- Tests MUST be written and FAIL before implementation
- Story complete before moving to the next priority (recommended, not required — stories are
  independent)

### Parallel Opportunities

- T002, T005, T006 (Foundational) can run in parallel — different files, no shared dependency chain
- T008, T009 (US1 tests) can run in parallel
- T012, T013 (US2 tests) can run in parallel
- T018, T019 (US3 tests) can run in parallel
- Once Foundational (Phase 2) completes, Phases 3, 4, and 5 can be worked in parallel by different
  developers, since none of their implementation tasks share a file with another story's
  implementation tasks (verify current file ownership before parallelizing T010/T016/T020, which each
  touch a different provider file)

---

## Parallel Example: Foundational Phase

```bash
Task: "Create DashboardTileSize enum in src/StageFright.Plugins.Contracts/DashboardTileSize.cs"
Task: "Add GetTileSizeClass helper to src/StageFright.UI/Pages/Dashboard/Dashboard.razor.cs"
Task: "Add .sf-dash-grid and .tile-size-* CSS rules to src/StageFright.App/wwwroot/app.css"
```

## Parallel Example: User Story 1

```bash
Task: "bUnit test Should_ApplyDefaultSizeClass_When_ProviderDoesNotOverrideTileSize in tests/StageFright.UI.Tests/Pages/Dashboard/DashboardTests.cs"
Task: "bUnit test Should_ApplyConfiguredSizeClass_When_ProviderOverridesTileSize in tests/StageFright.UI.Tests/Pages/Dashboard/DashboardTests.cs"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup
2. Complete Phase 2: Foundational (CRITICAL — blocks all stories)
3. Complete Phase 3: User Story 1
4. **STOP and VALIDATE**: Load the Dashboard and confirm Attendance Trend renders double-width while
   other tiles stay 1x1
5. Demo if ready

### Incremental Delivery

1. Complete Setup + Foundational → foundation ready, zero visual change yet
2. Add User Story 1 → test independently → demo (MVP: right-sized tiles visible)
3. Add User Story 2 → test independently → demo (gap-free packing + responsive collapse)
4. Add User Story 3 → test independently → demo (core + plugin tiles both opt into larger sizes)
5. Polish: full test suite + quickstart validation

### Parallel Team Strategy

With multiple developers, after Foundational is done:

- Developer A: User Story 1 (`AttendanceTrendDashboardTileProvider`)
- Developer B: User Story 2 (CSS `dense`/`@media` rules + `CashFlowDashboardTileProvider`)
- Developer C: User Story 3 (`TestTileProvider`, cross-provider integration test)

---

## Notes

- [P] tasks = different files, no dependencies
- [Story] label maps task to specific user story for traceability
- Verify tests fail before implementing
- Commit after each task or logical group
- Stop at any checkpoint to validate a story independently
