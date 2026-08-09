# Feature Specification: Audit Trail Retention Fix & Customization

**Feature branch**: `014-audit-trail-retention`
**Source issues**: [#275](https://github.com/) (bug — startup purge never executes), [#291](https://github.com/) (feature — customizable retention period)

## Summary

The audit trail startup purge silently never runs today because the application container resolves the purge service by its *concrete* class, which isn't how the service is registered — so the resolved reference is always null, the purge call is skipped, and a misleading "purge complete" line is logged regardless. Separately, the purge's 12-month retention window is hardcoded, with no way for a coordinator to change it. Because the fix for the first problem touches the exact code path the second problem needs to extend, they are delivered as one change: the purge is wired to resolve correctly, and its retention window becomes a coordinator-configurable setting (1–7 years, default 1 year) available from first-run setup and from Settings thereafter.

## User Scenarios & Testing

### User Story 1 - Audit trail purge actually runs at startup (Priority: P1)

As the person running the application, when the app starts, old audit trail entries beyond the retention period are actually removed, and the log accurately reflects whether the purge ran.

**Why this priority**: This is a defect masking silent data-retention failure — the organisation may be retaining audit data indefinitely without knowing it, which the previous log message actively hid. Fixing it is the foundation the second story depends on (there is no point making retention configurable if the purge that reads it never runs).

**Independent Test**: Start the application against a database containing audit trail entries older than the retention period. Confirm entries older than the cutoff are removed, and the startup log line only claims success when the purge actually executed.

**Acceptance Scenarios**:

1. **Given** the application starts normally, **When** startup completes, **Then** the audit trail purge executes and entries older than the configured retention period are removed.
2. **Given** audit trail entries exist that are both older and younger than the retention cutoff, **When** the purge runs, **Then** only the entries older than the cutoff are removed; entries within the retention period are preserved.
3. **Given** the purge fails for any reason (e.g. a data-access error), **When** startup continues, **Then** the failure is logged and does not prevent the application from starting.
4. **Given** the purge ran successfully, **When** the outcome is logged, **Then** the log states that the purge completed. **Given** the purge did not run (e.g. resolution or execution failure), **When** startup logs are reviewed, **Then** no line claims the purge completed.

### User Story 2 - Coordinator sets audit retention period during first-run setup (Priority: P2)

As the coordinator completing first-run setup, I can choose how long audit trail history is kept (1 to 7 years), so the organisation's retention period matches its own record-keeping needs instead of a fixed default.

**Why this priority**: Builds directly on Story 1 — configuring a retention period is only meaningful once the purge that consumes it is reliably running. Independently valuable because it turns a previously invisible, fixed behaviour into an explicit choice the coordinator makes once, up front.

**Independent Test**: Complete first-run setup, selecting a retention period other than the default. Confirm the value is saved and is what the startup purge subsequently uses.

**Acceptance Scenarios**:

1. **Given** a new installation, **When** the coordinator reaches the relevant step of the setup wizard, **Then** they are offered a retention period choice from 1 to 7 years, defaulted to 1 year.
2. **Given** the coordinator leaves the retention period at its default, **When** setup completes, **Then** the stored retention period is 1 year.
3. **Given** the coordinator selects a value within 1–7 years, **When** setup completes, **Then** that value is stored and used by the next startup purge.

### User Story 3 - Coordinator changes audit retention period later from Settings (Priority: P3)

As the coordinator, I can revisit and change the audit retention period from the Settings screen after setup, so a choice made at installation isn't permanent.

**Why this priority**: Nice-to-have completeness — the setup wizard (Story 2) is the only place the requesting issue names explicitly, but every other first-run choice in this system (renewal months, age ranges, GST) is also editable later from Settings, and leaving retention wizard-only would be an inconsistent dead end for anyone who changes their mind post-setup.

**Independent Test**: From the Settings screen, change the retention period to a new valid value and save. Confirm the new value is what the next startup purge uses.

**Acceptance Scenarios**:

1. **Given** the coordinator opens Settings after setup, **When** they view the general settings, **Then** the current audit retention period is displayed and editable within 1–7 years.
2. **Given** the coordinator saves a new valid retention value, **When** the save completes, **Then** the new value is persisted and reflected on next load.
3. **Given** the coordinator attempts to save a retention value outside 1–7 years, **When** they submit, **Then** the save is rejected with a clear validation message and no change is persisted.

## Edge Cases

- No audit trail entries exist at all when the purge runs — it completes without error and removes nothing.
- All existing entries are older than the new (shorter) retention period after a coordinator lowers it — the next startup purge removes all of them; this is expected, not an error.
- Settings row does not yet exist (purge runs before first-run setup is completed, if ever reachable) — the purge falls back to the 1-year default rather than failing.
- A retention value at the exact boundary (1 or 7 years) is valid; values below 1 or above 7 are rejected wherever entered (setup wizard and Settings).

## Requirements

### Functional Requirements

- **FR-001**: The application MUST resolve the audit trail purge capability through its registered service abstraction, not through a concrete type that isn't separately registered.
- **FR-002**: The startup purge MUST actually execute on every normal startup (subject to FR-005's failure tolerance).
- **FR-003**: The application MUST log that the startup purge completed only when it actually executed successfully; it MUST NOT log a success message when the purge was skipped or failed.
- **FR-004**: The purge MUST remove audit trail entries with a timestamp older than the configured retention period and MUST preserve entries within it.
- **FR-005**: A failure during the startup purge MUST be logged and MUST NOT prevent the application from starting (unchanged from existing behaviour).
- **FR-006**: The system MUST store a coordinator-configurable audit retention period, expressed in whole years, with a default of 1 year.
- **FR-007**: The stored audit retention period MUST be restricted to a range of 1 to 7 years inclusive; any attempt to save a value outside this range MUST be rejected with a validation error.
- **FR-008**: The first-run setup wizard MUST let the coordinator choose the audit retention period (1–7 years, default 1 year) before completing setup.
- **FR-009**: The Settings screen MUST let the coordinator view and change the audit retention period after setup, subject to the same 1–7 year validation as FR-007.
- **FR-010**: When no retention period has yet been configured (e.g. purge runs in a context with no Settings row), the purge MUST use the 1-year default rather than failing.
- **FR-011**: Changing the audit retention period MUST take effect on the next startup purge without requiring any other action.

### Key Entities

- **Settings** (existing singleton configuration record): gains an audit retention period attribute, expressed in whole years (1–7, default 1), used by the startup purge to compute its cutoff date.

## Success Criteria

### Measurable Outcomes

- **SC-001**: 100% of application startups that reach the purge step actually invoke the purge (previously 0%, since it was silently skipped every time).
- **SC-002**: A startup log claiming the audit purge completed is truthful in 100% of cases — the purge only ever logs success when it executed.
- **SC-003**: A coordinator can complete first-run setup with a chosen audit retention period, and 100% of subsequent startup purges honour that value as the cutoff.
- **SC-004**: Attempting to save an audit retention period outside 1–7 years is rejected 100% of the time, in both the setup wizard and Settings.

## Assumptions

- "Coordinator" refers to whoever completes first-run setup and/or has access to the Settings screen, consistent with existing terminology in this codebase.
- The retention period is a whole number of years (not months or days) — issue #291 specifies "up to 7 years" and a "1 year" default in year units, so the stored unit is years.
- Story 3 (editing from Settings after setup) is an assumed extension for consistency with every other first-run-configurable value in this system (renewal months, age range, GST registration all follow this pattern); it is not explicitly requested by either source issue but is included since leaving retention wizard-only would be inconsistent with the rest of the module.
- "Startup purge never executes" (issue #275) is resolved by correctly resolving the existing purge capability through its interface; no change to the purge's deletion logic itself (hard-delete of entries past the cutoff) is required — that part already behaves correctly when actually invoked.
