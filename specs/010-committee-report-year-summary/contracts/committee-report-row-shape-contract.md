# Contract: Committee Report row/section shape

This documents the internal contract `CommitteeReportProvider.GenerateAsync` must honor when populating `ReportData` for the year-summary redesign. No public/external API is exposed by this application (desktop MAUI Blazor app, no HTTP surface), so this is the closest analog: the shared model contract between the provider and `ReportViewer`/`PdfReportRenderer`/`CsvReportExporter`, all three of which already implement the generic master-detail contract from `specs/005-member-account-summary-redesign/contracts/report-master-detail-contract.md`. That contract's rules (all-or-nothing `SummaryRow` per section, cell-count parity, exports unaffected by `SummaryColumns`/`SummaryRow`) apply unchanged here. This document adds the Committee-Report-specific row content rules.

## Shape produced by `GenerateAsync`

```csharp
new ReportData
{
    Title = "Committee Report",
    SubTitle = $"Filter: {memberFilter} — {DateTime.UtcNow:d MMMM yyyy}",
    Columns =
    [
        new ReportColumn { Header = "Year", Alignment = ReportColumnAlignment.Left },
        new ReportColumn { Header = "Position", Alignment = ReportColumnAlignment.Left },
        new ReportColumn { Header = "Member(s)", Alignment = ReportColumnAlignment.Left }
    ],
    SummaryColumns =
    [
        new ReportColumn { Header = "Year", Alignment = ReportColumnAlignment.Left },
        new ReportColumn { Header = "Positions Recorded", Alignment = ReportColumnAlignment.Right }
    ],
    Sections = yearSections // each section below MUST set SummaryRow (per the spec-005 contract)
};

// One ReportSection per year with at least one matching record, most-recent-year-first:
new ReportSection
{
    Heading = "2026",                                  // PDF-only section label; CSV never reads this
    SummaryRow = new ReportRow { Cells = ["2026", "5"] },  // [Year, total record count for the year]
    Rows =
    [
        new ReportRow { Cells = ["2026", "President", "Alice"] },
        new ReportRow { Cells = ["2026", "Secretary", "Vacant"] },
        new ReportRow { Cells = ["2026", "Treasurer", "Carol"] },
        new ReportRow { Cells = ["2026", "Welfare Officer", "Dave"] },
        new ReportRow { Cells = ["2026", "General Committee Members", "Eve, Frank"] }
    ],
    Subtotal = null
}
```

## Contract rules specific to this report

1. **Year appears in every detail row, not only in `Heading`.** `CsvReportExporter` never writes `ReportSection.Heading` (confirmed by reading `src/StageFright.Reports/Rendering/CsvReportExporter.cs`), so `Cells[0]` of every `ReportRow` MUST be the year string. This is what makes US3 Acceptance Scenario 2 ("each row identifies the year... in CSV") true without any renderer change.
2. **Position-line ordering per year**: President, Secretary, Treasurer (always emitted, `"Vacant"` in `Cells[2]` when unfilled) — then every other distinct non-blank position label present that year, ordered alphabetically (case-insensitive) by its displayed label — then, if any blank-position records exist that year, exactly one `"General Committee Members"` line last.
3. **Matching is case-insensitive and trimmed** (FR-007): `"president "`, `"President"`, and `"PRESIDENT"` all resolve to the same named-role line; any two other-position values differing only by case/whitespace resolve to the same distinct-position line, displayed using the first-encountered trimmed value for that normalized key.
4. **Multi-member lines list every member, alphabetically by name, comma-separated** (FR-006, FR-006a, FR-010) — e.g., `"President: Alice, Bob"` is represented as `Cells = ["2026", "President", "Alice, Bob"]`. No member is ever dropped when more than one record shares a position/label in the same year.
5. **`SummaryRow.Cells[1]` (Positions Recorded) is the raw count of committee membership records for that year under the active filter** — not the number of position *lines* shown. A year with the example above has `RecordCount = 5` (Alice, [vacant Secretary contributes 0], Carol, Dave, Eve, Frank = 5 actual records; a vacant named role contributes no record).
6. **Years with zero matching records under the active filter never appear** as a `ReportSection` (FR-009) — there is no "vacant year" placeholder.
7. **Exports are unaffected by `SummaryColumns`/`SummaryRow`**, per the spec-005 master-detail contract: `PdfReportRenderer` and `CsvReportExporter` read only `Columns`/`Sections[].Rows`/`Subtotal`/`GrandTotal`, and MUST NOT be changed to reference the new fields for this feature either.
8. **Ordering of `Sections` is most-recent-year-first** (FR-001); `ReportViewer` does not re-sort `Sections`, so `CommitteeReportProvider` itself must return them in that order.
