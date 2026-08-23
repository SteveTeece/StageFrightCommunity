# Tasks: Theme Toggle Placement

**Input**: [spec.md](./spec.md) (see [Approach](./spec.md#approach) for the file-level plan)

- [ ] **T001** Move the `.btn-theme-toggle` block (label + `RadzenSwitch`) out of `<header class="shell-topbar">` into the bottom of `<nav class="shell-sidebar">`, after `.sidebar-list`, and delete the now-empty `<header class="shell-topbar">` element + [src/StageFright.UI/Layout/ShellLayout.razor](../../src/StageFright.UI/Layout/ShellLayout.razor)
- [ ] **T002** Remove the now-unused `.shell-topbar` CSS rule and adjust `.shell-content`'s top padding so page content reclaims the freed vertical space + [src/StageFright.App/wwwroot/app.css](../../src/StageFright.App/wwwroot/app.css)
- [ ] **T003** Add sidebar-pinned CSS for the relocated toggle (e.g. `margin-top: auto`, `flex: none` inside the sidebar's flex column) so it stays fixed at the bottom independent of the scrollable `.sidebar-list` + [src/StageFright.App/wwwroot/app.css](../../src/StageFright.App/wwwroot/app.css)
- [ ] **T004** Shrink `.btn-theme-toggle`'s padding/gap/font-size and the `RadzenSwitch` control so the rendered footprint is at least 50% smaller than today, keeping the click target usable + [src/StageFright.App/wwwroot/app.css](../../src/StageFright.App/wwwroot/app.css)
- [ ] **T005** Update the three existing theme-toggle tests to locate `.btn-theme-toggle` inside `.shell-sidebar` instead of the removed top bar; keep the Light/Dark text, click-to-switch, and hidden-on-`/setup` assertions passing + [tests/StageFright.UI.Tests/Layout/ShellLayoutTests.cs](../../tests/StageFright.UI.Tests/Layout/ShellLayoutTests.cs)
- [ ] **T006** Manually verify in the running MAUI app (CDP/DPI-aware screenshot workflow) that the toggle renders pinned at the sidebar bottom, visibly smaller, and still toggles theme correctly with both a short and a long navigation menu + src/StageFright.App
- [ ] **T007** Run `dotnet build` and the full test suite; confirm no regressions + repo root
