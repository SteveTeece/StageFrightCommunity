# Contract: Attendance Roll (StageFright.Core → StageFright.Reports → StageFright.UI boundaries)

**Feature**: `012-printable-attendance-roll` | **Phase**: 1 (Design)

This is a desktop MAUI Blazor Hybrid application with no external network API — the relevant
"interfaces exposed to other systems" are the two new internal module contracts this feature
introduces, plus the existing contracts it consumes read-only. Per the plan-template guidance,
this document captures those boundaries rather than a REST/CLI schema, since none exists in this
codebase (see `specs/011-member-firstname-lastname/contracts/member-service-contract.md` for the
established precedent of this documentation style).

## New contract: `IAttendanceRollService` (StageFright.Core → StageFright.UI)

```csharp
public interface IAttendanceRollService
{
    Task<AttendanceRollData> GenerateAsync(Guid rehearsalId, CancellationToken ct = default);
}
```

**Preconditions**:
- `rehearsalId` should identify a saved (non-deleted) `Rehearsal`. No other precondition — any
  active-member count, including zero, is a valid input state.

**Postconditions**:
- Returns an `AttendanceRollData` whose `Members` list contains exactly the members returned by
  `IMemberService.GetByStatusAsync(MemberStatus.Active)` at call time (FR-002), ordered by
  `LastName` then `FirstName` (FR-004), each with `AnnualFeePaid` computed per the rule in
  data-model.md / research.md Decision 5 (FR-007).
- Creates, updates, or deletes no `Member`, `Rehearsal`, `Fee`, `Payment`, `Transaction`, or GL
  record — this call has no side effects (spec Assumptions).
- Idempotent and side-effect-free: calling it twice in a row for the same rehearsal with no
  intervening data changes returns equivalent data both times; calling it after data changes (a
  member becomes inactive, a payment is recorded) reflects the new state, not a cached one (spec
  Assumptions: "reflects the active member list at the time of that later generation").

**Failure modes**:
- `EntityNotFoundException("Rehearsal", rehearsalId, nameof(GenerateAsync))` — `rehearsalId` does
  not match any saved rehearsal. This is the only exception this contract raises; any other
  failure surfacing from a dependency (member/fee/GL repository) is expected to already be
  wrapped in a project custom exception at that dependency's own boundary, per constitution §5 —
  `AttendanceRollService` does not need to re-wrap failures it does not itself cause.

## New contract: `IAttendanceRollPdfRenderer` (StageFright.Reports → StageFright.UI)

```csharp
public interface IAttendanceRollPdfRenderer
{
    byte[] Render(AttendanceRollData data, string organizationName = "");
}
```

**Preconditions**: `data` is non-null (an empty `Members` list is a valid, accepted input — see
Postconditions). `organizationName` may be empty (matches `IPdfReportRenderer.Render`'s existing
optional-organization-name convention).

**Postconditions**:
- Returns a non-empty PDF byte array for any valid input, including a zero-member roll (the
  renderer itself does not enforce FR-013's empty-state rule — see "Division of responsibility"
  below).
- The rendered document lays out members in two columns per page (FR-009), overflowing to
  additional physical pages for larger rosters, with minimal-width checkbox columns (FR-010),
  wrapping column headings (FR-011), and each member's surname shown in capitals alongside their
  first name (FR-003).
- Pure function of its inputs — no I/O, no repository/DbContext access, no mutation of `data`.

**Failure modes**: None expected under normal QuestPDF operation for valid, well-formed
`AttendanceRollData`; any unexpected QuestPDF exception is caught and handled by the UI caller
exactly as `ReportViewer.razor.cs`'s `PrintReport()` already catches and reports
`PdfRenderer.Render(...)` failures today — this contract does not introduce a new exception type.

## Division of responsibility: who enforces FR-013 (empty-state message)?

Neither new contract enforces the "no active members → show an empty-state message instead of a
blank printable roll" rule (FR-013) internally:
- `IAttendanceRollService.GenerateAsync` returns a normal, valid `AttendanceRollData` with an empty
  `Members` list — it does not throw for this case (research.md Decision 6).
- `IAttendanceRollPdfRenderer.Render` will happily render a header-only PDF for an empty
  `Members` list if called — it has no opinion on whether that's an appropriate thing to do.
- The UI caller (`RehearsalList.razor.cs`'s `PrintRoll` handler) is the single place FR-013 is
  enforced: it inspects `rollData.Members.Count` after calling `GenerateAsync` and, if zero, shows
  an inline alert instead of calling `Render`/writing a temp file/launching a viewer. This mirrors
  `AttendanceGrid.razor.cs`'s existing "No active members found" precedent exactly.

## Out of scope for this contract

- `IReportProvider` (`StageFright.Reports/Registry/IReportProvider.cs`) — unchanged; this feature
  deliberately does not implement or register against this contract (research.md Decision 1).
- `IMemberService`, `IMemberBalanceService`, `IRehearsalRepository`, `IFeeRepository` — no
  signature changes; `AttendanceRollService` only consumes their existing, published methods
  (`GetByStatusAsync`, `GetOutstandingFeesAsync`, `GetByIdAsync`, `AnnualFeeExistsAsync`).
- `IPdfReportRenderer`/`PdfReportRenderer` — unchanged; `IAttendanceRollPdfRenderer` is a new,
  separate sibling contract, not a modification of the generic reports renderer.
