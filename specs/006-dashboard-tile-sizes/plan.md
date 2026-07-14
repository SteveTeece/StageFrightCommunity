# Implementation Plan: Dashboard Tile Sizes

**Branch**: `006-dashboard-tile-sizes` | **Date**: 2026-07-14 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/006-dashboard-tile-sizes/spec.md`

**Note**: This template is filled in by the `/speckit-plan` command. See `.specify/templates/plan-template.md` for the execution workflow.

## Summary

Dashboard tiles currently render at one uniform size in a Bootstrap `row-cols` grid. This feature
lets each tile provider declare one of four pre-set sizes (1x1 default, 1x2 double-width, 2x1
double-height, 2x2 double-both) so data-rich tiles can be given more room while simple tiles stay
compact. The Dashboard's Bootstrap `row-cols` layout is replaced with a CSS Grid (`grid-auto-flow:
dense`) inside the existing "Core Metrics" / "Extensions" sections, which packs mixed-size tiles
without gaps or overlap and collapses to a single column on narrow screens — all via CSS, with no
new persistence, no custom JavaScript, and full backward compatibility for existing and plugin tile
providers that don't declare a size.

## Technical Context

**Language/Version**: C# 14, .NET (MAUI Blazor Hybrid, target `net10.0-windows10.0.19041.0`)

**Primary Dependencies**: Blazor (Razor components + code-behind), Bootstrap (existing grid/utility
classes), Radzen.Blazor and BlazorBootstrap (already in use elsewhere; not required for this
feature) — no new NuGet packages needed

**Storage**: N/A — tile size is a provider-declared property (like existing `NavigateRoute`,
`ActionText`), not persisted data

**Testing**: xUnit (`StageFright.Core.Tests`), bUnit (`StageFright.UI.Tests`, extending the existing
`DashboardTests.cs` pattern)

**Target Platform**: Windows desktop and macOS desktop (MAUI Blazor Hybrid, per constitution §7.1)

**Project Type**: Desktop app (MAUI Blazor Hybrid) — single-project vertical-slice structure per
constitution §4.1; no new module, this extends the existing Dashboard module and the
`StageFright.Plugins.Contracts` extension point

**Performance Goals**: N/A — no new async/data-loading work; layout is a pure CSS change on already
-loaded tile data

**Constraints**: No custom JavaScript (constitution §7.3); CSS lives in the global stylesheet unless
genuinely component-scoped (§4.7.2); existing tile behaviours (click-through, action link,
loading/error states, Core Metrics/Extensions grouping) must be unaffected; the `IDashboardTileProvider`
contract must remain backward compatible for existing core and third-party plugin implementations
(§8.1, §12.2)

**Scale/Scope**: 7 existing core tile providers + arbitrary plugin-contributed tiles; 4 size variants
(1x1, 1x2, 2x1, 2x2)

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **§8.1 Plug-in isolation / §12.2 backward compatibility**: The new tile-size capability is added to
  `IDashboardTileProvider` as a **default interface member** (mirroring the existing
  `NavigateRoute`/`ActionText` pattern), so third-party plugins that don't recompile against the new
  member still compile and default to 1x1. PASS.
- **§3.2.1 / §4.5 One class per file**: The new `DashboardTileSize` enum gets its own file in
  `StageFright.Plugins.Contracts`. PASS.
- **§4.7.1 Code-behind pattern**: `Dashboard.razor` markup changes stay declarative; any new logic
  (mapping a tile's size to a CSS class) lives in `Dashboard.razor.cs`, not an inline `@code` block.
  PASS.
- **§4.7.2 CSS isolation**: The grid/tile-size rules are shared across the whole Dashboard page, so
  they belong in the global stylesheet (`wwwroot/app.css`) alongside the existing `.sf-dash-tile`
  rules, not a new `.razor.css` file. PASS.
- **§7.3 Prohibited**: No custom JavaScript is introduced — CSS Grid (`grid-auto-flow: dense`)
  achieves the gap-free packing requirement declaratively. PASS.
- **§11 Testing**: New/changed behaviour (size resolution, default-to-1x1, CSS class applied per
  tile, Core Metrics/Extensions grouping preserved) is covered by bUnit component tests extending
  `DashboardTests.cs`, following `Should_[ExpectedBehavior]_When_[Condition]` naming. No new
  service/repository logic, so no new unit/integration test layer is required beyond the UI layer.
  PASS.

No violations identified. Complexity Tracking table is not needed.

## Project Structure

### Documentation (this feature)

```text
specs/006-dashboard-tile-sizes/
├── plan.md              # This file (/speckit-plan command output)
├── research.md          # Phase 0 output (/speckit-plan command)
├── data-model.md        # Phase 1 output (/speckit-plan command)
├── quickstart.md        # Phase 1 output (/speckit-plan command)
├── contracts/           # Phase 1 output (/speckit-plan command)
└── tasks.md             # Phase 2 output (/speckit-tasks command - NOT created by /speckit-plan)
```

### Source Code (repository root)

```text
src/StageFright.Plugins.Contracts/
├── IDashboardTileProvider.cs      # MODIFIED: add TileSize default member
└── DashboardTileSize.cs           # NEW: enum { OneByOne, OneByTwo, TwoByOne, TwoByTwo }

src/StageFright.UI/Pages/Dashboard/
├── Dashboard.razor                # MODIFIED: CSS Grid container, per-tile size class
└── Dashboard.razor.cs             # MODIFIED (if any size→CSS-class mapping logic is needed)

src/StageFright.UI/Modules/*/*.cs  # OPTIONAL: existing tile providers (e.g.
                                    # AttendanceTrendDashboardTileProvider,
                                    # CashFlowDashboardTileProvider) opt into a
                                    # larger TileSize where their content benefits

src/StageFright.App/wwwroot/app.css # MODIFIED: replace `.row.row-cols-*` grid with a
                                     # CSS Grid (`grid-auto-flow: dense`) + tile-size classes

tests/StageFright.UI.Tests/Pages/Dashboard/
└── DashboardTests.cs              # MODIFIED: new cases for size class + packing/grouping
```

**Structure Decision**: Single-project MAUI Blazor Hybrid structure (constitution §4.1, §7.1) — no
new module or project. This feature extends the existing `StageFright.Plugins.Contracts` extension
point (`IDashboardTileProvider`) and the existing Dashboard module's UI (`StageFright.UI/Pages/Dashboard`)
and shared stylesheet (`StageFright.App/wwwroot/app.css`). No backend/frontend split and no new
top-level directories are introduced.

## Complexity Tracking

*No Constitution Check violations — this section is not applicable.*
