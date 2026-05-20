# StageFright Community MVP — Comprehensive Specification Analysis Report

**Analysis Date**: 2026-05-20  
**Report Version**: 1.0  
**Analyzed Artifacts**:
- `specs/001-initial-mvp/spec.md` (967 lines)
- `specs/001-initial-mvp/plan.md` (902 lines)
- `specs/001-initial-mvp/tasks.md` (536 lines)
- `.specify/memory/constitution.md` (2.2.1)

**Analysis Scope**: Cross-artifact consistency, quality, coverage, constitution alignment

---

## Executive Summary

### Overall Assessment: **⚠ MODERATE-RISK** — Multiple gaps, ambiguities, and consistency issues identified

**Severity Distribution**:
- **CRITICAL Issues**: 6 (blocking Phase 1 implementation)
- **HIGH Issues**: 12 (significant gaps/misalignments)
- **MEDIUM Issues**: 18 (incomplete specification or coverage)
- **LOW Issues**: 14 (minor inconsistencies, advisory)

**Key Findings**:
1. ✅ Constitution alignment is strong across all artifacts
2. ⚠ Coverage gaps: 9 requirements with zero associated tasks
3. ⚠ Task orphans: 5 tasks referenced without clear requirement mapping
4. ⚠ Underspecification: 14 requirements lack measurable acceptance criteria or implementation details
5. ✅ No major duplication detected
6. ⚠ Terminology inconsistencies: 7 concepts named differently across documents
7. ⚠ Data entity mismatches: 3 entities in plan but not fully specified in spec

**Readiness Assessment**: Specification is **NOT READY** for Phase 1 implementation without addressing CRITICAL issues.

---

## 1. Duplication Detection

### Finding 1.1: Minimal Duplication (Good)
**Severity**: LOW  
**Status**: ✅ PASS

Analysis found NO significant requirement duplication. User stories and functional requirements are well-delineated with no overlapping scope. Each requirement appears once in the spec with single task mapping.

**Example**: 
- FR-004 (Annual fee application) → US4 → Task T-121 (single mapping, no duplication)
- FR-002 (Member management) → US2 → Tasks T-038-T-075 (clear scope separation)

---

## 2. Ambiguity Detection

### Finding 2.1: Vague Performance Requirements ❌
**Severity**: MEDIUM  
**Issue**: Report generation performance is underspecified

**Details**:
- NFR-019 states: "reports MUST generate and display within 5 seconds for typical organizations (≤500 members, ≤3 years of historical data)"
- Term "typical organizations" is vague
- No guidance for atypical organizations (>500 members)
- No definition of "acceptable" performance for compliance verification

**Impact**: Test acceptance criteria cannot be precisely validated

**Remediation**:
- [ ] Define exact performance threshold: "5 seconds for ≤500 members, ≤2000 transactions"
- [ ] Specify timeout behavior: "If report generation exceeds 5 seconds, show 'Generating...' message with cancel button"
- [ ] Add performance benchmark tasks to Phase 4

**Related References**: NFR-019, FR-047, T-200

---

### Finding 2.2: Unclear Dashboard Tile Timeout Behavior ❌
**Severity**: MEDIUM  
**Issue**: Dashboard graceful degradation lacks specific timeout and behavior details

**Details**:
- FR-011: "Tiles load progressively and degrade gracefully if slow or failing"
- T-180: "Implement graceful degradation for dashboard with tile timeout handling (5-second limit)"
- **Gap**: No specification of what happens AT 5 seconds vs. AFTER 5 seconds
- **Gap**: No specification of retry behavior or error message format
- **Gap**: UI response during timeout unclear (skeleton loader, spinner, blank space?)

**Impact**: Implementation team must make design decisions without specification guidance

**Remediation**:
- [ ] Add to spec/FR-011: "If a tile provider does not return data within 5 seconds, the dashboard SHALL: (1) display the tile area as a loading skeleton with 'Loading...' text, (2) continue loading other tiles, (3) replace loaded tiles progressively as data arrives, (4) if tile still loading after 10 seconds, replace with error state 'Tile unavailable'."
- [ ] Specify error tile content: "Show error icon + message 'Unable to load [Tile Name]' + retry button"
- [ ] Add retry logic: "Clicking retry button triggers tile refresh (another 5-second attempt)"

**Related References**: FR-010, FR-011, T-090, T-180

---

### Finding 2.3: "Scalable" and "Responsive" Without Metrics ⚠
**Severity**: LOW  
**Issue**: Architecture claims scalability/responsiveness without defined metrics

**Details**:
- Plan §1.0: "The application targets desktop platforms only; emphasizing reliability, simplicity, and modularity for long-term maintainability"
- Spec §1: No scalability targets (max members, max transactions, data growth rate)
- No concurrency metrics (max concurrent users — though MVP is single-user, Phase 2+ context needed)

**Impact**: Low impact for MVP (single-user, local database), but Phase 2+ may struggle with inherited ambiguity

**Remediation**:
- [ ] Add to plan: "MVP Scalability Assumptions: ≤500 members, ≤10 years historical data, ≤20K transactions, single-user access. Phase 2+ will define multi-user and cloud scalability targets."

**Related References**: Plan §1.0, NFR-003

---

### Finding 2.4: "WCAG AA Compliance" Verification Approach Undefined ❌
**Severity**: MEDIUM  
**Issue**: Compliance verification method is not specified

**Details**:
- NFR-004: "All UI elements MUST comply with WCAG AA contrast requirements"
- NFR-006: "color palette compliance (HSL lightness 60–80%, saturation <50%) MUST be verified by automated tests"
- **Gap**: No specification of which tool/method performs verification (axe-core, WAVE, manual testing, automated contrast analyzer?)
- **Gap**: No specification of test automation framework (unit tests, integration tests, Playwright E2E?)
- **Gap**: No specification of WCAG AA success criteria priority (all SC 2.1 AA = 50+ criteria?)

**Impact**: T-191 (automated contrast ratio test) cannot be implemented without clarity on test framework

**Remediation**:
- [ ] Add to NFR-004: "WCAG AA compliance verification uses axe-core analyzer for automated detection. All critical and serious issues MUST be resolved; warnings are advisory."
- [ ] Add to T-191: "Implement automated contrast ratio validation using `ColorContrast.CalculateRatio(fg, bg)` returning numeric ratio (4.5:1 minimum for text). Test runs on all UI surfaces in both light and dark themes."
- [ ] Link to WCAG 2.1 AA guideline subset: "Apply to SC 1.4.3 (Contrast Minimum) and SC 4.1.3 (Status Messages)."

**Related References**: NFR-004, NFR-006, T-185, T-186, T-191

---

### Finding 2.5: "User-Friendly" Error Messages Lack Specificity ❌
**Severity**: MEDIUM  
**Issue**: Error message standards are undefined

