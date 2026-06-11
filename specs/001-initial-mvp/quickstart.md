# Quickstart & Validation Guide: StageFright Community — Initial MVP

**Branch**: `001-initial-mvp` | **Plan**: [plan.md](./plan.md) | **Data model**: [data-model.md](./data-model.md) | **Contracts**: [contracts/](./contracts/)

This guide describes how to build, run, test, and validate the MVP end-to-end. Implementation details live in tasks.md (Phase 2); this document is validation-only.

## Prerequisites

- .NET 10.0 SDK with the MAUI workload: `dotnet workload install maui`
- Windows 10.0.19041.0+ (development on Windows; macOS validation requires a Mac with Mac Catalyst workload)
- IDE: VS Code (with C# Dev Kit) or Visual Studio 2026

## Build & run

```powershell
# From repository root
dotnet restore StageFrightCommunity.sln
dotnet build StageFrightCommunity.sln -c Debug

# Run the desktop app (Windows)
dotnet build src/StageFright.App -t:Run -f net10.0-windows10.0.19041.0
```

First launch auto-creates the app-data directory, `stagefright.db` (migrated), and the `Plugins/` directory, then shows the setup wizard.

## Run tests

```powershell
dotnet test StageFrightCommunity.sln                                  # Full suite (merge gate, NFR-005)
dotnet test tests/StageFright.Core.Tests                              # Unit (< 5 s budget)
dotnet test tests/StageFright.Data.Tests                              # DAL integration (< 30 s budget)
dotnet test tests/StageFright.UI.Tests                                # bUnit UI (< 60 s budget)
dotnet test tests/StageFright.Integration.Tests                       # Cross-layer + acceptance journeys
```

All tests must pass before merge; acceptance suite runs on every PR (NFR-005).

## End-to-end validation scenarios

Each scenario maps to a user story (spec §3) and has a corresponding automated acceptance test; manual walkthrough steps below double as smoke tests.

### V1 — First-run setup (US1, SC-001)

1. Delete `stagefright.db` from app data; launch app.
2. Setup wizard appears; enter organization name, annual fee, attendance fee, renewal month (all mandatory); Save.
3. **Expect**: dashboard displays with empty tiles; Settings shows entered values; database contains **zero Fee records**; system categories Cash (GL#0100), MemberReceivable (GL#0101), BadDebtExpense (GL#9900) exist.

### V2 — Member registration & committee (US2)

1. Members → Add Member with name/address/join date only → saves; appears in Active list with no age shown.
2. Add a member with DOB → age displays per the FR-002a algorithm; invalid email/phone/future DOB → specific validation errors, no save.
3. Edit a member: check Committee Member, position required; save → Committee History shows current year bolded with "Current" badge (semantic HTML + ARIA).
4. Inactivate a member → hidden from Active list, `InactivateDate` set, audit entry recorded, no soft-delete fields touched.

### V3 — Rehearsals & attendance fees (US3)

1. Schedule a rehearsal (date/time/notes).
2. Record attendance via the batch grid (Member | Attended ☐ | Paid ☐) → Save.
3. **Expect**: attendance + fee records created atomically; fees default PAID (GL pairs: Debit MemberReceivable/Credit Income + Debit Cash/Credit MemberReceivable); "Mark as unpaid" members have receivable-only GL; `StoredAttendanceRate` frozen on the rehearsal; dashboard Rehearsals tile shows the rate; attendance is immutable (no edit/clear UI exists).

### V4 — Annual fee application (US4)

1. With active + inactive members and renewal month reached, Finance → Apply Annual Fees.
2. **Expect**: confirmation dialog with eligible count (active only, no existing unpaid current-year annual fee); on confirm, fees + GL pairs created; Finance tile outstanding balance updates with muted green/red/gray color per sign.

### V5 — Payments & FIFO (US6)

1. Member owes multiple fees across years; record one payment.
2. **Expect**: payment allocated oldest-first (FeeDate, CreatedAt, Id ASC); GL pairs per allocation; partial/overpayment handled per FR-016; balance = Σdebits − Σcredits; Notes is the only editable Payment field (others rejected with the immutability message); Notes edit audited.

### V6 — Accounting reports (US6a, SC-012/013)

1. Reports menu → Finance → each of: Income Statement, Trial Balance, Account Register, Member Account Summary.
2. **Expect**: default date range = current calendar year; "Generating report..." modal; Trial Balance totals equal within 0.01 (forced imbalance in a test fixture must fail generation with the exact FR-034 message); Account Register running balance correct; Member Account Summary ages by DueDate (current/30/60/90+) and includes archived members.
3. Print → PDF via OS dialog with headers/subtotals/grand totals; Export to CSV → opens cleanly in a spreadsheet with proper escaping.

### V7 — Categories (US7)

1. Settings → Categories: create income + expense categories → GL accounts auto-assigned sequentially (10xx/20xx by creation order).
2. Attempt to archive a category referenced by a transaction → blocked with explanation; archive an unreferenced one → appears in archive view with restore option; reorder works.

### V8 — Dashboard & plugin tile (US8, SC-007)

1. Dashboard shows 4 core tiles loading in parallel; a deliberately slow/failing test tile (fixture) shows "Unable to load" without blocking others.
2. Copy `StageFright.TestPlugin.dll` into `Plugins/`; relaunch → plugin tile renders in the Extensions section; remove a dependency to force load failure → startup continues, structured error logged.

### V9 — Backup & restore (US9, SC-008)

1. Settings → Backup → `.sfbak` file created (protobuf) with schemaVersion + all 10 entity types.
2. Restore: pre-import checkpoint auto-created, confirmation required; valid file → all data restored (PK upsert, atomic); a fixture file missing Categories → rejected with "Import file incomplete: missing Categories. Restore from complete backup."

### V10 — Themes (US10, SC-010)

1. Toggle theme → all surfaces switch (Bootstrap `data-bs-theme`); restart app → preference restored from Settings; automated contrast tests confirm WCAG AA in both themes.

### V11 — Reports menu & shared viewer (US11, SC-019)

1. Reports root menu lists Members section (Member List, Committee Report) then Finance section (4 reports); test plugin adds its own section alphabetically after.
2. Member List status filter defaults to Active and persists across print/export within a session; Committee Report filter likewise (Active Only default).
3. A provider that throws (fixture) is skipped from the menu / shows a friendly error in the viewer; other reports unaffected.

### V12 — Reactivation forgiveness (FR-024)

1. Reactivate an inactive member with prior-year + current-year unpaid fees.
2. **Expect**: dialog shows fees by year — prior years pre-checked, current year unchecked; confirm → GL write-off pairs to BadDebtExpense (GL#9900) for selected fees only; Fee records untouched; audit entries note default vs override; balance reflects only non-forgiven fees.

### V13 — Committee annual reset & AGM banner (FR-031)

1. Record an AGM event (event type "Annual General Meeting" — no fees created); ensure `LastCommitteeResetYear` < current year and AGM > 7 days ago → Settings page shows the reminder banner.
2. Click "Reset Committee for New Year" → confirmation → current-year assignments cleared (soft-deleted), history preserved, `LastCommitteeResetYear` updated, banner disappears, audit entry written.

## Observability checks

- `logs/` in app data contains structured Serilog output for: startup/shutdown, setup completion, batch fee application (counts), tile load timings, plugin discovery results, import/export entity counts, audit purge result.
- Startup purge of audit entries older than 12 months runs once; a simulated purge failure logs an error and startup continues.

## Definition of done (release gate, spec §9)

All user stories implemented; all acceptance scenarios pass; all SC-001…SC-019 verified; full test suite green in CI; constitution gates (one-class-per-file, custom exceptions, soft-delete rules, no custom JS) verified in review.
