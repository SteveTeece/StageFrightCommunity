---

description: "Task list template for feature implementation"
---

# Tasks: [FEATURE NAME]

**Template-Version**: 2.3.0
**Required-Constitution-Version**: 2.3.0
**Last-Updated**: 2026-05-15

**Input**: Design documents from `/specs/[###-feature-name]/`
**Prerequisites**: plan.md (required), spec.md (required for user stories), research.md, data-model.md, contracts/

**Tests**: Tests are MANDATORY. Every feature must include tasks that prove
coverage of all reachable code paths (success, validation failure, exception,
boundary, and state-transition paths).

**Module & Dashboard Tiles** (Constitution §4.1–4.2): If this feature is a new module, include tasks for module folder structure setup and dashboard tile implementation. All tiles must support graphs/charts and other content types.

**Code Organization** (Constitution §3.2.1, §4.5): Every task that produces a class, interface, record, struct, or enum MUST ensure it is placed in its own dedicated file with proper naming alignment. Code review MUST reject any violations. This is non-negotiable.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

## Path Conventions

- **Single project**: `src/`, `tests/` at repository root
- **Feature Modules (Vertical Slice)**: `src/Features/[ModuleName]/Domain/`, `src/Features/[ModuleName]/Application/`, etc. (Constitution §4.1)
- **Web app**: `backend/src/`, `frontend/src/`
- **Desktop**: `api/src/`, `desktop/src/` or `apps/desktop/`
- **Dashboard Tiles**: `src/Features/[ModuleName]/DashboardTile.cs` (Constitution §4.2)
- Paths shown below assume single project - adjust based on plan.md structure

<!-- 
  ============================================================================
  IMPORTANT: The tasks below are SAMPLE TASKS for illustration purposes only.
  
  The /speckit.tasks command MUST replace these with actual tasks based on:
  - User stories from spec.md (with their priorities P1, P2, P3...)
  - Feature requirements from plan.md
  - Entities from data-model.md
  - Endpoints from contracts/
  
  Tasks MUST be organized by user story so each story can be:
  - Implemented independently
  - Tested independently
  - Delivered as an MVP increment
  
  DO NOT keep these sample tasks in the generated tasks.md file.
  ============================================================================
-->

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project initialization and basic structure

- [ ] T001 Create project structure per implementation plan
- [ ] T002 Initialize [language] project with [framework] dependencies
- [ ] T003 [P] Configure linting and formatting tools

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core infrastructure that MUST be complete before ANY user story can be implemented

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

Examples of foundational tasks (adjust based on your project):

- [ ] T004 Setup database schema and migrations framework
- [ ] T005 [P] Implement authentication/authorization framework
- [ ] T006 [P] Setup API routing and middleware structure
- [ ] T007 Create base models/entities that all stories depend on
- [ ] T008 Configure error handling and logging infrastructure
- [ ] T008A Define feature custom exception taxonomy and boundary translation rules
- [ ] T009 Setup environment configuration management

**Checkpoint**: Foundation ready - user story implementation can now begin in parallel

---

## Phase 3: User Story 1 - [Title] (Priority: P1) 🎯 MVP

**Goal**: [Brief description of what this story delivers]

**Independent Test**: [How to verify this story works on its own]

### Tests for User Story 1 (REQUIRED)

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [ ] T010 [P] [US1] Contract test for [endpoint] in tests/contract/test_[name].py
- [ ] T011 [P] [US1] Integration test for [user journey] in tests/integration/test_[name].py
- [ ] T012 [P] [US1] UI integration test for all user-facing functions in this story in tests/StageFright.UI.Tests/Integration/[UserStory1]UIIntegrationTests.cs
- [ ] T012A [P] [US1] Add code-path matrix tests for success, validation, exception, and boundary paths for this story

### Implementation for User Story 1

- [ ] T013 [P] [US1] Create [Entity1] model in src/models/[entity1].py
- [ ] T014 [P] [US1] Create [Entity2] model in src/models/[entity2].py
- [ ] T015 [US1] Implement [Service] in src/services/[service].py (depends on T013, T014)
- [ ] T016 [US1] Implement [endpoint/feature] in src/[location]/[file].py
- [ ] T017 [US1] Add validation and error handling
- [ ] T018 [US1] Add logging for user story 1 operations

**Checkpoint**: At this point, User Story 1 should be fully functional and testable independently

---

## Phase 4: User Story 2 - [Title] (Priority: P2)

**Goal**: [Brief description of what this story delivers]

**Independent Test**: [How to verify this story works on its own]

### Tests for User Story 2 (REQUIRED)

- [ ] T019 [P] [US2] Contract test for [endpoint] in tests/contract/test_[name].py
- [ ] T020 [P] [US2] Integration test for [user journey] in tests/integration/test_[name].py
- [ ] T020A [P] [US2] Add tests covering all reachable code paths (success, validation, exception, boundary, state transitions)

### Implementation for User Story 2