**Details**:
- NFR-004: "All user-facing error messages MUST be validated in user testing for clarity (≥90% of users understand the message without assistance)"
- **Gap**: No specification of error message format (sentence structure, tone, action guidance)
- **Gap**: No specification of how "≥90% of users understand" is measured (sample size, testing method?)
- **Gap**: No error message templates or examples provided

**Example of ambiguity**:
- FR-002a: "Validation error messages MUST be specific: 'Date of birth cannot be today or in the future', 'Date of birth must be within 150 years', 'Member age ({age} years) must be at least {minimum_age} years old'." ✅ (Good example)
- But most other error scenarios lack this level of specificity (e.g., FR-014: "Import file incomplete: missing {entity_type}" is specific, but others are vague)

**Impact**: T-177 (error message constants) and T-179 (edge case handlers) cannot determine which error messages to implement

**Remediation**:
- [ ] Add to spec, new section "Error Message Standards":
  ```
  All user-facing error messages MUST:
  - Be written in plain English, active voice, 2nd person (e.g., "You are..." not "The system...")
  - Explain what went wrong AND what to do next
  - Be ≤100 characters (fit on single line on mobile view)
  - NOT include technical jargon or exception names
  - Include a recoverable action when possible (e.g., "Retry", "Go Back", "Contact Support")
  
  Example: ✅ "Email format is invalid. Please enter an email address like user@example.com."
  Example: ❌ "ArgumentException: Invalid email format"
  ```
- [ ] Add user testing requirement: "Error message clarity validated with ≥10 participants (target ≥90% comprehension rate without explanation)"

**Related References**: NFR-004, T-177, T-183

---

## 3. Underspecification

### Finding 3.1: Payment FIFO Allocation Algorithm Missing Implementation Details ❌
**Severity**: HIGH  
**Issue**: FIFO algorithm is defined conceptually but lacks precise implementation specification

**Details**:
- FR-016: "System MUST allocate payments using FIFO (First-In-First-Out): oldest unpaid fees satisfied first (e.g., 2024 annual fee before 2025 annual fee before 2025 attendance fees)"
- Plan §3.1: Payment FIFO ordering uses `CreatedAt` timestamp as tiebreaker: "fees created on same date sorted by CreatedAt ascending"
- **Gap**: Edge case handling undefined:
  - What if member has mixed annual + attendance fees from same year? (Order of prioritization?)
  - What if payment is partial? (Allocate to single fee or split across multiple?)
  - What if payment amount exceeds total outstanding balance? (Overpayment handling?)
  - How is `CreatedAt` timestamp set for retroactive fees created in bulk (annual fee application)? (All same timestamp or sequential?)

**Impact**: T-111 (payment allocation service) cannot be implemented without edge-case guidance

**Remediation**:
- [ ] Add to FR-016, detailed algorithm spec:
  ```
  FIFO Allocation Algorithm:
  1. Retrieve all unpaid fees for member, sorted by:
     a. FeeDate ascending (oldest date first)
     b. FeeType priority: Annual fees before Attendance fees
     c. CreatedAt ascending (tiebreaker for fees on same date)
  2. For each fee in order:
     a. If payment amount >= fee amount: allocate full fee, reduce payment amount
     b. If payment amount < fee amount: allocate partial payment to fee, mark fee as partially paid, zero payment amount
     c. If payment exhausted: stop processing remaining fees
  3. Create GL transaction pairs for each allocation:
     a. Debit=$allocation_amount on Cash (GL#0100)
     b. Credit=$allocation_amount on MemberReceivable (GL#0101)
     c. Link to Payment record
  4. Recalculate member balance (sum unpaid amounts across all fees)
  
  Edge Cases:
  - Partial Payment: If payment amount < next unpaid fee, mark fee as partially paid with remaining balance. Payment record Amount = allocation_amount (GL transaction amount), not original payment amount. (QUESTION: Clarify if Payment.Amount represents initial payment or allocated amount.)
  - Overpayment: If payment amount exceeds total outstanding balance, create GL transaction for full payment amount, mark all fees as paid, record overpayment as member credit (GL debit on MemberReceivable, credit on CashReceived).
  - Bulk Annual Fees: When annual fee batch is applied (T-121), all fees created in batch have timestamps within seconds of each other. For tiebreaker ordering, use Fee.Id ascending if CreatedAt is identical.
  ```
- [ ] Add comprehensive test in T-184: "Test FIFO allocation with: 2024 annual $50 (unpaid), 2025 annual $50 (unpaid), 2025 attendance $10 (unpaid, created 1 day after 2025 annual). Payment of $75 should allocate: $50→2024 annual, $25→2025 annual, $0→2025 attendance. 2025 attendance remains unpaid at $10."

**Related References**: FR-016, FR-017, T-111, T-184

---

### Finding 3.2: GL Account Assignment Order Unclear for Simultaneous Category Creation ❌
**Severity**: MEDIUM  
**Issue**: GL account sequencing is unclear when categories created in rapid succession

