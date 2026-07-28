# Research: Printable Member Attendance Roll

**Feature**: `012-printable-attendance-roll` | **Phase**: 0 (Outline & Research)

There were no `NEEDS CLARIFICATION` markers left in Technical Context (the one open question —
whether the roll requires an already-scheduled rehearsal — was resolved in spec.md's
Clarifications session). This phase focuses on *how* to implement each functional requirement
given the constraints the existing codebase already imposes.

> **Correction — 2026-07-28**: The original implementation's "Annual Fee Paid" column, and its
> "always-live, always-blank" member/checkbox model, did not match the actual requirement (see
> spec.md's "Correction" clarification session). Decisions 4 and 5 below are updated accordingly;
> Decisions 8–10 are new.

## Codebase Inventory (baseline)

| Area | File | Relevant existing behavior |
|---|---|---|
| Report contract | `src/StageFright.Reports/Registry/IReportProvider.cs` | `GenerateAsync(ReportFilterValues) -> ReportData` — global, filter-driven, no entity-ID parameter |
| Report data model | `src/StageFright.Reports/Models/ReportData.cs`, `ReportSection.cs`, `ReportRow.cs` | Flat: `Columns` (shared across all sections) + `Sections` (each a list of `ReportRow`, each row a list of plain **string** cells) + optional subtotal/grand total. No concept of a second column or a non-text cell. |
| PDF renderer | `src/StageFright.Reports/Rendering/PdfReportRenderer.cs` | One QuestPDF `Table` per report, auto-paginating across physical pages natively (confirmed by its single `container.Page(...)` + `Table` + `page.Footer()...CurrentPageNumber()/TotalPages()`); every cell rendered via `cell.Text(value)` — no shape/glyph drawing anywhere in this codebase today. |
| Report trigger | `src/StageFright.UI/Pages/Reports/ReportsPage.razor`, `src/StageFright.UI/Shared/ReportViewer.razor(.cs)` | User picks a report from a menu → `/reports/{reportId}` → `ReportViewer` renders filters + a "Print / PDF" button. `PrintReport()` (`ReportViewer.razor.cs:147-168`) is the exact "render → write temp file → `Process.Start(UseShellExecute:true)`" pattern this feature reuses. No mechanism exists to pre-scope a report to one entity ID (e.g. a rehearsal) via this pipeline. |
| Rehearsal list | `src/StageFright.UI/Pages/Rehearsals/RehearsalList.razor(.cs)` | Per-row Actions column already shows "Record Attendance" / "Recorded"; this is the only per-rehearsal action surface in the app (no separate rehearsal detail page exists). |
| Point-in-time active membership | `src/StageFright.Data/Repositories/MemberRepository.cs` (`GetActiveAsOfAsync`), consumed by `src/StageFright.Core/Modules/Rehearsals/RehearsalService.cs` (`FreezeAttendanceRateAsync`) and `EventService.cs`'s equivalent participation-rate freeze | `IMemberRepository.GetActiveAsOfAsync(date)` — `(Status=Active AND ActivateDate <= date) OR (Status=Inactive AND ActivateDate <= date AND InactivateDate > date)`, excluding soft-deleted. Corrected 2026-07-28 (see Decision 8) to also match members who have since gone inactive but were active as of `date` — the original query only matched members whose *current* status was Active, which this feature's real-implementation testing surfaced as a gap versus its own doc comment. |
| Recorded attendance lookup | `src/StageFright.Core/Contracts/IAttendanceRepository.cs:16` (`GetByRehearsalAsync`) | Returns all `AttendanceRecord`s for a rehearsal; used to populate the "Present" checkbox's real state per the corrected FR-005. |
| Attendance-fee-paid signal | `src/StageFright.Core/Modules/Finance/MemberBalanceService.cs:28-55` (`GetOutstandingFeesAsync`) + `src/StageFright.Core/Contracts/IFeeRepository.cs:18` (`GetByMemberAsync`) | `GetOutstandingFeesAsync(memberId)` returns only fees with `RemainingAmount > 0`; cross-referencing against the member's `Fee` with `FeeType.Attendance` and matching `RehearsalId` (found via `GetByMemberAsync`) gives real per-rehearsal fee-paid state per the corrected FR-006. |
| Fee classification | `src/StageFright.Core/Enums/FeeType.cs` | `Annual` / `Attendance` / `Other` — `Attendance` is the type to filter on (previously `Annual`, removed with the "Annual Fee Paid" column). |
| Attendance fee amount | `src/StageFright.Core/Entities/Settings.cs:33` (`AttendanceFee`), also consumed by `src/StageFright.Core/Modules/Rehearsals/AttendanceService.cs:89-90` | Per-rehearsal attendance fee amount configured in Settings; sourced via `ISettingsRepository` for the fee column's header text per the corrected FR-006. |
| Rehearsal lookup by id | `src/StageFright.Core/Modules/Rehearsals/RehearsalService.cs:71-91` (`FreezeAttendanceRateAsync`) | `_rehearsalRepo.GetByIdAsync(rehearsalId, ct) ?? throw new EntityNotFoundException("Rehearsal", rehearsalId, nameof(FreezeAttendanceRateAsync))` — `IRehearsalRepository` (via `ISoftDeletableRepository<Rehearsal>`) already exposes `GetByIdAsync`, even though `IRehearsalService` doesn't wrap it; this is the exact not-found precedent to follow. |
| DI registration | `src/StageFright.App/MauiProgram.cs:169,180,230` | `IRehearsalService`, `IMemberBalanceService`, `IPdfReportRenderer` all registered `AddScoped` in `RegisterCoreServices` — new services/renderers follow the same pattern. |

## Decisions

### 1. Do NOT model this as an `IReportProvider` — build a dedicated, module-owned print path instead

**Decision**: The roll is generated by a new `IAttendanceRollService` (Core, Rehearsals module) +
`IAttendanceRollPdfRenderer` (Reports project), triggered directly from `RehearsalList.razor`'s
existing Actions column — not registered in `ReportProviderRegistry`, not reachable from the
generic `/reports` menu.

**Rationale**: `ReportData`/`PdfReportRenderer` can only express one flat, single-column-flow
table of string cells (confirmed above); this feature's mandatory two-column layout (FR-009) and
checkbox columns (FR-005–FR-007) cannot be produced by that pipeline without changing it for
every existing report. `IReportProvider.GenerateAsync` also takes only `ReportFilterValues` — a
user-selected, generically-typed filter bag — with no way to pre-scope generation to one specific
`rehearsalId` the way FR-001 requires ("surfaced from an existing rehearsal ... not from the
create form"). Extending the shared pipeline to support both concerns (multi-column layout +
entity-scoped generation) would touch code depended on by all nine existing report providers for
the benefit of exactly one new, structurally different print artifact — the constitution's
"prefer clarity, avoid cleverness" (§3.1) and "no coupling across boundaries without explicit
justification" (§3.3) both favor a small, isolated addition over reshaping shared infrastructure.
FR-012's requirement that the roll be "consistent with how other reports ... are generated and
printed" is satisfied at the *technology and UX* level (same QuestPDF dependency, same
render-to-temp-file-and-launch pattern) without requiring the same *data model*.

