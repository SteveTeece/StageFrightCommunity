# Members — Living Spec

> [DRAFT] Surface-first draft from existing code — every requirement is observed from the code surface unless tagged otherwise. Review before trusting.

## Purpose
The Members capability is the group's roster of record: it tracks who belongs, their contact details, their active/inactive standing, and their committee service history. Without it there is no identity to hang fees, attendance, participation, or GL balances off — every other module (Finance, Events, Rehearsals) depends on a Member existing first.

## Requirements

### Members carry independently validated first and last names
A member record MUST store first name and last name as separate required fields (not a single combined name), each trimmed and capped at 100 characters, on both create and update.

#### Scenario: first or last name missing
- **WHEN** a member is created or updated with a blank first name or a blank last name
- **THEN** the operation is rejected with a validation error naming the missing field

#### Scenario: name exceeds the length limit
- **WHEN** a trimmed first or last name is longer than 100 characters
- **THEN** the operation is rejected with a validation error

### Legacy combined names split by one canonical, deterministic rule
When a single combined name string must be decomposed into first/last name components (e.g. reconciling historically migrated data), the system SHALL apply exactly one algorithm — trim outer whitespace, collapse repeated internal spaces to one, split at the first remaining space, and truncate each resulting part to 100 characters — so that splitting the same input always produces the same result regardless of when or where it runs.

#### Scenario: single-word name
- **WHEN** a combined name contains no space (e.g. "Madonna")
- **THEN** the whole value becomes the first name and the last name is empty

#### Scenario: name with irregular spacing
- **WHEN** a combined name has leading/trailing or doubled internal spaces (e.g. "  John   Michael Smith  ")
- **THEN** it is normalized before splitting, and only the first remaining space is treated as the first/last name boundary (first="John", last="Michael Smith")

### Member profile validation enforces address, contact format, and settings-driven age bounds
Creating or updating a member MUST require a non-blank street address, MUST reject a syntactically invalid email when one is supplied, and MUST validate an optional date of birth against the group's configured age settings.

#### Scenario: address missing
- **WHEN** street address is blank
- **THEN** the operation is rejected with a validation error

#### Scenario: malformed email
- **WHEN** an email is supplied that doesn't match a basic `local@domain.tld` shape
- **THEN** the operation is rejected with a validation error

#### Scenario: date of birth outside allowed bounds
- **WHEN** a supplied date of birth is not in the past, or implies an age exceeding the configured maximum age range, or (when a minimum member age is configured) implies an age below that minimum
- **THEN** the operation is rejected with a validation error describing which bound was violated

#### Scenario: date of birth omitted
- **WHEN** no date of birth is supplied
- **THEN** age-related validation is skipped entirely, since date of birth is optional

### Age is computed in completed years with a defined leap-year rule
The system MUST compute a member's age as whole completed years from date of birth to a reference date, treating a February 29 birthday in a non-leap reference year as falling on March 1st for the purpose of deciding whether the birthday has occurred yet.

#### Scenario: birthday not yet reached this year
- **WHEN** today's date falls before this year's birthday anniversary
- **THEN** the computed age is one less than (reference year − birth year)

#### Scenario: February 29 birth date in a non-leap year
- **WHEN** the reference year is not a leap year and the member was born on February 29
- **THEN** the anniversary is treated as March 1st when deciding whether the birthday has passed

### Status transitions between Active and Inactive are recorded with immutable effective dates and audited
Inactivating or reactivating a member MUST update its status, stamp the corresponding effective date (InactivateDate or ActivateDate) with the transition date, and MUST write an audit trail entry recording the status change.

#### Scenario: inactivate an active member
- **WHEN** an Active member is inactivated
- **THEN** Status becomes Inactive, InactivateDate is set to the current date, and an audit entry is recorded

#### Scenario: reactivate an inactive member
- **WHEN** an Inactive member is activated
- **THEN** Status becomes Active, ActivateDate is set to the current date, and an audit entry is recorded

#### Scenario: transition targets an unknown member
- **WHEN** a status transition or profile update is requested for a member id that does not exist
- **THEN** the operation fails with a not-found error rather than silently succeeding

### Archiving a member soft-deletes it and cascades only to that member's current-year committee record
Archiving MUST soft-delete the member and MUST soft-delete only the archived member's committee membership record for the current calendar year, leaving prior years' committee history untouched and readable.

#### Scenario: archive a current-year committee holder
- **WHEN** a member holding a committee position for the current year is archived
- **THEN** both the member and that current-year CommitteeMembership record are soft-deleted in the same transaction

#### Scenario: archive a member with only prior-year committee history
- **WHEN** an archived member's committee history contains only records from earlier years
- **THEN** those earlier-year records are left un-deleted

### A member holds at most one committee position per calendar year
Assigning a committee position for a member/year pair that already has a record MUST update that record in place rather than creating a duplicate; assigning a position requires a non-blank position value.

#### Scenario: reassigning within the same year
- **WHEN** a member already has a committee position recorded for a given year and is assigned a different position for that same year
- **THEN** the existing record's position is updated, not duplicated

