# Feature Specification: Dashboard Tile Sizes

**Feature Branch**: `006-dashboard-tile-sizes`

**Created**: 2026-07-14

**Status**: Draft

**Input**: GitHub Issue #231 — "[FEATURE] Dashboard tile sizes"

> Dashboard tiles can appear cluttered when presenting significant data. Tiles should be easy to
> read, clearly visible, and allow the user to get an overview of the organisation at a glance.
> Tile providers should be able to pick between a pre-set range of tile sizes. Consider the default
> Membership Summary tile — this should be considered a 1x1 tile. Other tile size options: 1x2
> (double width), 2x1 (double height), 2x2 (double height, double width). Tiles should be able to
> determine their position on the dashboard to cleanly fit.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Glanceable dashboard with right-sized tiles (Priority: P1)

As a committee member viewing the Dashboard, I want tiles that present more detailed data (trends,
breakdowns, multi-line summaries) to appear larger than simple single-metric tiles, so I can
distinguish at-a-glance which tiles carry more information and read them without the layout feeling
cluttered.

**Why this priority**: This is the core value proposition of the feature — an uncluttered, readable
dashboard — and is required for any other part of the feature to matter.

**Independent Test**: Can be fully tested by opening the Dashboard and confirming that at least one
data-rich tile (e.g., an attendance trend or cash flow tile) renders visibly larger than a simple
summary tile (e.g., Membership Summary), and that both are fully readable without truncation or
overlap.

**Acceptance Scenarios**:

1. **Given** the Dashboard has a mix of tiles configured at different sizes, **When** the page
   loads, **Then** each tile renders at its configured size (1x1, 1x2, 2x1, or 2x2) with its full
   content visible.
2. **Given** a tile does not declare a size, **When** the Dashboard renders it, **Then** it displays
   at the standard 1x1 size (matching current behaviour).

---

### User Story 2 - Tiles pack cleanly regardless of size mix (Priority: P2)

As a committee member, I want the dashboard to arrange tiles of different sizes neatly — without
overlapping tiles or odd empty gaps — so the page looks intentional and organized no matter which
tiles are present or how the browser window is sized.

**Why this priority**: Without clean packing, mixed tile sizes would make the dashboard look broken
rather than improved, undermining the goal of the feature.

**Independent Test**: Can be fully tested by loading the Dashboard with a combination of 1x1, 1x2,
2x1, and 2x2 tiles present at once and visually confirming no tiles overlap and no row is left with
an avoidable empty gap, then resizing the browser window and confirming the layout reflows cleanly.

**Acceptance Scenarios**:

1. **Given** the Dashboard contains tiles of mixed sizes, **When** it renders, **Then** no two tiles
   overlap and no tile is clipped or hidden.
2. **Given** the browser window is narrowed to a typical mobile width, **When** the Dashboard
   re-renders, **Then** wider/taller tiles stack in a single column in the same relative order,
   remaining fully readable without horizontal scrolling.
3. **Given** the existing "Core Metrics" and "Extensions" grouping sections, **When** tiles of mixed
   sizes are present within each section, **Then** each section packs its own tiles cleanly and the
   two sections remain visually separated as they are today.

---

### User Story 3 - Module owners choose the right size for a tile's content (Priority: P3)

As a person adding or maintaining a dashboard tile (a core module or a plugin), I want to choose
which of the pre-set sizes best fits the tile's content, so a tile with a chart or multiple
breakdown rows isn't squeezed into a space meant for a single metric.

**Why this priority**: This is what makes Stories 1 and 2 possible, but it is a supporting/enabling
capability rather than something end users directly observe, so it ranks below the user-facing
outcomes.

**Independent Test**: Can be fully tested by configuring an existing tile (e.g., Attendance Trend) to
a larger size and confirming the Dashboard reflects the new size on next load without other tiles
being affected.

**Acceptance Scenarios**:

1. **Given** a tile is configured with one of the four pre-set sizes, **When** the Dashboard loads,
   **Then** that size is applied consistently every time, until the configuration is changed again.
