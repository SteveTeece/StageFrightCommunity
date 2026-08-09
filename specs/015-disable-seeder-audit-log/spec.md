# Feature Specification: Skip Audit Trail Logging During Debug Data Seeding

**Feature branch**: `015-disable-seeder-audit-log`
**Source issue**: [#296](https://github.com/SteveTeece/StageFrightCommunity/issues/296) (bug — data seeding slow)

## Summary

The optional first-run sample-data seeder generates two full calendar years of realistic club data (members, rehearsals, attendance, fees, payments, events, AGMs, expenses, deposits). Every one of those writes currently also produces an audit trail entry, one database write at a time. That extra bookkeeping is unnecessary — the seeded data is a synthetic starting fixture, not something a real user did — and it measurably slows seeding down. This change stops the seeder from writing audit trail entries at all, without touching what it seeds or how normal, user-driven actions are audited.

## User Scenarios & Testing

### User Story 1 - Sample data seeds quickly and without audit noise (Priority: P1)

As the person opting into sample data on first-run setup (a developer or evaluator trying the app), I want the seeding process to skip writing audit trail entries for the records it creates, so that generating two years of sample data finishes quickly and the audit trail isn't cluttered with hundreds of synthetic entries that don't represent anything a real user did.

**Why this priority**: This is the entire fix the issue asks for — there is no smaller independently valuable slice.

**Independent Test**: Complete first-run setup with the "seed sample data" checkbox ticked. Confirm the seeding step completes without the audit trail table gaining any rows, and confirm every seeded record (members, fees, payments, rehearsals, etc.) still exists exactly as it does today.

**Acceptance Scenarios**:

1. **Given** the coordinator ticks "seed sample data" during first-run setup, **When** seeding runs, **Then** no audit trail entries are created for any member, rehearsal, attendance, fee, payment, event, AGM, expense, income, deposit, or account record the seeder creates.
2. **Given** seeding has just finished, **When** the coordinator opens the Audit Trail page, **Then** it shows no entries for the seeded records (it may show an entry for the setup completion itself, since that is a normal user action, not seeding).
3. **Given** seeding has finished, **When** the coordinator performs a normal action afterward (e.g. edits a member, records a payment), **Then** that action is recorded in the audit trail exactly as it is today — audit logging is fully back in effect for everything that happens after seeding.
4. **Given** seeding is running, **When** one of its steps throws an unexpected error partway through, **Then** audit logging for the rest of the application session still resumes normally afterward — the suppression does not leak past the seeding attempt.

## Edge Cases

- Seeding is skipped entirely because active members already exist (the seeder's existing duplicate-data guard) — nothing is created, so there is nothing to log either way; behavior is unchanged.
- Release builds never register the seeder at all (existing behavior) — this change has no effect when the seeder isn't present.
- A seeding step throws partway through — audit logging must still be back to normal for the rest of the session (see Acceptance Scenario 4), not left permanently disabled.

## Requirements

### Functional Requirements

- **FR-001**: The system MUST NOT create an audit trail entry for any record created, updated, or status-changed by the debug data seeder while it is running.
- **FR-002**: The system MUST continue to create audit trail entries, unchanged, for every action performed through the application's normal user-facing workflows outside of seeding.
- **FR-003**: Audit trail logging MUST automatically resume for the remainder of the application session as soon as the debug data seeder finishes — whether it completes successfully or is interrupted by an error.
- **FR-004**: Skipping audit trail writes during seeding MUST NOT change the seeded data itself — the same members, fees, payments, rehearsals, attendance, events, AGMs, expenses, deposits, and accounts are created exactly as before.
- **FR-005**: The debug data seeder MUST remain gated behind its existing opt-in "seed sample data" checkbox in first-run setup — this change affects only what happens once seeding runs, not when or whether it runs.

## Success Criteria

### Measurable Outcomes

- **SC-001**: A full two-year debug data seed run creates zero audit trail records — down from one record for every member, rehearsal, payment, event, AGM action, expense, income entry, and bank deposit the seeder creates today (several hundred across the full seeded dataset).
- **SC-002**: Every audit-relevant action performed through the app's normal workflows (outside of seeding) still produces exactly one audit trail record, matching today's behavior with zero regressions.
- **SC-003**: If the debug seed run is interrupted partway through, the very next audit-relevant action taken afterward is still recorded — confirming audit logging is never left permanently disabled by a failed or partial seed.

## Assumptions

- "Disable audit logging for the data seeder" applies to every record the seeder creates, not just one entity type (e.g. attendance) — the issue's own wording is general, and the seeder's slowness comes from the combined weight of audit writes across members, rehearsals, payments, events, AGMs, and expenses.
- Suppression is scoped precisely to the seeder's own run, not a permanent or build-wide flag — a developer manually testing the app immediately after seeding still expects their own edits to be audited normally.
- No new setting or UI control is needed — this is an internal behavior change to the seeder that already sits behind the existing "seed sample data" checkbox from spec 001.
- It is expected and acceptable that the Audit Trail page shows no historical entries for seeded records once this change ships — seeded data represents a pre-populated fixture, not a sequence of real user actions, so having no audit trail is the correct representation, not a gap.

## Approach

- **New** `src/StageFright.Core/Modules/AuditTrail/AuditTrailSuppressionScope.cs` — a small ambient scope (`AsyncLocal`-backed) exposing `Begin()` (returns an `IDisposable`) and `IsSuppressed`, so a caller can wrap a block of work and have it flow through nested `await` calls without threading a flag through every method signature.
- **Edit** `src/StageFright.Core/Modules/AuditTrail/AuditTrailService.cs` — `LogAsync` returns immediately without touching the repository when `AuditTrailSuppressionScope.IsSuppressed` is true; unchanged otherwise. This is the single choke point every one of the ~20 application services already calls through, so no other service needs to change.
- **Edit** `src/StageFright.App/Seeding/DebugDataSeeder.cs` — wrap the body of `SeedAsync` in `using var _ = AuditTrailSuppressionScope.Begin();` so the whole seeding run is covered and the scope is guaranteed to lift even if a step throws (FR-003).
- **Tests**: new `AuditTrailSuppressionScopeTests.cs` covering begin/dispose, restoration after an exception inside the scope, and flowing across an `await`; extend `AuditTrailServiceTests.cs` with a case asserting `LogAsync` makes no repository call while suppressed, alongside the existing case proving it still logs normally when not suppressed.
- No DI registration, schema, or migration changes — this is a purely additive, backward-compatible change confined to the audit-trail module plus one call site in the seeder.

**Dependencies**: none beyond the existing `IAuditTrailService` / `AuditTrailRepository` already in place.

## ADDED Requirements
<!-- capability: audit-trail -->

### Audit writes can be suppressed for the duration of a bulk, non-user-driven operation

An ambient `AuditTrailSuppressionScope` SHALL let a caller mark a block of work as exempt from audit logging; while a scope is active, `AuditTrailService.LogAsync` SHALL return without writing to the audit trail repository, and normal logging SHALL resume the instant the scope is disposed — including when an exception unwinds through it. This lets a bulk, non-user-driven operation such as the debug data seeder generate synthetic sample data without producing a real audit trail entry for each record.

#### Scenario: a caller suppresses audit logging around bulk sample-data generation
- **WHEN** a caller wraps a block of work in `AuditTrailSuppressionScope.Begin()`
- **THEN** every `LogAsync` call made during that block, however many services it flows through, writes no audit trail entry
- **AND** logging resumes normally for the rest of the application session the instant the scope is disposed, even if the wrapped work threw an exception

## ADDED Requirements
<!-- capability: app-host -->

### Sample-data seeding produces no audit trail entries for the records it creates

The debug data seeder SHALL suppress audit trail logging for its entire run, so the synthetic records it creates are not indistinguishable from a burst of real user activity in the audit trail.

#### Scenario: sample data is seeded
- **WHEN** the debug data seeder runs (via the opt-in setup-wizard checkbox)
- **THEN** no audit trail entry is created for any member, rehearsal, attendance, fee, payment, event, AGM, expense, income, deposit, or account record it creates
- **AND** audit trail logging for actions taken after seeding finishes is unaffected
