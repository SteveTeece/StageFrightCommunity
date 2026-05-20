# StageFright MVP Specification — Remediation Checklist

**Analysis Date**: 2026-05-20  
**Report Version**: 1.0  
**Purpose**: Track remediation of specification issues before Phase 1 implementation

---

## 🔴 CRITICAL Issues (Must Fix Before Phase 1)

### CRIT-001: FIFO Payment Allocation Algorithm
- **Issue**: Edge cases undefined (partial payment, overpayment, bulk fees)
- **Finding**: 3.1 in main report
- **Remediation**:
  - [ ] Add detailed 4-step FIFO algorithm to FR-016
  - [ ] Document edge cases: partial payment handling, overpayment as member credit, bulk annual fees
  - [ ] Add tiebreaker: Fee.Id ascending if CreatedAt identical
  - [ ] Add comprehensive test case to T-184
  - [ ] Review and approve remediation
- **Estimated Effort**: 2–3 hours
- **Owner**: TBD
- **Status**: ⏳ NOT STARTED

### CRIT-002: Committee Annual Reset Logic
- **Issue**: Timezone handling, duplicate-reset guard, conditional logic complex
- **Finding**: 3.4 in main report
- **Remediation**:
  - [ ] Update FR-031 with UTC timezone specification
  - [ ] Add step-by-step algorithm with Examples
  - [ ] Define condition: `(currentMonth >= CommitteeRenewalMonth) AND (LastCommitteeResetYear < currentYear)`
  - [ ] Add comprehensive startup check tests (T-167-test with 5+ scenarios)
  - [ ] Clarify CommitteeRenewalMonth semantics (month = reset on 1st of month)
  - [ ] Review and approve remediation
- **Estimated Effort**: 2–3 hours
- **Owner**: TBD
- **Status**: ⏳ NOT STARTED