#### Scenario: positions across different years
- **WHEN** a member is assigned committee positions in different years
- **THEN** each year keeps its own distinct record, forming a year-by-year history

#### Scenario: marking a member as committee without a position
- **WHEN** the member form has "Committee Member" checked but no position value entered at save time
- **THEN** the save is rejected with a required-field error and no committee record is written

### An annual, organization-wide committee reset clears the current year's positions atomically
The system MUST provide a single operation that soft-deletes every current-year committee membership record across all members, records the reset year against Settings, and writes one audit entry, all within one atomic transaction.

#### Scenario: reset performed
- **WHEN** the annual reset is invoked
- **THEN** every current-year CommitteeMembership record is soft-deleted, Settings.LastCommitteeResetYear is set to the current year, and one audit entry is written

#### Scenario: reset attempted with no settings configured
- **WHEN** the annual reset is invoked but no Settings record exists yet
- **THEN** the operation is rejected rather than proceeding with undefined defaults

### The AGM reset reminder appears only once its preconditions are all met
The system MUST surface a reminder to run the annual committee reset only when an AGM event has been recorded for the current year, that AGM occurred more than 7 days ago, and the reset has not already been performed this year; otherwise it MUST surface nothing.

#### Scenario: all preconditions satisfied
- **WHEN** an AGM is recorded for the current year, it took place 8 or more days ago, and this year's reset hasn't run yet
- **THEN** a reminder message is returned prompting the reset

#### Scenario: AGM too recent
- **WHEN** the most recent AGM occurred fewer than 7 days ago
- **THEN** no reminder is returned

#### Scenario: reset already done this year
- **WHEN** Settings.LastCommitteeResetYear is already the current year
- **THEN** no reminder is returned, even if an AGM was recorded

### The member list defaults to active members and supports revealing inactive ones and free-text filtering
The member list MUST show only non-archived Active members by default, MUST let the user reveal Inactive members alongside them via an explicit toggle, and MUST support filtering the visible set by a case-insensitive substring match against name, phone, email, and address.

#### Scenario: default view
- **WHEN** the member list loads without any toggle changed
- **THEN** only Active members are shown

#### Scenario: revealing inactive members
- **WHEN** the "show inactive members" toggle is turned on
- **THEN** Inactive members are added to the visible list alongside Active ones

#### Scenario: filtering by search term
- **WHEN** a search term is entered
- **THEN** the visible list narrows to members whose name, phone, email, or address contains that term, case-insensitively

### A member's fee-paid status is derived strictly from GL credits posted to the Member Receivable account
The member detail page's fee history MUST compute "paid" status and paid amount only from ledger credit entries posted to the Member Receivable account for that fee, never from all GL credits associated with the fee (which would double-count the accrual entry against Income).

#### Scenario: fee with both accrual and payment entries
- **WHEN** a fee has an Income-account accrual credit and a separate Member-Receivable-account payment credit
- **THEN** only the Member Receivable credit total counts toward the fee's paid amount

#### Scenario: paid amount reaches the fee amount
- **WHEN** the summed Member Receivable credits for a fee are greater than or equal to the fee amount
- **THEN** the fee is shown as Paid; otherwise it is shown as Unpaid

#### Scenario: fee history fails to load
- **WHEN** loading a member's fee/payment history raises an error
- **THEN** the page still renders the rest of the member's detail, simply without fee history, rather than failing outright

### Members contribute a top-level navigation entry and a dashboard summary tile
The Members capability MUST register a navigation entry positioned immediately after Dashboard, and MUST provide a dashboard tile summarizing active/inactive/total member counts plus an alert when one or more members carry an outstanding balance.

#### Scenario: navigation ordering
- **WHEN** the application's navigation bar is built
- **THEN** the Members entry appears with the second-lowest display order, right after Dashboard

#### Scenario: dashboard tile with outstanding balances
- **WHEN** the dashboard renders and one or more members have a non-zero outstanding GL balance
- **THEN** the Members tile shows active/inactive/total counts and an alert chip naming how many members have outstanding fees

#### Scenario: dashboard tile with no outstanding balances
- **WHEN** no member carries an outstanding balance
- **THEN** the tile shows a neutral "No outstanding fees" note instead of an alert

### Archived members remain restorable rather than requiring re-creation
The system MUST provide a way to reverse an archive by clearing the member's soft-delete flag and writing an audit entry, so an accidentally archived member is not permanently lost. [NEEDS CLARIFICATION: no page or control within this capability's read scope (MemberList/MemberDetail/MemberForm) calls GetArchivedAsync or RestoreAsync — is browsing/restoring archived members handled by a different capability/page outside this scope, or is this currently unreachable from the UI?]

#### Scenario: restoring an archived member
- **WHEN** RestoreAsync is invoked for an archived member's id
- **THEN** the member's soft-delete flag is cleared and an audit "Restore" entry is written

## Uncovered
_None — every file in the area was read._
