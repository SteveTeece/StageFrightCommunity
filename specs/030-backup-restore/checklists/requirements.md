# Specification Quality Checklist: Restore From Backup During First-Run Setup

**Purpose**: Validate Companion specification completeness before planning
**Created**: 2026-09-07
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

- **Self-check pass — 2026-09-07.** All items pass. No `spec.md` edits were required by the self-check.
- **Refinement — 2026-09-07.** Added a post-backup self-verification step: after a backup file is written it is read back and the restore-confirmation statistics are checked against the live data (User Story 2 acceptance scenarios 6–8, FR-021–FR-024, SC-009). Re-checked — all items still pass; each new requirement is a single testable MUST with matching acceptance criteria and a measurable outcome.
- **One deferred clarification (FR-020):** whether the backup file must be encrypted or password-protected. This is a genuine product/privacy decision (the file carries full member personal data and financial history and is intended to be emailed between people), left as a single `[NEEDS CLARIFICATION]` marker for the `clarify` step. An informed default ("unencrypted, matching the current local database") is recorded under Assumptions.
- **Pre-existing user-visible artefacts referenced, not implementation choices:** the Background and Assumptions mention the existing `.sfbak` backup file and the existing "Settings → Backup & Restore" screen. These are the current-state surface this feature extends; they are named so the delta is unambiguous, not as new technical decisions.
- **`checkbox` in the requirements** is a value the user pinned verbatim in issue #340 ("use a checkbox control") and is recorded under Verbatim Constraints; it is intentionally kept rather than paraphrased.
- Items marked incomplete require spec updates before clarify or plan. (None are incomplete.)