- [ ] T021 [P] [US2] Create [Entity] model in src/models/[entity].py
- [ ] T022 [US2] Implement [Service] in src/services/[service].py
- [ ] T023 [US2] Implement [endpoint/feature] in src/[location]/[file].py
- [ ] T024 [US2] Integrate with User Story 1 components (if needed)

**Checkpoint**: At this point, User Stories 1 AND 2 should both work independently

---

## Phase 5: User Story 3 - [Title] (Priority: P3)

**Goal**: [Brief description of what this story delivers]

**Independent Test**: [How to verify this story works on its own]

### Tests for User Story 3 (REQUIRED)

- [ ] T025 [P] [US3] Contract test for [endpoint] in tests/contract/test_[name].py
- [ ] T026 [P] [US3] Integration test for [user journey] in tests/integration/test_[name].py
- [ ] T026A [P] [US3] Add tests covering all reachable code paths (success, validation, exception, boundary, state transitions)

### Implementation for User Story 3

- [ ] T027 [P] [US3] Create [Entity] model in src/models/[entity].py
- [ ] T028 [US3] Implement [Service] in src/services/[service].py
- [ ] T029 [US3] Implement [endpoint/feature] in src/[location]/[file].py

**Checkpoint**: All user stories should now be independently functional

---

[Add more user story phases as needed, following the same pattern]

---

## Phase N: Polish & Cross-Cutting Concerns

**Purpose**: Improvements that affect multiple user stories

- [ ] TXXX [P] Documentation updates in docs/
- [ ] TXXX Code cleanup and refactoring
- [ ] TXXX Performance optimization across all stories
- [ ] TXXX [P] Additional unit tests in tests/unit/
- [ ] TXXX Security hardening
- [ ] TXXX Run quickstart.md validation

---

## Merge Gate (Constitution) *(mandatory)*

- [ ] MG01 Evidence linked for reachable code-path coverage per changed behavior.
- [ ] MG02 All user-facing UI functions covered by integration tests.
- [ ] MG03 Exception taxonomy and boundary translation tasks completed.
- [ ] MG04 Logging/observability tasks completed for error and critical paths.
- [ ] MG05 No unresolved placeholders (`NEEDS CLARIFICATION`, TODO, TBD) in generated feature deliverables (`spec.md`, `plan.md`, `tasks.md`), excluding template scaffolding under `.specify/templates/`.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies - can start immediately
- **Foundational (Phase 2)**: Depends on Setup completion - BLOCKS all user stories
- **User Stories (Phase 3+)**: All depend on Foundational phase completion
  - User stories can then proceed in parallel (if staffed)
  - Or sequentially in priority order (P1 → P2 → P3)
- **Polish (Final Phase)**: Depends on all desired user stories being complete

### User Story Dependencies

- **User Story 1 (P1)**: Can start after Foundational (Phase 2) - No dependencies on other stories
- **User Story 2 (P2)**: Can start after Foundational (Phase 2) - May integrate with US1 but should be independently testable
- **User Story 3 (P3)**: Can start after Foundational (Phase 2) - May integrate with US1/US2 but should be independently testable

### Within Each User Story

- Tests MUST be written and FAIL before implementation
- Custom exceptions and boundary translation rules MUST be implemented before story completion
- Models before services
- Services before endpoints
- Core implementation before integration
- Story complete before moving to next priority

---

## Parallel Example: User Story 1

```bash
# Launch all tests for User Story 1 together:
Task: "Contract test for [endpoint] in tests/contract/test_[name].py"
Task: "Integration test for [user journey] in tests/integration/test_[name].py"

# Launch all models for User Story 1 together:
Task: "Create [Entity1] model in src/models/[entity1].py"
Task: "Create [Entity2] model in src/models/[entity2].py"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup
2. Complete Phase 2: Foundational (CRITICAL - blocks all stories)
3. Complete Phase 3: User Story 1
4. **STOP and VALIDATE**: Test User Story 1 independently
5. Deploy/demo if ready

### Incremental Delivery

1. Complete Setup + Foundational → Foundation ready
2. Add User Story 1 → Test independently → Deploy/Demo (MVP!)
3. Add User Story 2 → Test independently → Deploy/Demo
4. Add User Story 3 → Test independently → Deploy/Demo
5. Each story adds value without breaking previous stories

### Parallel Team Strategy

With multiple developers:

1. Team completes Setup + Foundational together
2. Once Foundational is done:
   - Developer A: User Story 1
   - Developer B: User Story 2
   - Developer C: User Story 3
3. Stories complete and integrate independently

---

## Notes

- [P] tasks = different files, no dependencies
- [Story] label maps task to specific user story for traceability
- Task IDs must be unique within the document
- Each user story should be independently completable and testable
- Verify tests fail before implementing
- Verify tests cover all reachable code paths before marking a story complete
- Commit after each task or logical group
- Stop at any checkpoint to validate story independently
- Avoid: vague tasks, same file conflicts, cross-story dependencies that break independence
