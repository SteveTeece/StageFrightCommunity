# Tasks: Report Action Buttons — Separated Pill Style

- [X] **T001** Add a `.report-actions` CSS rule block modeled on the existing `.nav-tabs, .nav-pills` glass tab-bar rule (container: padding + gap + rounded background; buttons: borderless by default, hover treatment, no `border-radius:999px`-collapsed segmented look) + `src/StageFright.App/wwwroot/app.css`
- [X] **T002** Swap the report actions container's styling class in the Report Viewer from `btn-group` to `report-actions` (keep `role="group"` / `aria-label="Report actions"` for accessibility; no change to the `PrintReport` / `ExportCsv` / `Regenerate` click bindings) + `src/StageFright.UI/Shared/ReportViewer.razor`
- [X] **T003** Run the existing Report Viewer test suite to confirm Print / Export / Refresh click behavior is unchanged (SC-004) + `tests/StageFright.UI.Tests/Shared/ReportViewerTests.cs`
- [X] **T004** Launch the app and visually verify, in light theme, that the report action buttons are spaced apart, unbordered by default, and match the Finance screen's tab selector (SC-001, SC-002, SC-003) + `src/StageFright.UI/Shared/ReportViewer.razor`
- [X] **T005** Repeat the visual verification in dark theme (FR-006) + `src/StageFright.UI/Shared/ReportViewer.razor`
- [X] **T006** Run a full `dotnet build` and `dotnet test` and report the results per the project's build/test verification rule + `StageFrightCommunity.slnx`
