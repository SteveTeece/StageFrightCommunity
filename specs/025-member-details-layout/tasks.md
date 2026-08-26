# Tasks: Member Details View — Compact Two-Column Layout

- [ ] **T001** Reorganize the basic details `<dl>` into two `col-md-6` columns inside the existing `row g-2`: contact fields (Address, Phone, Email) in the left column, membership fields (Join Date, Date of Birth, Age, Status) in the right column — keep each field's existing conditional `@if` guard unchanged (FR-001–FR-003) + `src/StageFright.UI/Pages/Members/MemberDetail.razor`
- [ ] **T002** Reduce the Fee Payment History `RadzenDataGrid`'s `PageSize` from `15` to a smaller value (following the `PageSize="10"` precedent in `CommitteeSettingsTab`/`EventTypesTab`) so the grid fits below the reflowed details block without a page scrollbar (FR-005, FR-006) + `src/StageFright.UI/Pages/Members/MemberDetail.razor`
- [ ] **T003** Add one sentence to the "Data grid standards" section noting this grid as a second space-driven `PageSize` exception, alongside the existing `CommitteeSettingsTab`/`EventTypesTab` mention + `CLAUDE.md`
- [ ] **T004** Run the existing Member Details test suite to confirm field visibility rules and other behavior are unchanged (SC-004) + `tests/StageFright.UI.Tests/Pages/Members/MemberDetailTests.cs`
- [ ] **T005** Launch the app and visually verify, in light theme, that a member with a full set of basic details and several fee payments renders in two columns and fits the page without scrolling (SC-001, SC-002, SC-003) + `src/StageFright.UI/Pages/Members/MemberDetail.razor`
- [ ] **T006** Repeat the visual verification in dark theme (FR-008) + `src/StageFright.UI/Pages/Members/MemberDetail.razor`
- [ ] **T007** Run a full `dotnet build` and `dotnet test` and report the results per the project's build/test verification rule + `StageFrightCommunity.slnx`
