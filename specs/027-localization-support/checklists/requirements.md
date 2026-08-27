# Specification Quality Checklist: Localization Support (Language Resource Files)

**Purpose**: Validate Companion specification completeness before planning
**Created**: 2026-08-27
**Feature**: [spec.md](../spec.md)

## Content Quality

- [x] No implementation details (languages, frameworks, APIs)
- [x] Focused on user value and business needs
- [x] Written for non-technical stakeholders
- [x] All mandatory sections completed (User Scenarios, Requirements, Success Criteria)

## Requirement Completeness

- [x] Any [NEEDS CLARIFICATION] markers are genuine ambiguities (≤3) deferred to clarify — not unresolved guesses
- [x] Each Functional Requirement is a single, testable MUST/SHOULD statement
- [x] Success criteria are measurable
- [x] Success criteria are technology-agnostic (no implementation details)
- [x] All acceptance scenarios are defined
- [x] Edge cases are identified
- [x] Scope is clearly bounded
- [x] Dependencies and assumptions identified

## Feature Readiness

- [x] All functional requirements have clear acceptance criteria
- [x] User scenarios cover primary flows
- [x] Feature meets measurable outcomes defined in Success Criteria
- [x] No implementation details leak into the specification

## Notes

- Self-check pass completed 2026-08-27. All items pass.
- Amended 2026-08-27 (post-plan): added FR-023 + SC-010 + US3 scenarios/edge cases for "respect the OS display language as the default when a matching resource set ships, else fall back to Australian English". Plan artifacts (`plan.md`, `research.md`, `data-model.md`, `contracts/`) reconciled in the same edit; no new `[NEEDS CLARIFICATION]` introduced.
- Amended 2026-08-27 (post-plan): added FR-024 + US2 scenario 4 + edge cases + SC-001 clause for "user-facing enum values are localised" (`Enum_<Type>_<Member>` in a shared `EnumsResource` via a `LocalizeEnum` helper; enum identity stays culture-invariant). Plan artifacts reconciled (new `EnumsResource`, `EnumLocalizationExtensions.cs`, Decision 10, enum-coverage / no-raw-enum-display guards); no new `[NEEDS CLARIFICATION]`.
- One `[NEEDS CLARIFICATION]` marker remains (FR-021): whether immediate in-session language switching is required for v1, or whether "applies on next launch" is acceptable. An informed default ("applies on next launch, with a restart notice") is recorded under Assumptions; the marker is deferred to `/speckit-companion-clarify`.
- "MAUI Blazor Hybrid", "GL balance", and "endonym" appear in Assumptions / Key Entities as pre-existing product context and domain vocabulary already used across the project's other specs, not as new implementation or design choices.
- Scope is deliberately staged: P1 proves the extraction pattern on the navigation shell + Members; P2 applies it across all remaining surfaces; P3 adds user-selectable language. Out-of-scope items (plugin text, RTL, report layout redesign, translating user-entered data, translating logs) are listed under Assumptions.