**Alternatives considered**:
- **Add a new `ReportFilterType.Entity` (dynamic, DB-backed option list) so a "rehearsal" filter
  could scope a report, plus extend `ReportData`/`PdfReportRenderer` with an optional multi-column
  render mode** — rejected: doubles the scope of this feature into a generic-pipeline redesign,
  risks regressing the six existing MVP reports, and still wouldn't cleanly express
  minimal-width checkbox columns without adding a typed-cell concept to `ReportRow` (currently
  `IReadOnlyList<string>`), a much larger, unjustified change for one feature.
- **Register the new renderer's output as an `IReportProvider` anyway**, just for menu discovery,
  registering it in `ReportProviderRegistry` so it appears under `/reports` — rejected: FR-001
  explicitly scopes the print action to "an existing rehearsal (e.g., its list row or
  detail/attendance page)", not a general reports menu; the roll has no meaningful "pick a
  rehearsal" filter UX to offer there beyond what the rehearsal list row already provides for
  free.

### 2. Print action location: `RehearsalList.razor`'s Actions column, not `AttendanceGrid.razor`

**Decision**: Add a "Print Roll" button to the existing Actions column template in
`RehearsalList.razor` (`RehearsalList.razor:69-84`), available for every non-deleted rehearsal
regardless of whether attendance has already been recorded.