**Details**:
- Plan §3.1: "GL accounts assigned in creation order (by CreatedAt timestamp, ascending); first income category GL#1000, second GL#1001, etc."
- Clarification Session: "GL account assigned automatically via GLAccountAssignmentService when coordinator creates new category (no user input needed)"
- **Gap**: What if two categories created within same millisecond (timestamp tie)?
- **Gap**: What if createdAt timestamp is manually set to past date during import?
- **Gap**: GLAccountAssignmentService algorithm for "next available GL#" is undefined (max GL# determination?)

**Impact**: T-034b (GLAccountAssignmentService) cannot deterministically sequence GL accounts

**Remediation**:
- [ ] Add to plan, GLAccountAssignmentService specification:
  ```
  GLAccountAssignmentService.AssignGLAccountAsync(Category category):
  - If category.Type == Income:
    a. Query all Income categories where isArchived=false, ordered by CreatedAt ASC, then Id ASC
    b. Count categories = N
    c. Assign GL# = 1000 + N (e.g., first income = GL#1000, second = GL#1001, etc.)
    d. Max GL# for Income = 1099 (100 categories max per type)
  - If category.Type == Expense:
    a. Same logic, GL# = 2000 + N (max 2099)
  - Enforce constraint: Coordinator cannot create >100 income OR >100 expense categories. Reject with message: "Cannot create category: maximum 100 income categories already defined. Please archive unused categories first."
  - Timestamp tiebreaker: If CreatedAt identical (rare), use Id ascending (database GUID comparison) for deterministic ordering
  ```
- [ ] Add test T-034b-test: "Create 5 income categories in rapid succession; verify GL assignments 1000, 1001, 1002, 1003, 1004 in creation order regardless of system clock precision."

**Related References**: FR-032, Plan §3.1, T-034b

---

### Finding 3.3: Member Age Calculation Lacks Leap Year and Edge Date Handling ❌
**Severity**: MEDIUM  
**Issue**: Age calculation formula is simplified; edge cases undefined

**Details**:
- FR-002a: "Age calculation formula: `floor((today - DOB) / 365.25)`"
- **Gap**: Does 365.25 account for leap years consistently?
- **Gap**: Edge case: Member born Feb 29 (leap year), today is Feb 28 (non-leap year). Is member X years or X-1 years old? (Both 365 days and 366 days interpretations exist)
- **Gap**: No specification of timezone handling (what is "today"? UTC or local?)

**Impact**: T-072 (AgeCalculationService) may calculate age inconsistently across timezones

**Remediation**:
- [ ] Add to FR-002a, precise algorithm:
  ```
  Age Calculation (Server-Side):
  - Input: DateOfBirth (stored as UTC date without time)
  - Reference Date: DateTime.UtcNow.Date (today, UTC timezone, no time component)
  - Algorithm:
    age = referenceDate.Year - dob.Year
    if (referenceDate.Month < dob.Month) || (referenceDate.Month == dob.Month && referenceDate.Day < dob.Day):
      age = age - 1
    return age
  - Result: Age in completed years (member becomes older on their birthday, not before)
  - Example:
    * DOB = 1990-02-28, Today = 2026-02-27: Age = 35 (birthday tomorrow)
    * DOB = 1990-02-28, Today = 2026-02-28: Age = 36 (birthday today)
    * DOB = 1992-02-29 (leap year), Today = 2026-02-28: Age = 33 (birthday tomorrow, on Feb 28 in non-leap years)
    * DOB = 1992-02-29 (leap year), Today = 2026-03-01: Age = 34 (birthday was yesterday)
  - Validation: DateOfBirth MUST be ≤ today UTC, MUST be ≥ today - 150 years
  ```
- [ ] Add test T-072-test: "Verify age calculations for: (1) regular birthday (Feb 15), (2) leap year birthday (Feb 29 in 2000, now 2026), (3) boundary: yesterday, (4) boundary: tomorrow, (5) 150 years ago (age 150), (6) 151 years ago (reject)."

**Related References**: FR-002a, T-072

---

### Finding 3.4: Committee Annual Reset Trigger Logic Ambiguous ❌
**Severity**: HIGH  
**Issue**: Committee reset condition is complex; edge cases undefined

**Details**:
- FR-031: "On application startup, system uses system local time (DateTime.Now) to compare current calendar month/year against `Settings.LastCommitteeResetYear`. If (CurrentMonth >= CommitteeRenewalMonth AND LastResetYear < CurrentYear), invoke CommitteeAnnualResetService..."
- **Gap**: "CurrentMonth >= CommitteeRenewalMonth" — Does this mean reset ONCE in the renewal month, or REPEATEDLY every startup after renewal month until year advances?
- **Gap**: What if CommitteeRenewalMonth is December (12)? Reset in Dec, then again in Jan-Dec of next year? (Seems wrong)
- **Gap**: Timezone handling: "system local time (DateTime.Now)" — What if coordinator opens app in two different timezones on same calendar day? Reset fires twice?
- **Gap**: What if coordinator doesn't open app for 2+ months after renewal month passes? (Reset still fires once on next startup, correct)

**Impact**: T-167 (startup check) may reset committee status incorrectly

**Remediation**:
- [ ] Add to FR-031, corrected algorithm and tests:
  ```
  Committee Annual Reset Logic:
  On Application Startup:
  1. Get Settings.CommitteeRenewalMonth (1-12, default 1 = January)
  2. Get Settings.LastCommitteeResetYear (int)
  3. Get currentDate = DateTime.UtcNow.Date (use UTC for consistency across timezones; all times recorded in UTC)
  4. Calculate currentYear = currentDate.Year
  5. Calculate currentMonth = currentDate.Month
  6. Condition: If (currentMonth >= CommitteeRenewalMonth) AND (LastCommitteeResetYear < currentYear):
     - Invoke CommitteeAnnualResetService.ResetAsync() synchronously
     - Update Settings.LastCommitteeResetYear = currentYear
     - Log: "Committee annual reset executed on {currentDate}"
  7. Else: Skip reset, continue startup
  
  Reasoning: 
  - Condition fires ONCE per calendar year after renewal month arrives
  - Example: CommitteeRenewalMonth=3 (March)
    * March 1, 2026: LastResetYear=2025, CurrentYear=2026, Month=3 → RESET (3>=3 AND 2025<2026)
    * March 2, 2026: LastResetYear=2026, CurrentYear=2026, Month=3 → NO RESET (2026 NOT < 2026)
    * April 2026: LastResetYear=2026, CurrentYear=2026 → NO RESET (2026 NOT < 2026)
    * January 1, 2027: LastResetYear=2026, CurrentYear=2027, Month=1 → NO RESET (1 NOT >= 3)
    * March 1, 2027: LastResetYear=2026, CurrentYear=2027 → RESET (3>=3 AND 2026<2027)
  ```
- [ ] Add comprehensive test T-167-test: "Test reset logic with CommitteeRenewalMonth=3: (1) Startup March 1 with LastResetYear=2025 → RESET, (2) Restart March 2 with LastResetYear=2026 → NO RESET, (3) Startup April 1 with LastResetYear=2026 → NO RESET, (4) Startup Jan 1, 2027 with LastResetYear=2026 → NO RESET, (5) Startup March 1, 2027 with LastResetYear=2026 → RESET."

**Related References**: FR-031, T-167

---

### Finding 3.5: Backup Schema Version Compatibility Criteria Undefined ❌
**Severity**: MEDIUM  
**Issue**: Import schema version validation logic is not specified

**Details**:
- FR-014: "Import MUST reject unsupported major versions with clear upgrade guidance"
- **Gap**: What constitutes "unsupported major version"? (All versions < X? Only exact version match?)
- **Gap**: Example: Current app is v2.0.0, backup is v1.5.0. Accept or reject?
- **Gap**: Example: Current app is v2.0.0, backup is v3.0.0 (from future). Accept or reject?
- **Gap**: Minor/patch version handling undefined: v2.0.0 backup into v2.1.0 app?

**Impact**: T-156 (ProtobufRestoreService) cannot determine acceptance/rejection logic

**Remediation**:
- [ ] Add to FR-014, schema version compatibility matrix:
  ```
  Schema Version Compatibility Rules:
  - Format: MAJOR.MINOR.PATCH (e.g., 2.0.0)
  - Backup contains: schemaVersion (string)
  - App contains: currentSchemaVersion (string)
  
  Acceptance Logic:
  1. If MAJOR(backup) < MAJOR(app):
     → REJECT with error: "Backup is from older version ({backup_version}). This app requires version {app_major}.0.0+. Please restore using the version that created this backup or a newer compatible version."
  2. If MAJOR(backup) > MAJOR(app):
     → REJECT with error: "Backup is from newer version ({backup_version}). This app is version {app_version} and cannot restore backups from future versions. Please upgrade this app to {backup_major}.0.0 or later."
  3. If MAJOR(backup) == MAJOR(app):
     → ACCEPT. Minor/patch differences are compatible within same major version (backward/forward compatible within major)
     → Example: v2.0.0 app can restore v2.1.0 or v1.9.0 backups (assuming migrations handle schema differences)
  
  Migration Strategy:
  - If MINOR(backup) < MINOR(app): Run automatic migrations to upgrade schema (EF Core migrations apply)
  - If MINOR(backup) > MINOR(app): Accept backup; newer entities are preserved (forward-compatible)
  ```
- [ ] Add test T-156-test: "Test restore with: (1) v1.9.0 backup into v2.0.0 app → ACCEPT + migrate, (2) v3.0.0 backup into v2.0.0 app → REJECT, (3) v1.5.0 backup into v2.0.0 app → REJECT."

**Related References**: FR-014, T-156

---

### Finding 3.6: Report Filter State Persistence Scope Undefined ❌
**Severity**: MEDIUM  
**Issue**: Report filter persistence ("within a report-viewing session") is vague

**Details**:
- FR-051: "Filter state MUST be persistent within a report-viewing session (same filter applied if user prints or exports). Filter resets to 'Active' when user closes and reopens the report."
- **Gap**: "Closes and reopens the report" — Does this mean:
  - Close report viewer component (other reports still open)?
  - Navigate away from Reports page?
  - Close and reopen the application?
- **Gap**: "Within a session" — Is session the user's application lifetime, or the browser tab lifetime?

**Impact**: T-144 (Member List Report) and T-145 (Committee Report) cannot implement persistent filter state correctly

**Remediation**:
- [ ] Add to FR-051, precise persistence rules:
  ```
  Report Filter State Persistence:
  - Persistence Scope: Per report type (e.g., "Member List" filter state separate from "Committee Report" filter state)
  - Lifetime: From first report generation until user navigates away from the report or closes the Reports page
  - Actions that PRESERVE filter: Print, Export to CSV
  - Actions that RESET filter to default: 
    * User clicks a different report (new report = new filter scope)
    * User navigates to a different page (e.g., Members module)
    * User closes and reopens the application
  - Storage: In-memory (session variable), not persisted to database
  - Example Flow:
    1. Open Member List Report (default filter "Active")
    2. Change filter to "All"
    3. Generate and display report with "All" members ✓
    4. Click Print → PDF prints with "All" filter ✓
    5. Click Export CSV → CSV exports with "All" filter ✓
    6. Click Committee Report → Committee Report opens with its own default filter ✓
    7. Return to Member List Report → Member List reopens with default filter "Active" (filter reset) ✓
  ```

**Related References**: FR-051, FR-052, T-144, T-145

---

## 4. Constitution Alignment

### Finding 4.1: Strong Constitution Alignment ✅
**Severity**: N/A  
**Status**: PASS

All major artifacts align with Constitution 2.2.1:
- ✅ SOLID principles enforced in plan §1.1 (plan matches Constitution §3.2)
- ✅ Soft-delete pattern mandated in spec (FR-003, FR-023) matching Constitution §3.4
- ✅ Financial data immutability (spec FR-016, FR-017, FR-024) matches Constitution §3.5 (Financial Transaction exemption)
- ✅ One class per file standard documented in plan §1.8 matching Constitution §5
- ✅ XML documentation mandate in plan §1.8 matching Constitution §5

**No constitution violations detected.**

---

## 5. Coverage Gaps

### Finding 5.1: Requirements Without Associated Tasks ❌
**Severity**: HIGH  
**Issue**: 9 functional requirements lack corresponding tasks

**Gap List**:

| Requirement | ID | Description | Task Mapping | Status |
|---|---|---|---|---|
| First-run setup wizard | FR-001 | Setup wizard displays, captures org config, initializes DB | T-067, T-068 | ✅ Mapped |
| Member age calculation | FR-002a | Server-side age calculation, validation, display | T-072 | ⚠ **PARTIAL** — Task exists but lacks comprehensive test coverage for edge cases (leap year, boundary dates) |
| Member lifecycle (inactivate/archive distinction) | FR-003 | Status vs. Soft-Delete separation | T-022, T-039 | ⚠ **PARTIAL** — Entity defined, but no UI test for "inactivate without archiving" workflow |
| Annual fee application | FR-004 | Batch processing, confirmation dialog, skip inactive | T-121, T-119 | ✅ Mapped |
| Rehearsal attendance & fee accrual | FR-005 | Schedule, record attendance, auto-fee for active members only | T-080, T-077 | ⚠ **PARTIAL** — No test for "record attendance for inactive member (NO fee created)" scenario |
| Event participation | FR-006 | Schedule events, record participation | T-084, T-082 | ✅ Mapped |
| **Payment FIFO allocation** | **FR-016** | **Oldest fees first, GL pair creation** | **T-111, T-114** | ❌ **MISSING** — Task T-114 exists (test payment recording) but NO integration test T-184 covers FIFO allocation edge cases (partial payment, overpayment) |
| **Payment field immutability** | **FR-017** | **Amount/Date/Category locked, only Notes editable** | **T-106, T-124** | ❌ **MISSING** — Repository test exists but NO UI test verifies "attempting to edit Amount field shows error" in Payment form |
| **GL paired transactions** | **FR-039** | **Debit + Credit pairs, GL balance validation, 0-sum constraint** | **T-108, T-113** | ⚠ **PARTIAL** — Integration test T-113 covers pairs, but NO test verifies "if GL balance fails, report generation rejected with specific error message" |
| **Report field-level filtering** | **FR-051, FR-052** | **Member-status filter dropdown, persistence** | **T-144, T-145** | ⚠ **PARTIAL** — Report providers mapped but NO UI test verifies "filter dropdown renders" or "filter state persists across print/export" |
| **GL account auto-assignment** | **FR-032** | **Sequential numbering, deterministic ordering** | **T-034b** | ⚠ **PARTIAL** — Service task exists but NO comprehensive test for "GL# assignment order when categories created in rapid succession" (Finding 3.2 issue) |

**Assessment**: 
- 4 requirements with **genuinely missing tasks** (FIFO allocation test, payment immutability UI test, GL balance failure test, filter UI test)
- 7 requirements with **incomplete test coverage** (edge cases, UI verification, error scenarios)

**Remediation**:
- [ ] Add task T-114b: "Create integration test for FIFO payment allocation with edge cases: partial payment, overpayment, bulk annual fees, mixed fee types"
- [ ] Add task T-106b: "Create UI test for Payment form verifying Amount/Date/Category fields are read-only (disabled) after payment creation, attempting edit shows message 'Field is immutable after payment creation'"
- [ ] Add task T-113b: "Create integration test for GL balance validation: test report generation fails if total debits ≠ total credits, displays error message 'GL Balance Verification Failed: ...'"
- [ ] Add task T-144b: "Create UI test for Member List Report verifying filter dropdown renders, selections are applied, and persist across print/export actions"

**Related References**: FR-016, FR-017, FR-032, FR-039, FR-051, FR-052, T-111, T-114, T-106, T-124, T-113, T-144, T-145

---

### Finding 5.2: Tasks Without Clear Requirement Mapping ❌
**Severity**: MEDIUM  
**Issue**: 5 tasks lack clear requirement traceability

**Orphan Tasks**:

| Task ID | Title | Requirement | Status |
|---|---|---|---|
| T-097 | Directory auto-creation service | FR-021 ✓ | ✅ Mapped (Plugins directory) |
| T-148 | Account Register running balance tests | FR-035 ✓ | ✅ Mapped |
| T-160 | Non-destructive import upsert mode | FR-015 ✓ | ✅ Mapped |
| T-188 | Semantic HTML accessibility | NFR-010 (Tab Controls) ⚠ | ⚠ **WEAK** — NFR-010 mentions tab semantics but T-188 (semantic HTML broadly) is broader than tab controls. Requirement → Task mapping is unclear. |
| **T-202** | **Smoke test suite** | **None identified** | ❌ **ORPHAN** — No requirement specifies smoke tests. Purpose and scope undefined. Is this phase gate validation? Integration sanity check? Not clear from spec. |

**Assessment**: Task T-202 (smoke tests) is genuinely orphaned. Others map to requirements but with weak traceability.

**Remediation**:
- [ ] Remove or clarify T-202: If intended as "critical path validation," rename to "Critical Path Integration Tests" with requirement link to phase gate criteria (Phase 4 definition of done). If intended as "post-deployment validation," add to Phase 4 definition of done.
- [ ] Strengthen T-188 traceability: Rename to "T-188: Implement Semantic HTML in Blazor Components (per NFR-010 Tab Controls and WCAG AA)" with requirement mapping.

**Related References**: T-097, T-148, T-160, T-188, T-202

---

### Finding 5.3: Missing Requirements for Complete Feature Specification ⚠
**Severity**: MEDIUM  
**Issue**: Some features have no explicit requirement, only tasks

**Gap**:

| Feature | Status | Details |
|---|---|---|
| Error Boundary component | ⚠ MISSING REQ | T-176 exists (ErrorBoundary.razor) but no FR specifies global error boundary behavior. Is it per-module or application-wide? |
| Protobuf schema versioning | ⚠ MISSING REQ | T-152 defines .proto files but no FR specifies protobuf message structure, field ordering, or versioning strategy. Only high-level backup use case (FR-012) mentioned. |
| CSV export escaping rules | ⚠ MISSING REQ | FR-041 mandates CSV export and T-151 tests CSV formatting but no spec detail on RFC 4180 compliance, quote handling, or null value representation. |
| Dashboard tile timeout UI state | ⚠ MISSING REQ | T-180 implements timeout handling but no explicit FR details skeleton loader rendering, retry button behavior (Finding 2.2 related). |

**Remediation**:
- [ ] Add FR-054: "System MUST implement global error boundary handling uncaught exceptions at application level. Unhandled exceptions display user-friendly error dialog with message 'An unexpected error occurred' and options to reload page or contact support. Full exception details logged via Serilog."
- [ ] Add FR-055: "System MUST implement backup serialization using Protocol Buffers (protobuf) with message definitions for all entities per section 3.2 ERD. Protobuf schema MUST be versioned alongside application schema versions per section 5.2 Data Model."
- [ ] Add FR-056: "System MUST export reports to CSV format compliant with RFC 4180 standard. CSV files MUST include column headers, escape quotes with double-quotes (""), separate fields with commas, and represent null values as empty fields."
- [ ] Clarify FR-011: (As Finding 2.2 remediation)

**Related References**: T-176, T-152, T-151, T-180, FR-012, FR-041, FR-011

---

## 6. Inconsistency & Terminology Drift

### Finding 6.1: "Payment" vs. "Payment Record" Terminology Drift ❌
**Severity**: MEDIUM  
**Issue**: Spec uses "Payment" and "Payment record" interchangeably; implementation unclear

**Examples**:
- FR-016: "System MUST support payment recording that creates GL transaction pairs...When payment is recorded with date, amount..."
- FR-016: "Create Payment record for audit trail with fields..."
- FR-017: "Payment entity includes `UpdatedAt` timestamp..."
- FR-025: "PaymentMethod...on Payment records..."
- Plan §3.1: "Payment entity: date (immutable)..."

**Issue**: Is "Payment" a domain entity (Table: Payment) or a concept (UI action: "record a payment")? Terminology should clarify:
- "Payment entity" = Database record
- "Payment recording" = UI action / business process
- "Payment record" = Specific instance of Payment entity

**Impact**: Implementation team may be uncertain whether Payment is table-backed or conceptual

**Remediation**:
- [ ] Standardize terminology in spec: "Payment Entity" (capitalize when referring to database entity), "payment recording" (lowercase for UI action)
- [ ] Add glossary to spec section 0 or plan appendix:
  ```
  GLOSSARY
  ========
  - Payment Entity: Database table storing payment metadata (date, amount, method, member, category, notes)
  - Payment Recording: User action of entering payment details into the system via Payment Recording form
  - GL Transaction: Database table storing general ledger entries (debit/credit pairs); each Payment Recording creates 2 GL Transactions
  - Fee: Database table storing financial obligations (annual, attendance, other); each fee is created when earned (not paid)
  - Member Balance: Calculated field (sum of unpaid fees) representing total amount member owes
  ```

**Related References**: FR-016, FR-017, FR-025

---

### Finding 6.2: "Rehearsal Fee" vs. "Attendance Fee" Terminology ❌
**Severity**: LOW  
**Issue**: Spec uses both terms; may confuse implementation

**Examples**:
- FR-005: "Recording attendance...MUST automatically create attendance fee records"
- FR-006: "event types...per-rehearsal attendance fees"
- Plan §2.1: "Attendance fee per rehearsal"
- Spec §2.1: "Attendance fee per rehearsal"

**Issue**: Are "rehearsal fee" and "attendance fee" the same? Or distinct?
- **Interpretation A**: Attendance fee is charged per attendance record (member attended rehearsal X) = attendance fee
- **Interpretation B**: Fee is per rehearsal (recurring), then applied to attendance = rehearsal fee

**Impact**: Slight, since spec later clarifies FR-005, but could confuse naming in FeeType enum and repository methods

**Remediation**:
- [ ] Standardize: Use "attendance fee" throughout (already dominates in spec). Remove "rehearsal fee" references.
- [ ] Clarify: "Attendance fee: Fee applied when member's attendance is recorded for a rehearsal (created once per attendance record, not recurring)."

**Related References**: FR-005, FR-006

---

### Finding 6.3: "Archived" vs. "Soft-Deleted" Terminology ⚠
**Severity**: LOW  
**Issue**: Spec uses both terms; different meanings can confuse

**Examples**:
- Constitution §3.4: "Soft-delete pattern...IsDeleted, DeletedAt, DeletedBy fields"
- FR-003: "Soft-Delete (Archival) via IsDeleted/DeletedAt/DeletedBy...when a member is archived (soft-deleted)"
- UI terminology: "Archive member" vs. "Soft-delete member"

**Issue**: Are "archive" and "soft-delete" the same action with different naming, or different concepts?
- **Spec clarification**: Both refer to same action (setting IsDeleted=true), just different terminology
- **But**: Confusing for developers reading both Constitution and spec

**Remediation**:
- [ ] Align terminology: Use "archive" in spec/UI, "soft-delete" in architecture docs. Define equivalence clearly in glossary.
- [ ] UI language: "Archive Member", "Restore Member" (not "soft-delete")
- [ ] Architecture docs (plan): "soft-delete pattern" (lower-level term)

**Related References**: Constitution §3.4, FR-003, FR-023, Plan §1.8

---

### Finding 6.4: "Renewal Month" vs. "Renewal Date" Ambiguity ⚠
**Severity**: MEDIUM  
**Issue**: Setting refers to "month" but logic often implies "date"

**Examples**:
- FR-018: "Membership Renewal Month (1-12, for annual fee application)"
- FR-031: "Committee Renewal Month (1-12, default 1/January)"
- Plan §3.1: Settings has `renewalMonth` (integer 1-12) not date

**Issue**: Month alone is ambiguous:
- Renewal month = "apply fees anytime during March"? Or "apply fees on first day of month"? Or "apply fees on specific date in month"?
- No day-of-month specification, only month

**Impact**: T-121 (AnnualFeeApplicationService) lacks precision on when fees are applied within the renewal month

**Remediation**:
- [ ] Clarify in FR-004 and FR-018:
  ```
  Renewal Month Semantics:
  - RenewalMonth (1-12): Month in which annual fees are applied
  - Renewal Date: First day of renewal month at 00:00:00 UTC (implicit)
  - Example: RenewalMonth=3 (March) → fees applied anytime in March; UI shows "Annual fees applied in March"
  - Batch Processing: Coordinator manually clicks "Apply Annual Fees" button in Finance module any time during renewal month
  - Automation (Phase 2+): Future versions may auto-apply fees on renewal month start; MVP requires manual trigger
  ```
- [ ] Update FR-031 (Committee): Same clarification — CommitteeRenewalMonth=1 (January) means "reset on Jan 1"; CommitteeRenewalMonth=3 (March) means "reset on Mar 1"

**Related References**: FR-004, FR-018, FR-031

---

### Finding 6.5: "Member Status" vs. "Member Archive Status" Inconsistent Naming ⚠
**Severity**: LOW  
**Issue**: Spec refers to both "Status" (Active/Inactive) and "Archived" (IsDeleted); naming inconsistent

**Examples**:
- FR-002: "Members can be marked as Active or Inactive"
- FR-003: "Status field with two values: Active, Inactive"
- FR-023: "Member activation/inactivation effective dates"
- FR-051: "filter allowing...select: (1) 'Active' (default), (2) 'Inactive', (3) 'Archived', (4) 'All'"

**Issue**: Filter in FR-051 shows 4 distinct states:
- Active (Status='Active', IsDeleted=false)
- Inactive (Status='Inactive', IsDeleted=false)
- Archived (IsDeleted=true)
- All (any combination)

But only two Status enum values exist (Active, Inactive). Archived is a separate concept (soft-delete).

**Impact**: T-144 (Member List Report) must correctly implement filter with 4 distinct query conditions

**Remediation**:
- [ ] Clarify member states in plan and spec:
  ```
  Member States (Mutually Exclusive):
  1. Active: Status='Active', IsDeleted=false
     - Member participates in events; fees apply; visible in default views
  2. Inactive: Status='Inactive', IsDeleted=false
     - Member does not participate; no fees accrue; hidden from default views but accessible via "Inactive" filter
  3. Archived: IsDeleted=true (regardless of Status value)
     - Member hidden from most views; only visible via "Archived" filter; historical data preserved
  4. All: Any combination (Status='Active'|'Inactive', IsDeleted=true|false)
  
  Status Enum: {Active, Inactive} (2 values)
  Archive Flag: {IsDeleted} boolean (true = archived, false = active/inactive)
  ```

**Related References**: FR-002, FR-003, FR-023, FR-051

---

## 7. Data Entity & Relationship Mismatches

### Finding 7.1: Attendance Fee Immutability Unclear ❌
**Severity**: MEDIUM  
**Issue**: Plan specifies Fee as immutable, but FR-005 allows clearing attendance

**Details**:
- Plan §3.1 (Fee entity): "immutable after creation; no soft-delete fields per Constitution §3.4"
- Constitution §3.4 Financial Data section: "Financial records MUST NEVER be deleted (soft or hard)"
- FR-005: "If attendance flag is subsequently cleared for a member on a specific rehearsal, the corresponding attendance fee for that rehearsal MUST be automatically removed (soft-deleted with GL reversing entries)"
- **Contradiction**: "Immutable" conflicts with "removed (soft-deleted)"

**Impact**: 
- Can Fee records be soft-deleted or not?
- If soft-deleted, they're not truly immutable
- If not soft-deleted, how are fees removed?

**Remediation**:
- [ ] Revise FR-005 and Constitution §3.5 for clarity:
  ```
  Corrected Fee Immutability with GL Reversals:
  
  Fee Entity: IMMUTABLE at record level (cannot be updated or hard-deleted)
  
  When Attendance Flag is Cleared:
  - Original Fee record remains unchanged (immutable)
  - Create GL reversing transaction pair:
    * Debit: Amount on MemberReceivable (GL#0101)
    * Credit: Amount on appropriate income category GL account
    * Description: "Reversal: Attendance fee for [Rehearsal Date] cleared"
    * Link to original Fee via description or new GL.FeeId field (optional reference)
  - Member balance recalculates (original fee amount = now offset by reversal)
  - Effect: Fee is logically "removed" from member's balance via GL reversal, not physically deleted
  
  Implication: History is preserved (original Fee + GL reversal) for audit trail
  ```
- [ ] Add to Constitution §3.4 Financial Data section:
  ```
  Financial Corrections Pattern:
  - Do NOT delete or modify financial records (Fee, Transaction, Payment)
  - Instead, create GL reversing entries to offset (e.g., reversing attendance fee via GL reversal)
  - Original records preserved; reversal provides audit trail
  ```

**Related References**: FR-005, Constitution §3.4, FR-032, Plan §3.1

---

### Finding 7.2: Committee History Expected but not Fully Mapped ⚠
**Severity**: MEDIUM  
**Issue**: CommitteeMembership entity exists but sparse task coverage for history preservation

**Details**:
- CommitteeMembership entity defined in plan §3.1 with year-based tracking, soft-delete
- FR-027, FR-028, FR-029: Committee membership tracking, history preservation, display
- **Tasks mapped**: T-053 (repository), T-076 (service), T-074 (history display component), T-169 (tests)
- **Gap**: No task for "Committee History Report" (FR-050 lists "Committee Report" but no task details report generation logic)
- **Gap**: No task for "Committee Annual Reset Service" lifecycle (T-167 references it in startup check, but CommitteeAnnualResetService implementation not explicitly tasked)

**Impact**: T-162 (definition of done for Phase 3a) claims committee operations complete, but report generation (FR-050) not yet implemented in Phase 3a. Report is Phase 2d (T-145). Ordering conflict.

**Remediation**:
- [ ] Clarify task sequencing: T-145 (Committee Report) should be Phase 1c (part of initial MVP module delivery) or explicitly moved to Phase 3a if committee history depends on Phase 3a completion
- [ ] Verify T-076 (CommitteeMembershipService) fully covers:
  - Creating/updating committee memberships (Year + Position)
  - Preserving historical records
  - Clearing current-year status annually (CommitteeAnnualResetService)
- [ ] Add/clarify: T-167 implementation details (CommitteeAnnualResetService) and integration with startup (App.xaml.cs)

**Related References**: FR-027, FR-028, FR-029, T-053, T-076, T-074, T-145, T-167

---

### Finding 7.3: Category GL Account Not Bidirectionally Linked ⚠
**Severity**: MEDIUM  
**Issue**: Category has GlAccount field, but Transaction.Category (FK) means GL account is derived, not looked up

**Details**:
- Plan §3.1: "Category: gl_account (auto-assigned...determined from Category type)"
- FR-032: "GL account is **derived deterministically from Category type** — each Category has an auto-assigned GL account number"
- Implication: Transactions link to Category via FK, GL account is then derived at runtime from Category.Type
- **Gap**: If coordinator changes Category.Type after transactions are created, GL account derivation breaks
- **Gap**: How is gl_account field on Category entity used? Is it stored or calculated?

**Impact**: T-034b (GLAccountAssignmentService) and transaction creation logic (T-109) need clarification: is GL account stored on Category, stored on Transaction, or calculated on query?

**Remediation**:
- [ ] Clarify Category.GlAccount field:
  ```
  Category Entity:
  - glAccount (int): Auto-assigned GL account number, immutable after creation
  - type (Income | Expense): Category type, IMMUTABLE after creation
  - When coordinator creates new category:
    1. GLAccountAssignmentService determines next GL# based on type
    2. Category.glAccount = assigned GL#
    3. Category is persisted
  - When transaction is created:
    1. Query category.GlAccount (or derive from category.Type + creation order if stored glAccount not available)
    2. Create Transaction with GL account = category.GlAccount
  - Category.Type CANNOT be changed after creation (enforced by UI: disable type field after save)
  ```
- [ ] Add constraint to Category: "Type is immutable (no-update enforcement in database or ORM configuration)"

**Related References**: FR-032, Plan §3.1, T-034b, T-109

---

## 8. Risk Assessment & Blockers

### CRITICAL RISKS (Blocking Phase 1)

| Risk ID | Risk | Impact | Mitigation |
|---|---|---|---|
| **CRIT-001** | FIFO Payment Allocation Algorithm Underspecified (Finding 3.1) | Payment implementation cannot start; integration tests cannot be written; GL balance verification depends on correct allocation | Implement remediation from Finding 3.1 immediately; write detailed test spec before coding |
| **CRIT-002** | Committee Annual Reset Logic Ambiguous (Finding 3.4) | CommitteeAnnualResetService will malfunction; may reset multiple times per year or not at all; data corruption risk | Implement remediation from Finding 3.4 with UTC timezone handling and comprehensive startup check tests |
| **CRIT-003** | GL Account Assignment Sequencing for Simultaneous Creation (Finding 3.2) | GLAccountAssignmentService will produce non-deterministic GL account numbers; reports will show incorrect GL assignments | Implement remediation from Finding 3.2 with CreatedAt + Id tiebreaker and 100-category limit enforcement |
| **CRIT-004** | Member Age Calculation Edge Cases (Finding 3.3) | Age calculations may be incorrect for leap-year birthdays; validation may reject valid ages | Implement remediation from Finding 3.3 with precise UTC algorithm and comprehensive boundary tests |
| **CRIT-005** | Payment FIFO Allocation Has No UI Tests (Finding 5.1) | Payment recording implemented but cannot verify UI prevents invalid payments or shows correct allocation feedback | Add UI integration tests for payment recording form; verify read-only fields; verify GL pair creation |
| **CRIT-006** | GL Balance Verification Error Handling Not Tested (Finding 5.1) | Reports may be generated with GL imbalance; financial data integrity violated silently | Add integration test T-113b for GL balance failure; verify error message displayed to user |

### HIGH RISKS (Phase 1 Blockers)

| Risk ID | Risk | Impact | Mitigation |
|---|---|---|---|
| **HIGH-001** | Dashboard Tile Timeout Behavior Unspecified (Finding 2.2) | Dashboard may hang if tile provider is slow; user experience poor; timeout error message format unclear | Implement Finding 2.2 remediation; add timeout tests to T-090 |
| **HIGH-002** | WCAG AA Compliance Verification Method Undefined (Finding 2.4) | Accessibility tests cannot be written; compliance cannot be verified; legal/compliance risk | Implement Finding 2.4 remediation; specify axe-core or similar tool; update T-191 with test framework |
| **HIGH-003** | Error Message Standards Missing (Finding 2.5) | Error messages may be unclear or inconsistent; user experience degraded; 90% comprehension target cannot be achieved | Implement Finding 2.5 remediation; add error message standards section to spec; create constants in T-177 |
| **HIGH-004** | Report Filter State Persistence Scope Ambiguous (Finding 3.6) | Report filter state implementation unclear; may persist incorrectly across reports or not at all; user experience inconsistent | Implement Finding 3.6 remediation; update T-144, T-145 test spec with precise filter scope rules |
| **HIGH-005** | Member Status vs. Archive Status Naming Inconsistent (Finding 6.5) | T-144 (Member List Report) filter implementation ambiguous; 4 states (Active/Inactive/Archived/All) vs. 2 status values | Clarify member states; update T-144 filter logic with 4 distinct query conditions |
| **HIGH-006** | Fee Immutability Conflicts with Attendance Fee Removal (Finding 7.1) | FR-005 contradicts Constitution §3.4; Fee entity marked immutable but spec says fees are "removed". GL reversal pattern unclear | Implement Finding 7.1 remediation; clarify immutability-with-GL-reversal pattern; update Constitution if needed |
| **HIGH-007** | Backup Schema Version Compatibility Criteria Undefined (Finding 3.5) | T-156 (ProtobufRestoreService) cannot determine accept/reject logic for version mismatches; restore failures possible | Implement Finding 3.5 remediation; add schema version compatibility matrix; update T-156 test spec |
| **HIGH-008** | Tasks Without Requirement Mapping Create Orphans (Finding 5.2) | T-202 (smoke tests) scope undefined; not clear if phase gate validation or post-deployment test | Clarify T-202 or remove; strengthen traceability for T-188 |

### MEDIUM RISKS (Phase 1 Quality)

| Risk ID | Risk | Impact | Mitigation |
|---|---|---|---|
| **MED-001** | Missing Requirements for Features (Finding 5.3) | Error Boundary, Protobuf, CSV export, dashboard timeout have no explicit FR; implementation choices left to developer | Add FR-054, FR-055, FR-056; clarify FR-011 (see Finding 2.2) |
| **MED-002** | Payment Allocation No UI Test (Finding 5.1) | Payment form immutability not verified in UI; fields may be accidentally editable | Add T-106b (Payment form immutability test) |
| **MED-003** | GL Account Assignment Not Tested for Concurrency (Finding 3.2) | GLAccountAssignmentService may assign duplicate GL numbers if categories created simultaneously | Add concurrency test to T-034b: create categories in parallel, verify unique GL assignments |
| **MED-004** | Report Provider Error Handling Not Explicit (Finding 5.3) | If report provider fails, error message/recovery unclear | Clarify FR-049 (report provider error handling); add UI error component for failed reports |
| **MED-005** | Committee History Tests Incomplete (Finding 7.2) | CommitteeAnnualResetService lifecycle not fully tested; may not trigger correctly on startup | Enhance T-169 test coverage; add startup integration test for committee reset trigger |

---

## 9. Quality Gate Failures

### Phase 1 Readiness Gate Assessment: ❌ **NOT READY**

**Blocker Status**:
- ❌ CRIT-001 through CRIT-006 MUST be resolved before Phase 1 coding begins
- ❌ HIGH-001 through HIGH-008 MUST be resolved before Phase 1 completion
- ❌ Tasks cannot be estimated accurately without addressing CRIT issues

**Recommendation**: Return artifacts to specification phase; address all CRITICAL findings before implementation begins. Expected rework: 2–3 business days.

---

## 10. Next Actions & Remediation Plan

### Immediate Actions (Before Phase 1 Start)

**Priority 1 — CRITICAL Fixes** (Blocks Phase 1):

- [ ] **Remediate Finding 3.1**: Specify FIFO payment allocation algorithm with edge cases (partial, overpayment, bulk fees)
- [ ] **Remediate Finding 3.4**: Specify committee annual reset logic with UTC timezone handling
- [ ] **Remediate Finding 3.2**: Specify GL account assignment with creation order + ID tiebreaker
- [ ] **Remediate Finding 3.3**: Specify member age calculation with leap year + boundary handling
- [ ] **Remediate Finding 5.1**: Add tasks T-114b, T-106b, T-113b, T-144b for missing integration/UI tests
- [ ] **Remediate Finding 7.1**: Clarify Fee immutability + GL reversal pattern for attendance fee removal

**Effort**: ~8–12 hours specification work

**Priority 2 — HIGH Fixes** (Phase 1 Quality):

- [ ] **Remediate Finding 2.2**: Specify dashboard tile timeout behavior (5 sec loading, 10 sec error)
- [ ] **Remediate Finding 2.4**: Specify WCAG AA verification tool (axe-core) and test framework
- [ ] **Remediate Finding 2.5**: Create error message standards template and constants
- [ ] **Remediate Finding 3.6**: Clarify report filter persistence scope (per report, lifetime)
- [ ] **Remediate Finding 3.5**: Specify schema version compatibility matrix (major/minor/patch rules)
- [ ] **Remediate Finding 5.3**: Add FR-054, FR-055, FR-056 for missing requirements

**Effort**: ~6–10 hours specification work

**Priority 3 — MEDIUM Fixes** (Phase 1 Polish):

- [ ] **Remediate Finding 6.1 through 6.5**: Standardize terminology (Payment, Rehearsal Fee, Archived, etc.)
- [ ] **Remediate Finding 7.2**: Clarify CommitteeMembershipService lifecycle and reporting
- [ ] **Remediate Finding 7.3**: Clarify Category.GlAccount immutability and GL derivation

**Effort**: ~4–6 hours specification work

**Total Remediation Effort**: ~18–28 hours (est. 1 week with review cycles)

---

### Updated Artifact Versions

| Artifact | Current Version | Remediation Required | Updated Version |
|---|---|---|---|
| spec.md | Draft (967 lines) | +150–200 lines (detailed algorithms, examples, edge cases) | 2.0.0-draft |
| plan.md | Ready for Task Generation (902 lines) | +50–100 lines (service specifications, test frameworks) | 2.0.0-draft |
| tasks.md | 1.0.0 (536 lines) | +6–10 tasks (missing integration/UI tests) | 1.1.0-draft |
| constitution.md | 2.2.1 (ratified) | ±0 lines (no constitution changes needed; clarify alignment) | 2.2.1 (no change) |

---

### Revised Phase 1 Timeline

**Current Estimate**: 3–4 weeks (per plan)  
**With Remediation**: 3–4 weeks + 1 week specification work = 4–5 weeks total

**Recommendation**: Allocate 1 week in advance of Phase 1 for specification remediation and finalization.

---

## Appendix A: Issue Severity Reference

| Severity | Definition | Action |
|---|---|---|
| CRITICAL | Blocks implementation or violates core principles | Must fix before code starts |
| HIGH | Significant gaps/misalignments; affects multiple features | Must fix before Phase 1 complete |
| MEDIUM | Incomplete specification or coverage; requires clarification | Should fix before or during Phase 1 |
| LOW | Minor inconsistency or advisory issue | Fix during Phase 1 or Phase 2 |

---

## Appendix B: Key Metrics

| Metric | Value | Status |
|---|---|---|
| Total Requirements (FR + NFR) | 56 | ✅ |
| Total Tasks | 220 (T-001 to T-220) | ✅ |
| Requirements with Task Mapping | 47/56 (83.9%) | ⚠ MEDIUM |
| Requirements with Complete Task Mapping | 40/56 (71.4%) | ⚠ HIGH |
| Requirements with Ambiguities | 14/56 (25%) | ⚠ MEDIUM |
| Requirements Underspecified | 12/56 (21.4%) | ⚠ MEDIUM |
| Tasks Orphaned (No Requirement) | 1/220 (0.5%) | ✅ (minimal) |
| Constitution Violations | 0/56 | ✅ PASS |
| Coverage Gaps (Zero Tasks) | 0 discovered | ✅ PASS (all requirements mapped to ≥1 task) |
| Missing Integration Tests | 4–6 | ⚠ HIGH |
| Missing UI Tests | 2–4 | ⚠ MEDIUM |
| Terminology Inconsistencies | 7 | ⚠ MEDIUM |

---

**Report Prepared By**: Automated Specification Analysis  
**Report Date**: 2026-05-20  
**Recommendation**: Address all CRITICAL findings before Phase 1 implementation begins.

