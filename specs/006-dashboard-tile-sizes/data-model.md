# Data Model: Dashboard Tile Sizes

No database entities or migrations are introduced by this feature — tile size is a provider-declared
UI concern (like the existing `NavigateRoute`/`ActionText`), not persisted application data.

## DashboardTileSize (enum)

New type in `StageFright.Plugins.Contracts`, one per file per constitution §3.2.1.

| Value      | Grid footprint            | Notes                                   |
|------------|----------------------------|------------------------------------------|
| `OneByOne` | 1 column × 1 row (default) | Matches every tile's current rendered size |
| `OneByTwo` | 2 columns × 1 row           | "Double width" per Issue #231             |
| `TwoByOne` | 1 column × 2 rows           | "Double height" per Issue #231            |
| `TwoByTwo` | 2 columns × 2 rows          | "Double height, double width"             |

## Dashboard Tile (conceptual entity, from spec.md Key Entities)

Represented by the existing `IDashboardTileProvider` contract plus its size, not a new class:

| Attribute      | Source                                   | Notes                                              |
|----------------|-------------------------------------------|-----------------------------------------------------|
| `TileId`       | `IDashboardTileProvider.TileId`           | Unchanged                                            |
| `Title`        | `IDashboardTileProvider.Title`            | Unchanged                                            |
| `ModuleName`   | `IDashboardTileProvider.ModuleName`       | Unchanged                                            |
| `DisplayOrder` | `IDashboardTileProvider.DisplayOrder`     | Unchanged; still separates Core (<100) vs Extensions (≥100) |
| `TileSize`     | `IDashboardTileProvider.TileSize` **(new)** | Default interface member, defaults to `OneByOne` (FR-003) |

**Validation rules**: None beyond the enum's fixed set of four values — `DashboardTileSize` being a
plain enum means invalid/out-of-range sizes are a compile-time impossibility for any well-formed
provider (arbitrary custom dimensions are explicitly out of scope per spec.md Assumptions).

**State transitions**: None. A tile's size is fixed at compile time by its provider implementation
(FR-008 — persists across loads because it isn't runtime-mutable state at all).