**Rationale**: `RehearsalList` is the only per-rehearsal action surface today (there is no
separate rehearsal detail page — confirmed in inventory); it already lists every scheduled
rehearsal with an Actions column, requires no navigation, and naturally supports FR-001's
"already-scheduled (saved) rehearsal" precondition (a row only exists once a rehearsal is saved).
Placing it here also satisfies the spec's Assumption that the roll "can be generated, and
re-generated, for a rehearsal at any point after it has been scheduled" — the button is available
before and after attendance is recorded, without needing two separate code paths in
`AttendanceGrid.razor`'s pre-recording vs. already-recorded branches.

**Alternatives considered**:
- **`AttendanceGrid.razor`** (the attendance-taking page) — rejected as the sole location: it
  requires an extra navigation step for what is meant to be a quick, incidental action from the
  list the attendance-taker already sees, and would need the button duplicated across its two
  mutually-exclusive branches (pre-recording form vs. already-recorded read-only view) for no
  added benefit over placing it once in `RehearsalList`.
- **A new dedicated rehearsal detail page** — rejected: no such page exists today for any other
  rehearsal action (edit, delete); introducing one solely to host a print button would be a
  disproportionate structural addition for a single-click action.

### 3. Two-column, multi-page layout: pre-chunk in C#, not QuestPDF auto-column-flow

**Decision**: `AttendanceRollPdfRenderer` computes a fixed `RowsPerColumn` constant (a
conservative estimate of how many roll rows fit in one column of one A4 page at the chosen font
size/padding, matching the header/footer/margins already used by `PdfReportRenderer`), chunks the
sorted member list into groups of `2 × RowsPerColumn`, and emits one explicit QuestPDF
`container.Page(...)` per chunk — each page containing a `Row` with two side-by-side `Table`
columns (first `RowsPerColumn` members left, remainder right). The document-wide footer
(`CurrentPageNumber()`/`TotalPages()`) works unchanged across multiple `Page()` blocks, exactly as
it already does for the single-`Table` reports.

**Rationale**: QuestPDF has no built-in "CSS `column-count`"-style primitive that auto-balances
two independently-flowing columns and synchronizes their page breaks — `PdfReportRenderer`'s
existing multi-page support only auto-paginates a *single* flowing `Table`, not two side-by-side
tables that need to break to a new page together once both are full. Pre-chunking the member list
in plain C# (a `List<T>.Chunk(2 * RowsPerColumn)` call) sidesteps relying on undocumented or
fragile layout-engine behavior, is trivially unit-testable (assert the exact left/right split for
boundary counts), and matches "simple over clever" (§3.1) far better than attempting to coax
QuestPDF into dynamic column-balancing it wasn't designed for. FR-009's requirement — fill column
one before overflowing into column two, then continue onto additional pages — is exactly what
this chunking strategy produces by construction.

**Alternatives considered**:
- **Rely on QuestPDF's automatic pagination for two independent `Table`s inside a `Row`** —
  rejected after checking `PdfReportRenderer`: its multi-page behavior is demonstrated only for a
  single `Table`; nothing in this codebase shows two tables coordinating a shared page-break point,
  and QuestPDF's documented column-layout support does not include CSS-style newspaper columns.
  Attempting it would be unverified, high-risk behavior for a "simple over clever" codebase.
- **Dynamically measure available height at render time (via QuestPDF's size-measurement APIs) to
  compute rows-per-column instead of a fixed constant** — rejected as unnecessary complexity: this
  report's font size, margins, and row padding are fixed constants controlled entirely by this
  renderer (not user-configurable), so a fixed, code-reviewable constant is simpler, deterministic,
  and just as correct as a runtime measurement for this fixed layout.

### 4. Checkbox rendering: bordered box, unchecked; bordered box + checkmark glyph, checked

**Decision**: Render each checkbox cell as a small fixed-size `Container` with a border
(`.Border(1).BorderColor(...).Width(Xpt).Height(Xpt)`), left empty for unchecked ("Present" and
the fee column, both before attendance is recorded and for any member not checked per the
corrected FR-005/FR-006 rules). A checked cell renders the same bordered box with a centered bold
"✓" (U+2713) checkmark glyph inside it — **not** a solid filled/black background (superseded; see
below). Since the correction, "Present" and the fee checkbox are no longer always-blank: each is
computed per member from real attendance/fee data (see the corrected Decision 5) and rendered
checked or unchecked accordingly, using this same box-and-glyph presentation either way.

**Rationale**: QuestPDF Community edition bundles a limited default font set, so Unicode glyphs
were initially avoided over a theoretical tofu/missing-glyph risk (see original rationale below).
In practice the bundled default font renders "✓" cleanly on this project's target platforms — this
was confirmed by direct visual inspection of generated PDFs before landing the change. A solid
filled/black box (the first implementation) was replaced after user feedback that it looked
visually unpolished for a print-and-tick document; a bordered box with a checkmark reads clearly
as "this one's ticked" while an empty bordered box reads as "not yet ticked," matching the
handwritten-tick metaphor the roll is designed around. This is now this project's standing
preference for "checked" states across any future printable report using the same checkbox
pattern — not just this one roll.

**Superseded original rationale** (kept for context): relying on Unicode box-drawing/checkbox
glyphs (☐ U+2610 / ☑ U+2611) risked silent tofu/missing-glyph rendering on some platforms/fonts,
which nothing in this codebase's existing `PdfReportRenderer` usage had ever had to handle (every
existing cell is plain Latin text). A drawn border box is glyph-independent and guaranteed to
render identically on Windows and macOS (the app's two supported desktop targets, per Technical
Context). This concern turned out not to materialize for the single "✓" glyph in QuestPDF's
default font, so the solid-fill fallback was dropped in favor of the more legible checkmark.

