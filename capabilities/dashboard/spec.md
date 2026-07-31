# Dashboard — Living Spec

> [DRAFT] Surface-first draft from existing code — every requirement is observed from the code surface unless tagged otherwise. Review before trusting.

## Purpose

The Dashboard is the app's landing page (registered as the home nav entry, DisplayOrder 0): a single glanceable screen where every module — core and plugin alike — can surface a metric tile. Without it, a user (or a plugin author) has no single place to see cross-module status at a glance, and modules would have no supported way to advertise a summary of their own data on first load.

## Requirements

### Tile providers load independently so one failing tile never breaks the page
Each registered `IDashboardTileProvider`'s data load MUST be attempted in isolation; an exception from one provider MUST be caught and turned into a per-tile error state rather than propagating and preventing the rest of the dashboard from rendering.

#### Scenario: a tile's data source throws
- **WHEN** a provider's tile-data load throws during `OnParametersSetAsync`
- **THEN** that tile shows "Unable to load {Title}" in place of its body
- **AND** every other tile on the page still loads and renders normally

### Tile providers are split into Core and Extension sections by DisplayOrder
Providers MUST be partitioned into a "Core Metrics" section (DisplayOrder < 100) and an "Extensions" section (DisplayOrder >= 100), and the Extensions section MUST be omitted entirely when no provider falls in that band. This lets plugin authors land tiles in a visually distinct area purely by choosing a DisplayOrder, with no other integration step.

#### Scenario: no plugins are installed
- **WHEN** every registered provider has DisplayOrder < 100
- **THEN** the "Extensions" heading and section are not rendered at all

#### Scenario: a plugin registers a tile
- **WHEN** a provider is registered with DisplayOrder >= 100
- **THEN** it renders under "Extensions", separate from the built-in module tiles

### Dashboard data is reloaded fresh on every visit, never shown stale
Navigating to the dashboard MUST re-fetch the provider list and start new load tasks every time, even if the component instance is reused by the router, so a value that changed elsewhere in the app is reflected the next time the user lands here.

#### Scenario: a value changes elsewhere then the user returns to the dashboard
- **WHEN** the user navigates away from `/dashboard` and back to it
- **THEN** all tiles re-run their data loads rather than displaying the previous visit's values

### A tile's visual footprint is controlled solely by its declared size, decoupled from its content
A provider SHALL declare its footprint via a size property (default: smallest/1x1) and the shell SHALL translate that into a layout class; growing or shrinking a tile MUST require only that one property changing on the provider, with no change to the tile's body component, other providers, or the grid container markup.

#### Scenario: a provider is resized
- **WHEN** a provider's declared size changes (e.g. from the default single-cell size to a double-width or double-height size)
- **THEN** the tile occupies the new footprint in the grid on next render
- **AND** no other provider or the tile's own body component needs to change

### A provider's data load is a mount gate, not the tile's content source
The provider's asynchronous data load determines only whether the tile's body component is allowed to mount (success) or is replaced with an error state (failure); the tile body component is responsible for fetching and rendering its own displayed content independently once mounted. [inferred]

#### Scenario: a provider's load succeeds trivially
- **WHEN** a provider's data load resolves without error
- **THEN** its body component mounts and performs its own independent data fetch and loading/error handling for what it displays

### Tile rendering never shows content left over from a previous load cycle
When a tile is given a new load task (e.g. on dashboard refresh), the previously rendered body component MUST be discarded and rebuilt rather than reused, so a fast-completing new load can never be mistaken for stale output from the prior one.

#### Scenario: a dashboard refresh supplies a new load task before the previous render has settled
- **WHEN** a tile receives a new load task while transitioning states
- **THEN** its body component is torn down and recreated for the new task rather than showing UI carried over from the old one

### Tile cards support click-through navigation with an optional secondary action link
A tile card whose provider declares a navigation route MUST be clickable as a whole to reach that route; a separate labeled action link MUST appear only when both the route and an action label are declared, and activating that link MUST navigate without also triggering the surrounding card's own click handler.

#### Scenario: a tile declares a route but no action label
- **WHEN** a provider sets a navigation route but no action text
- **THEN** the whole card is clickable to that route and no action link is shown

#### Scenario: a tile declares both a route and an action label
- **WHEN** a provider sets both a navigation route and action text
- **THEN** a labeled link appears in the tile header
- **AND** clicking that link navigates once, without also firing the card's own click-through handler

### External plugin tile providers extend the dashboard without risking app startup
A plugin assembly's tile provider(s) MUST be discoverable and registerable at runtime without a code change to the host app, and a failure while loading or registering a plugin assembly MUST be caught and logged rather than prevent the application (or the rest of the dashboard) from starting.

#### Scenario: a plugin assembly is broken
- **WHEN** a plugin DLL in the plugins directory fails to load or throws during type discovery
- **THEN** the failure is logged and that assembly's providers are skipped
- **AND** the application still starts and the dashboard still renders every other tile

## Uncovered

_None — every file in the area was read._