### CRIT-003: GL Account Assignment Sequencing
- **Issue**: Non-deterministic ordering when categories created simultaneously
- **Finding**: 3.2 in main report
- **Remediation**:
  - [ ] Update plan §3.1 GLAccountAssignmentService specification
  - [ ] Specify tiebreaker: CreatedAt ASC, then Id ASC
  - [ ] Add max 100-category limit enforcement (GL#1000-1099 for income, GL#2000-2099 for expense)
  - [ ] Add error message for exceeding limit
  - [ ] Add test T-034b-test for rapid succession creation
  - [ ] Review and approve remediation
- **Estimated Effort**: 2–3 hours
- **Owner**: TBD
- **Status**: ⏳ NOT STARTED

### CRIT-004: Member Age Calculation Edge Cases
- **Issue**: Leap year birthdays, timezone basis, boundary dates
- **Finding**: 3.3 in main report
- **Remediation**:
  - [ ] Update FR-002a with precise UTC algorithm
  - [ ] Replace formula `floor((today - DOB) / 365.25)` with step-by-step year/month/day comparison
  - [ ] Add leap year birthday handling (Feb 29 member born 1992, today 2026-02-28 = age 33)
  - [ ] Specify timezone: Use `DateTime.UtcNow.Date` (UTC, no time component)
  - [ ] Add validation test cases for: regular birthday, leap year birthday, boundaries, 150-year limit
  - [ ] Update T-072 test specification
  - [ ] Review and approve remediation
- **Estimated Effort**: 2–3 hours
- **Owner**: TBD
- **Status**: ⏳ NOT STARTED

### CRIT-005: Missing Integration/UI Tests
- **Issue**: 4-6 critical test gaps for FIFO, GL balance, payment immutability
- **Finding**: 5.1 in main report
- **Remediation**:
  - [ ] Add task T-114b: "Integration test for FIFO payment allocation (partial, overpayment, bulk fees)"
  - [ ] Add task T-106b: "UI test for Payment form read-only fields (Amount/Date/Category locked)"
  - [ ] Add task T-113b: "Integration test for GL balance validation failure → report generation rejected"
  - [ ] Add task T-144b: "UI test for Member List Report filter persistence (filter state preserved across print/export)"
  - [ ] Update tasks.md with new task definitions
  - [ ] Link tasks to phase gates
  - [ ] Review and approve remediation
- **Estimated Effort**: 2–3 hours
- **Owner**: TBD
- **Status**: ⏳ NOT STARTED

### CRIT-006: Fee Immutability vs. GL Reversal Pattern
- **Issue**: FR-005 says fees are "removed" but Constitution §3.4 says financial records are immutable
- **Finding**: 7.1 in main report
- **Remediation**:
  - [ ] Clarify in FR-005: "If attendance flag is cleared, create GL reversing entries (original Fee record remains immutable)"
  - [ ] Update Constitution §3.4 Financial Data section with "Financial Corrections Pattern" subsection
  - [ ] Add example: Original Fee $50 + GL reversal -$50 = net $0 (Fee immutable, balance offset via GL)
  - [ ] Update plan §3.1 Fee entity documentation
  - [ ] Add test T-080-test for "clear attendance → GL reversal created" scenario
  - [ ] Review and approve remediation
- **Estimated Effort**: 2–3 hours
- **Owner**: TBD
- **Status**: ⏳ NOT STARTED

**CRITICAL Subtotal**: 12–18 hours

---

## 🟠 HIGH Issues (Phase 1 Blockers)

### HIGH-001: Dashboard Tile Timeout Behavior
- **Issue**: Exact timeout semantics undefined (5s? 10s? What happens at each?)
- **Finding**: 2.2 in main report
- **Remediation**:
  - [ ] Add to FR-011: "If tile provider does not return data within 5 seconds, display loading skeleton with 'Loading...' text"
  - [ ] Add: "If tile still loading after 10 seconds, replace with error state 'Tile unavailable' + retry button"
  - [ ] Specify retry behavior: "Clicking retry triggers tile refresh (another 5-second attempt)"
  - [ ] Define error tile content: icon + message + retry button
  - [ ] Update T-180 and T-090 with detailed timeout implementation
  - [ ] Review and approve remediation
- **Estimated Effort**: 1–2 hours
- **Owner**: TBD
- **Status**: ⏳ NOT STARTED

### HIGH-002: WCAG AA Compliance Verification Method
- **Issue**: Tool/method not specified; test framework undefined
- **Finding**: 2.4 in main report
- **Remediation**:
  - [ ] Update NFR-004: "WCAG AA compliance verification uses axe-core analyzer"
  - [ ] Specify test framework: "Implement automated contrast ratio validation using ColorContrast.CalculateRatio(fg, bg) ≥ 4.5:1 for text"
  - [ ] Link to WCAG 2.1 AA criteria: SC 1.4.3 (Contrast Minimum), SC 4.1.3 (Status Messages)
  - [ ] Update T-191 with test framework details
  - [ ] Add axe-core configuration to project (NuGet package or similar)
  - [ ] Review and approve remediation
- **Estimated Effort**: 1–2 hours
- **Owner**: TBD
- **Status**: ⏳ NOT STARTED

### HIGH-003: Error Message Standards
- **Issue**: Format, tone, action guidance not specified; 90% comprehension testing method undefined
- **Finding**: 2.5 in main report
- **Remediation**:
  - [ ] Add "Error Message Standards" section to spec
  - [ ] Specify format: plain English, active voice, 2nd person, ≤100 characters
  - [ ] Require: "Explain what went wrong AND what to do next"
  - [ ] Add examples (✅ Good, ❌ Bad)
  - [ ] Define user testing: "≥10 participants, target ≥90% comprehension without explanation"
  - [ ] Create error message constants template in T-177
  - [ ] Review and approve remediation
- **Estimated Effort**: 1–2 hours
- **Owner**: TBD
- **Status**: ⏳ NOT STARTED

### HIGH-004: Report Filter State Persistence
- **Issue**: Scope ambiguous ("within a report-viewing session")
- **Finding**: 3.6 in main report
- **Remediation**:
  - [ ] Update FR-051/FR-052: Define persistence scope precisely
  - [ ] Add example flow: "Generate → Filter change → Print (filter preserved) → Export (filter preserved) → Close report (filter resets)"
  - [ ] Specify reset triggers: "Different report, navigate away from Reports page, close app"
  - [ ] Add storage: "In-memory session variable (not persisted to database)"
  - [ ] Update T-144-test, T-145-test with specific persistence assertions
  - [ ] Review and approve remediation
- **Estimated Effort**: 1–2 hours
- **Owner**: TBD
- **Status**: ⏳ NOT STARTED

### HIGH-005: Schema Version Compatibility Matrix
- **Issue**: Import accept/reject logic not specified
- **Finding**: 3.5 in main report
- **Remediation**:
  - [ ] Add to FR-014: Compatibility matrix (backup v1.5.0 into app v2.0.0 = ACCEPT; v3.0.0 into v2.0.0 = REJECT)
  - [ ] Define rules: MAJOR mismatch = reject, MINOR/PATCH = accept (backward/forward compatible)
  - [ ] Add error messages for unsupported versions
  - [ ] Add migration strategy: EF Core migrations apply if MINOR(backup) < MINOR(app)
  - [ ] Update T-156 test spec with version matrix scenarios
  - [ ] Review and approve remediation
- **Estimated Effort**: 1–2 hours
- **Owner**: TBD
- **Status**: ⏳ NOT STARTED

### HIGH-006: Missing Requirements for Features
- **Issue**: Error Boundary, Protobuf, CSV export, dashboard timeout have no explicit FR
- **Finding**: 5.3 in main report
- **Remediation**:
  - [ ] Add FR-054: "System MUST implement global error boundary handling uncaught exceptions"
  - [ ] Add FR-055: "System MUST implement backup serialization using Protocol Buffers"
  - [ ] Add FR-056: "System MUST export reports to CSV format compliant with RFC 4180"
  - [ ] Update spec with 3 new functional requirements
  - [ ] Link tasks T-176, T-152, T-151 to new requirements
  - [ ] Review and approve remediation
- **Estimated Effort**: 1–2 hours
- **Owner**: TBD
- **Status**: ⏳ NOT STARTED

### HIGH-007: Member Status vs. Archive Status Naming
- **Issue**: 4 distinct states (Active/Inactive/Archived/All) vs. 2 status values; filter implementation ambiguous
- **Finding**: 6.5 in main report
- **Remediation**:
  - [ ] Add glossary section to spec documenting 4 member states
  - [ ] Clarify: "Active (Status='Active', IsDeleted=false), Inactive (Status='Inactive', IsDeleted=false), Archived (IsDeleted=true), All (any)"
  - [ ] Update FR-051 example to show all 4 filter states
  - [ ] Update T-144 (Member List Report) test with 4-condition query verification
  - [ ] Review and approve remediation
- **Estimated Effort**: 1–2 hours
- **Owner**: TBD
- **Status**: ⏳ NOT STARTED

### HIGH-008: Task Orphan & Traceability
- **Issue**: T-202 (smoke tests) orphaned; T-188 weak traceability
- **Finding**: 5.2 in main report
- **Remediation**:
  - [ ] Remove T-202 or redefine as "Critical Path Integration Tests" with Phase 4 gate requirement
  - [ ] Rename T-188: "T-188: Implement Semantic HTML in Blazor Components (per NFR-010 Tab Controls and WCAG AA)"
  - [ ] Update task → requirement mapping in tasks.md
  - [ ] Review and approve remediation
- **Estimated Effort**: 0.5–1 hour
- **Owner**: TBD
- **Status**: ⏳ NOT STARTED

**HIGH Subtotal**: 9–14 hours

---

## 🟡 MEDIUM Issues (Phase 1 Polish)

- [ ] MED-001: Terminology standardization (Payment vs. Payment Record, Rehearsal Fee vs. Attendance Fee)
- [ ] MED-002: Committee history test coverage enhancement
- [ ] MED-003: GL account concurrency testing (simultaneous category creation)
- [ ] MED-004: Report provider error handling clarification
- [ ] MED-005: Fee immutability + GL reversal pattern documentation

**MEDIUM Subtotal**: 4–6 hours (can overlap with CRITICAL/HIGH work)

---

## Summary

| Priority | Count | Effort | Status |
|---|---|---|---|
| 🔴 CRITICAL | 6 | 12–18 hrs | ⏳ NOT STARTED |
| 🟠 HIGH | 8 | 9–14 hrs | ⏳ NOT STARTED |
| 🟡 MEDIUM | 5+ | 4–6 hrs | ⏳ NOT STARTED |
| **TOTAL** | **19+** | **25–38 hrs** | **⏳ NOT STARTED** |

**Recommended Timeline**: 1–2 weeks with review cycles

**Key Milestones**:
- Day 1–2: Address all CRITICAL findings
- Day 3–4: Resolve HIGH findings
- Day 5: Remediation review with stakeholders
- Day 6–7: Re-run analysis and final validation

---

## Sign-Off (To Be Updated)

| Role | Name | Status | Date |
|---|---|---|---|
| Product Owner | TBD | ⏳ | — |
| Architecture Lead | TBD | ⏳ | — |
| QA Lead | TBD | ⏳ | — |
| Phase 1 Lead | TBD | ⏳ | — |

All CRITICAL and HIGH issues must be resolved and signed off before Phase 1 implementation begins.

---

**Last Updated**: 2026-05-20  
**Next Review**: Upon completion of remediation items