**Alternatives considered**:
- **Unicode checkbox glyphs (☐/☑) as plain `Text`** — rejected due to font-embedding risk
  described above; no precedent anywhere in this codebase for symbol-glyph rendering via QuestPDF.
- **Plain "Yes"/"No" or blank text cells** — rejected: doesn't satisfy the spec's explicit
  "checkbox" requirement (FR-005–FR-007) or the printed-sheet, hand-markable intent described in
  the feature request.

### 5. "Present" and fee-paid computation: real per-rehearsal data, no new balance API

**Decision (corrected 2026-07-28)**: For each member on the roll, `AttendanceRollService` computes
two independent, real-state fields instead of the old always-false "Attended"/"Rehearsal Fee Paid"
and the old current-year "Annual Fee Paid":

- `Attended`: look up the member's `AttendanceRecord` for this rehearsal (via
  `IAttendanceRepository.GetByRehearsalAsync(rehearsalId)`, indexed by `MemberId`) and set `true`
  only if a record exists with `Attended == true`; otherwise `false` (covers both "not yet
  recorded" and "recorded absent").
- `RehearsalFeePaid`: find the member's `Fee` with `FeeType == FeeType.Attendance` and matching
  `RehearsalId` (via the existing `IFeeRepository.GetByMemberAsync(memberId)`, filtered
  client-side — no new repository method needed), then set `true` only if that fee exists **and**
  it does not appear in `IMemberBalanceService.GetOutstandingFeesAsync(memberId)` (matched by
  `FeeId`); otherwise `false`.

**Rationale**: This is the same "existence check + outstanding-balance exclusion" shape as the
original Annual Fee Paid computation (kept below for context), just re-keyed to a specific
rehearsal's `Attendance`-type fee instead of the current calendar year's `Annual`-type fee, and
paired with a genuinely independent `Attended` field sourced from `AttendanceRecord` rather than
always `false`. This directly matches every acceptance scenario in the corrected User Story 2:
attendance not yet recorded → both blank; attended with fee paid → both checked; attended but fee
marked unpaid (via the attendance grid's "mark as unpaid" option) → `Attended` checked,
`RehearsalFeePaid` unchecked; not attended/no record → both unchecked. Reusing
`GetOutstandingFeesAsync` (rather than a new balance query) keeps the same "GL balance is
authoritative, fees carry no per-record paid flag" precedent this codebase already relies on for
outstanding-balance checks elsewhere.

**Superseded original decision** (kept for context — no longer implemented): the original
"Annual Fee Paid" computation was `AnnualFeePaid = AnnualFeeExistsAsync(memberId, currentYear) &&
!GetOutstandingFeesAsync(memberId).Any(f => f.FeeType == Annual && f.FeeDate.Year ==
currentYear)`, using `IFeeRepository.AnnualFeeExistsAsync` for the existence check. This entire
column and field were removed per the correction — Annual Fee Paid is out of scope for this spec.

**Alternatives considered**:
- **Add a new `IMemberBalanceService` method (e.g. `IsRehearsalFeePaidAsync`)** — considered but
  not required: the existing `GetOutstandingFeesAsync` plus a client-side filter over
  `IFeeRepository.GetByMemberAsync` fully covers the corrected behavior without widening the
  `IMemberBalanceService` public contract for a single caller; keeping the computation local to
  `AttendanceRollService` avoids adding Rehearsals-specific concerns to the Finance module's public
  interface (§3.3 separation of concerns — Finance shouldn't need to know about rehearsal rolls).
- **Mirror `RehearsalFeePaid` directly off `Attended`** (checked whenever the member attended,
  regardless of actual payment) — rejected: loses the "mark as unpaid" signal the system already
  tracks per member per rehearsal, which the corrected spec explicitly calls out as a case the fee
  checkbox must distinguish (Acceptance Scenario 3 of the corrected User Story 2).

### 6. Empty-state handling (FR-013): checked in the UI layer, not thrown as an exception

**Decision**: `AttendanceRollService.GenerateAsync` returns an `AttendanceRollData` with an empty
`Members` list when there are no active members (it does not throw). `RehearsalList.razor.cs`'s
`PrintRoll` handler checks `rollData.Members.Count == 0` after calling the service and, if empty,
shows an inline alert message instead of rendering/opening a PDF.

**Rationale**: This exactly matches the existing precedent in `AttendanceGrid.razor.cs`/`.razor`
(`_members.Count == 0` → "No active members found. Add members before recording attendance."
alert, not an exception) — an empty active-member list is a normal, expected UI state to display
inline, not an exceptional error condition warranting a custom exception type. Keeping the check in
the UI layer (rather than the service throwing, say, a new `EmptyRollException`) also avoids
introducing a new exception type for a case that isn't a boundary failure (no framework exception,
no data-integrity problem) — just an empty result set, which `IReadOnlyList<T>` already represents
naturally.

**Alternatives considered**:
- **Service throws a dedicated exception (e.g. `NoActiveMembersException`) on empty roster** —
  rejected: inconsistent with the `AttendanceGrid` precedent for the identical "no active members"
  condition, and exceptions in this codebase are reserved for boundary-crossing failures (§5), not
  for a legitimately empty, valid query result.

### 7. Duplicate "render → temp file → launch" boilerplate: accepted, not extracted

**Decision**: `RehearsalList.razor.cs`'s `PrintRoll` handler repeats the same ~6-line
"write PDF bytes to a temp file, then `Process.Start` with `UseShellExecute = true`" sequence
already present in `ReportViewer.razor.cs`'s `PrintReport()`/`ExportCsv()`, rather than extracting
a shared helper.

**Rationale**: Per CLAUDE.md's explicit guidance ("Three similar lines is better than a premature
abstraction... a bug fix doesn't need surrounding cleanup"), two call sites sharing ~6 lines of
straightforward, unlikely-to-diverge code does not yet justify a new shared abstraction (e.g. an
`IPdfLauncher` service) — especially since the two call sites differ slightly in error-handling
context (`ReportViewer` sets `_error` bound to its own report-viewer UI; `RehearsalList` would set
its own page-level error/alert state).

**Alternatives considered**:
- **Extract a shared `IPdfLauncher`/`TempFileLauncher` utility now** — rejected as premature for
  two call sites; revisit only if a third print-and-launch call site appears.

### 8. Point-in-time membership: reuse `IMemberRepository.GetActiveAsOfAsync`, inject the repository directly

**Decision**: `AttendanceRollService` replaces its `IMemberService` dependency with
`IMemberRepository` and calls `GetActiveAsOfAsync(rehearsal.Date, ct)` instead of
`GetByStatusAsync(MemberStatus.Active, ct)`.

**Rationale**: `GetActiveAsOfAsync` already exists and already targets exactly the "who was active
as of this rehearsal's date" semantics the corrected FR-002 requires — it's the same method
`RehearsalService.FreezeAttendanceRateAsync` uses to compute the attendance-rate denominator for a
rehearsal, so the roll's membership and the frozen attendance rate now agree by construction, for
free. `AttendanceRollService` already injects other repositories directly (`IFeeRepository`,
`IRehearsalRepository`), so injecting `IMemberRepository` instead of going through `IMemberService`
(which has no equivalent point-in-time method) is consistent with that existing pattern, not a new
one.

**Correction note**: implementing and testing this decision surfaced a real gap in the existing
`GetActiveAsOfAsync` query — it only matched members whose *current* status is Active, so a member
who was active as of the rehearsal's date but has since gone inactive was silently excluded,
contradicting both the method's own doc comment and this feature's approved point-in-time design.
The query was corrected to `(Status=Active AND ActivateDate <= date) OR (Status=Inactive AND
ActivateDate <= date AND InactivateDate > date)`, which also fixes the same latent gap for
`FreezeAttendanceRateAsync`'s denominator. All four pre-existing `GetActiveAsOfAsync` integration
tests in `MemberRepositoryIntegrationTests.cs` continued to pass unchanged under the corrected
query; one new test (`GetActiveAsOfAsync_ReturnsMember_WhenInactivatedAfterDate`) was added to
cover the newly-supported branch.

**Alternatives considered**:
- **Add a new point-in-time method to `IMemberService`** — rejected: `IMemberRepository` already
  exposes the exact method needed; adding a pass-through on the service interface would be
  duplication for no behavioral gain, and every other data-access call in this service already goes
  through a repository directly.
- **Leave `GetActiveAsOfAsync` as-is and narrow the roll's documented point-in-time behavior to
  match its actual (buggy) output** — rejected: this would mean documenting and shipping behavior
  the user explicitly did not approve (a currently-inactive member who was present at a past
  rehearsal would silently vanish from a reprinted roll), for the sake of avoiding a small, safe,
  additive query fix with no conflicting existing test coverage.

### 9. Fee column heading: format as zero-decimal currency, computed once per render

**Decision**: `AttendanceRollData` gains a `decimal AttendanceFeeAmount` field, populated by
`AttendanceRollService` from `ISettingsRepository.GetAsync()` (a new dependency on this service).
`AttendanceRollPdfRenderer` formats it once per `Render()` call as
`AttendanceFeeAmount.ToString("C0")` (culture-default currency symbol, zero decimal places, e.g.
`"$5"`) and uses that string as the fee column's header text instead of "Rehearsal Fee Paid".

**Rationale**: `"C0"` directly produces the "$2" / "$5" zero-decimal, symbol-prefixed format called
for by the correction, using the same culture-default currency formatting `ToString("C")` already
uses elsewhere in the UI layer (e.g. `AttendanceGrid.razor`), just with the decimal count pinned to
zero. Computing it once in the renderer (rather than per-member) is correct because the amount is
roll-wide, not per-member — it's a Settings-level configuration value, not something that varies by
row.

**Alternatives considered**:
- **Extract a shared currency-formatting helper now** — rejected as premature: this is the first
  zero-decimal currency format needed anywhere in the codebase (the existing report-provider
  `FormatCurrency` helpers are two-decimal, `"F2"`, with no `$` symbol at all); a single `ToString("C0")`
  call inline does not yet justify a new shared utility for one call site.

### 10. `AnnualFeePaid` and its supporting fields/methods: removed, not deprecated

**Decision**: `AttendanceRollMember.AnnualFeePaid` and `AttendanceRollService`'s
`IsCurrentYearAnnualFeePaidAsync` private method are deleted outright, along with the
`IMemberBalanceService` dependency's use for that purpose (still retained, repurposed for the
corrected `RehearsalFeePaid` computation — see Decision 5) and the fourth PDF column that rendered
it.

**Rationale**: The corrected spec explicitly states Annual Fee Paid "is not part of this spec" —
this codebase's constitution favors removing code that's no longer required over leaving unused
fields/branches behind "just in case" (§3.1 "simple over clever", CLAUDE.md's "don't add
backwards-compatibility shims... if you are certain that something is unused, you can delete it
completely"). No other feature reads `AnnualFeePaid` or `IsCurrentYearAnnualFeePaidAsync`.

**Alternatives considered**:
- **Keep the field but stop rendering it** — rejected: leaves dead, untested code path with no
  caller, contradicting the "no half-finished implementations" guidance and this repo's exhaustive
  test-coverage rule (an unused field with no rendering path has nothing meaningful to test).

## Outstanding Risks (carried into tasks.md, not blocking)

- **`RowsPerColumn` constant tuning**: the exact number of rows that fit in one column at the
  chosen font size/padding must be verified visually against a real generated PDF during
  implementation (tasks.md must include a manual visual-check task), not just asserted
  programmatically, since QuestPDF's rendered row height depends on font metrics not fully
  reproducible from unit tests alone.
- **Checkbox box sizing vs. FR-010's "minimal width" requirement**: the checkbox column width must
  be visibly narrower than the name column in the rendered PDF — a manual visual check (alongside
  the automated non-empty-PDF assertions) is needed since QuestPDF column proportions
  (`RelativeColumn` ratios) are easy to get numerically "narrow enough" yet still look visually
  cramped or too wide in the actual PDF.