2. **Given** two tiles from different modules (core and plugin) both request a larger size, **When**
   the Dashboard renders, **Then** both render at their requested size and the layout still packs
   cleanly per Story 2.

### Edge Cases

- What happens when a 2x2 (or other multi-cell) tile doesn't fit the remaining space in the current
  row? The layout must wrap it to the next row rather than overlapping or clipping it.
- What happens when every tile on the dashboard requests the same larger size (e.g., all 2x1)? The
  layout must still pack them without gaps, even though no size variety is present.
- What happens when a tile fails to load its data (existing "Unable to load" error state)? The
  tile's configured size and position must be unaffected by the load failure.
- What happens on a very narrow (mobile-width) screen when a 1x2 or 2x2 tile is present? It must
  stack to full width rather than being cut off or forcing horizontal scrolling.
- What happens when a new tile is added to a dashboard that already has tiles arranged edge-to-edge?
  The new tile must be placed cleanly without displacing or overlapping existing tiles' visual order.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Dashboard MUST support four standardized tile sizes: 1x1 (standard/default), 1x2
  (double width), 2x1 (double height), and 2x2 (double width and height).
- **FR-002**: Each dashboard tile MUST be able to declare which of the four sizes it should render
  at, as part of the tile's own definition (set by the module or plugin that provides the tile).
- **FR-003**: A tile that does not declare a size MUST render at the standard 1x1 size, matching
  today's behaviour.
- **FR-004**: The Dashboard layout MUST automatically arrange tiles of mixed sizes so that no tiles
  overlap, no tile is clipped, and no row is left with an avoidable empty gap.
- **FR-005**: The Dashboard layout MUST remain fully readable and usable — without horizontal
  scrolling or clipped content — across the screen widths the application already supports, with
  wider/taller tiles stacking to a single column on narrow/mobile widths.
- **FR-006**: The existing "Core Metrics" and "Extensions" tile groupings MUST be preserved, with
  size-aware packing applied independently within each group.
- **FR-007**: Existing tile behaviours — click-through navigation, the header action link, and the
  loading/error states — MUST continue to work unchanged regardless of a tile's configured size.
- **FR-008**: A tile's configured size MUST persist across dashboard loads until the tile's
  definition is changed again; end users MUST NOT need to manually resize tiles each visit.

### Key Entities

- **Dashboard Tile**: A single card on the Dashboard contributed by a core module or plugin.
  Relevant attributes: title, owning module, display order/grouping, and its size (one of 1x1, 1x2,
  2x1, 2x2). Existing attributes (metrics shown, accent colour, click-through route) are unaffected
  by this feature.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: On every dashboard load, across all supported screen widths, zero tiles overlap and
  zero rows contain an avoidable empty gap.
- **SC-002**: 100% of tiles that present multiple data points (e.g., trends, multi-row breakdowns)
  can be configured to a larger size than the standard single-metric tile, without any per-tile
  custom layout work.
- **SC-003**: On a typical mobile-width screen, 100% of dashboard tiles remain fully readable with no
  horizontal scrolling required.
- **SC-004**: Existing dashboard tiles that don't opt into a larger size continue to render exactly
  as they do today (no visual regression for 1x1 tiles).

## Assumptions

- Tile size is a fixed property set by the module or plugin that provides the tile (a developer-time
  choice), not a setting end users adjust themselves at runtime. This matches the issue's phrasing
  ("tile providers should be able to pick") and keeps the feature scoped to layout, not a
  user-customizable dashboard.
- On narrow/mobile-width screens, tiles wider or taller than 1x1 stack to full width in a single
  column, in the same relative order as on wider screens, consistent with how the existing
  responsive grid already behaves.
- The existing "Core Metrics" and "Extensions" section grouping is retained; size-aware packing
  happens within each section rather than across the whole page.
- The four pre-set sizes (1x1, 1x2, 2x1, 2x2) are sufficient for the current set of dashboard tiles;
  arbitrary or custom tile dimensions are out of scope.
- No existing tile's default size changes as part of this feature unless a module owner explicitly
  opts it into a larger size.
