# Audit Trail — Living Spec

> [DRAFT] Surface-first draft from existing code — every requirement is observed from the code surface unless tagged otherwise. Review before trusting.

## Purpose

The audit trail gives every state-changing action across the application a permanent, independent record of what happened, to what, and when — so financial and membership history can be reconstructed and disputes investigated after the fact. Without it, corrections and status changes (especially around member lifecycle and GL-adjacent actions like forgiveness or committee resets) would leave no trace once the underlying record itself is updated or archived.

## Requirements

### Every domain state change is captured as an immutable audit record

Application services SHALL write one `AuditTrailEntry` per state-changing action (create, update, delete, restore, status change, and module-specific actions like forgiveness, committee reset, import/export), capturing the entity type, entity id, action, and before/after value snapshots where applicable. The record, once written, is never updated or deleted by application code.

#### Scenario: a member is created

- **WHEN** a service completes a domain write (e.g. a new member is added, an existing member is updated, archived, or restored)
- **THEN** it records a matching audit entry describing the entity affected and the action taken
- **AND** for an update, both the prior and new values are captured; for a create, only the new value; for a delete, only the prior value

[NEEDS CLARIFICATION: the repository exposes a query to read an entity's audit history back (`GetByEntityAsync`), but no service method or UI screen currently calls it — is a history view planned, or is this read path dead code?]

### Audit writes are transactionally coupled to the change they document

Logging an audit entry SHALL happen inside the same unit-of-work transaction as the domain write it records, so the two either both commit or both roll back together — an audit entry must never exist for a change that didn't happen, or vice versa.

#### Scenario: a transactional write fails after the audit call

- **WHEN** a service performs a domain mutation and its paired audit log call inside one `ExecuteInTransactionAsync` block
- **THEN** a failure anywhere in that block rolls back both the domain change and the audit entry together

### The recorded actor defaults to a fixed system identity [inferred]

In the MVP, every audit entry SHALL attribute the action to a fixed "system" identity rather than a real authenticated user, since the application has no per-user login. This keeps the audit trail's actor field meaningful once multi-user attribution is introduced without changing the entry shape.

#### Scenario: any action is logged without an explicit actor

- **WHEN** a caller logs an audit entry without supplying a user id
- **THEN** the entry's actor is recorded as "system"

### Audit history ages out on a rolling retention window instead of soft-delete

Unlike other entities, audit entries carry no soft-delete fields — they SHALL instead be hard-deleted once older than a fixed retention period (12 months), evaluated at application startup. This keeps the audit log bounded over time without ever allowing an individual entry to be edited or archived.

#### Scenario: startup retention purge runs

- **WHEN** the application starts
- **THEN** all audit entries older than 12 months are permanently removed from the store

### Retention purge failures never block application startup

A failure while purging expired audit entries SHALL be caught and logged as a structured error rather than propagated, so a transient storage problem in the audit log can never prevent the rest of the application from starting.

#### Scenario: the purge throws

- **WHEN** the retention purge encounters an unexpected error (e.g. a data access failure)
- **THEN** the error is logged
- **AND** application startup proceeds unaffected

## Uncovered

_None — every file in the area was read._
