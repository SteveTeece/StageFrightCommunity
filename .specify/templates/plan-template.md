# Implementation Plan: [FEATURE]

**Template-Version**: 2.1.0
**Required-Constitution-Version**: 2.1.0
**Last-Updated**: 2026-04-01
**Branch**: `[###-feature-name]` | **Date**: [DATE] | **Spec**: [link]
**Input**: Feature specification from `/specs/[###-feature-name]/spec.md`

**Note**: This template is filled in by the `/speckit.plan` command.

## Summary

[Extract from feature spec: primary requirement + technical approach from research]

## Technical Context

<!--
  ACTION REQUIRED: Replace the content in this section with the technical details
  for the project. The structure here is presented in advisory capacity to guide
  the iteration process.
-->

**Language/Version**: [e.g., Python 3.11, Swift 5.9, Rust 1.75 or NEEDS CLARIFICATION]  
**Primary Dependencies**: [e.g., FastAPI, UIKit, LLVM or NEEDS CLARIFICATION]  
**Storage**: [if applicable, e.g., PostgreSQL, CoreData, files or N/A]  
**Testing**: [e.g., pytest, XCTest, cargo test or NEEDS CLARIFICATION]  
**Code-Path Coverage**: Every reachable code path MUST be covered (success, validation failure, exception, boundary, state transition).  
**Exception Strategy**: Define and use project custom exceptions; do not leak raw framework exceptions across boundaries.  
**UI Integration Tests**: All user-facing features must have full UI integration test coverage.  
**Target Platform**: [e.g., Linux server, Windows 11/macOS 14 desktop, WASM or NEEDS CLARIFICATION]
**Project Type**: [single/web/desktop - determines source structure]  
**Performance Considerations**: [advisory guidance; record baseline goals for benchmarking where useful]  
**Constraints**: [domain-specific, e.g., <200ms p95, <100MB memory, offline-capable or NEEDS CLARIFICATION]  
**Scale/Scope**: [domain-specific, e.g., 10k users, 1M LOC, 50 screens or NEEDS CLARIFICATION]

## Risk Register *(mandatory)*

| Risk | Impact | Likelihood | Mitigation | Owner |
|------|--------|------------|------------|-------|
| [risk-1] | [H/M/L] | [H/M/L] | [mitigation] | [name/role] |
| [risk-2] | [H/M/L] | [H/M/L] | [mitigation] | [name/role] |

## Exception Taxonomy & Boundary Translation Plan *(mandatory)*

- [List feature-specific custom exceptions]
- [Define where raw framework/dependency exceptions are translated]
- [Define UI handling for deterministic user-safe messages]

## Plugin / Extension Boundary Plan *(mandatory where applicable)*

- [Identify extension points and registration/discovery model]
- [Define failure isolation behavior for extensions]
- [Define contract ownership and backward-compatibility constraints]

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

[Gates determined based on constitution file]

- No UI function may be considered complete without full integration test coverage.
- No implementation may be considered complete without tests for all reachable code paths in changed behavior.
- No feature may pass gate without documented custom exception taxonomy and boundary translation handling.

## Layer Mapping *(mandatory)*

- **Domain**: [entities, value objects, contracts]
- **Application**: [orchestration, use-cases, handlers]
- **Infrastructure**: [adapters, persistence, external integrations]
- **UI/Presentation**: [pages, components, routes]
- **Cross-Cutting**: [logging, telemetry, resilience]

## Project Structure

### Documentation (this feature)

```text
specs/[###-feature]/
├── plan.md              # This file (/speckit.plan command output)
├── research.md          # Phase 0 output (/speckit.plan command)
├── data-model.md        # Phase 1 output (/speckit.plan command)
├── quickstart.md        # Phase 1 output (/speckit.plan command)
├── contracts/           # Phase 1 output (/speckit.plan command)
└── tasks.md             # Phase 2 output (/speckit.tasks command - NOT created by /speckit.plan)
```

### Source Code (repository root)
<!--
  ACTION REQUIRED: Replace the placeholder tree below with the concrete layout
  for this feature. Delete unused options and expand the chosen structure with
  real paths (e.g., apps/admin, packages/something). The delivered plan must
  not include Option labels.
-->

```text
# [REMOVE IF UNUSED] Option 1: Single project (DEFAULT)
src/
├── models/
├── services/
├── cli/
└── lib/

tests/
├── contract/
├── integration/
└── unit/

# [REMOVE IF UNUSED] Option 2: Web application (when "frontend" + "backend" detected)
backend/
├── src/
│   ├── models/
│   ├── services/
│   └── api/
└── tests/

frontend/
├── src/
│   ├── components/
│   ├── pages/
│   └── services/
└── tests/

# [REMOVE IF UNUSED] Option 3: Desktop App + API (when "Windows/macOS" detected)
api/
└── [same as backend above]

desktop/
└── [platform-specific structure: feature modules, UI flows, platform tests]
```

**Structure Decision**: [Document the selected structure and reference the real
directories captured above]

## Phase 0: Research Alignment

- [Confirm open questions resolved or tracked]
- [Capture key technical decisions and alternatives]

## Phase 1: Design & Contracts Alignment

- [Align data model, contracts, and quickstart with spec]
- [Ensure plugin boundaries and exception strategy are explicit]

## Re-check Constitution Gate (Post-Design)

- [Confirm no new violations introduced after design decisions]
- [Record any approved complexity exceptions]

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| [e.g., 4th project] | [current need] | [why 3 projects insufficient] |
| [e.g., Repository pattern] | [specific problem] | [why direct DB access insufficient] |
