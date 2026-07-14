# Research: Dashboard Tile Sizes

No `NEEDS CLARIFICATION` markers remained in the Technical Context after drafting (all decisions
below have a clear, low-risk default given the existing codebase). Documented here for traceability.

## Decision: Represent tile size as a `DashboardTileSize` enum exposed via a default interface member

- **Decision**: Add `DashboardTileSize TileSize => DashboardTileSize.OneByOne;` as a default member
  on `IDashboardTileProvider`, alongside a new `DashboardTileSize` enum (`OneByOne`, `OneByTwo`,
  `TwoByOne`, `TwoByTwo`).
- **Rationale**: `IDashboardTileProvider` already uses default interface members for optional,
  backward-compatible capabilities (`NavigateRoute => null`, `ActionText => null`). Following the
  same pattern means existing core tile providers and any third-party plugin assembly built against
  the old contract continue to compile and behave exactly as today (FR-003, constitution §12.2).
- **Alternatives considered**:
  - A `(int Width, int Height)` tuple — rejected because it allows invalid combinations (e.g. 3x3)
    the spec explicitly scopes out ("four pre-set sizes... arbitrary or custom tile dimensions are
    out of scope").
  - A required (non-default) interface member — rejected because it would be a breaking change for
    any existing plugin implementing `IDashboardTileProvider`, violating §12.2.

## Decision: Replace the Bootstrap `row-cols` grid with a CSS Grid using `grid-auto-flow: dense`

- **Decision**: Change each tile section's container from `row row-cols-1 row-cols-sm-2 row-cols-lg-3 g-2`
  to a CSS Grid (`display: grid`, `grid-template-columns: repeat(...)`, `grid-auto-flow: dense`,
  `gap`), with each tile card assigned `grid-column: span N; grid-row: span N` based on its
  `TileSize`.
- **Rationale**: CSS Grid's dense auto-placement algorithm is purpose-built for packing
  mixed-size boxes without gaps (FR-004) and is pure CSS — no custom JavaScript is introduced,
  satisfying constitution §7.3. It also collapses cleanly to a single column via a `@media` breakpoint
  on narrow widths (FR-005), matching the existing Bootstrap breakpoints already used elsewhere in the
  stylesheet.
- **Alternatives considered**:
  - Keep Bootstrap's `row-cols` flex-based grid and use `col-*` span classes — rejected because
    Bootstrap's row-based grid does not backfill gaps left by variable-height tiles (no dense
    packing equivalent), which would violate FR-004/SC-001.
  - A JavaScript masonry/packing library — rejected outright by constitution §7.3 (no custom
    JavaScript / no JS interop for business logic).

## Decision: Keep size-aware packing scoped within each existing "Core Metrics" / "Extensions" section

- **Decision**: Each section (`Core Metrics`, `Extensions`) gets its own independent CSS Grid
  container; tiles are not packed across section boundaries.
- **Rationale**: Documented as an assumption in `spec.md` and confirmed by the existing `Dashboard.razor`
  structure, which already renders two separate `<section>` elements with independent grids
  (`DashboardTests.cs` asserts Core/Extensions isolation). Preserving this boundary satisfies FR-006
  and requires no change to the existing grouping logic in `Dashboard.razor.cs`.
- **Alternatives considered**: A single grid spanning both sections — rejected; it would let a
  plugin tile visually interleave with core tiles, contradicting existing tests
  (`CoreTile_DoesNotAppearInExtensionsSection`) and the established mental model of "your data" vs.
  "extensions".
