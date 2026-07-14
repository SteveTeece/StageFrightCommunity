# Contract: `IDashboardTileProvider` (amended)

This is the plugin/module extension point (constitution §8, §4.2) that both core modules and
external plugins implement to contribute a dashboard tile. This feature adds one new default
member; every other member is unchanged.

## Amended shape

```csharp
namespace StageFright.Plugins.Contracts;

public interface IDashboardTileProvider
{
    string TileId { get; }
    string Title { get; }
    string ModuleName { get; }
    int DisplayOrder { get; }
    Type TileComponentType { get; }
    string? NavigateRoute => null;
    string? ActionText => null;

    /// <summary>
    /// Pre-set size the tile should render at on the Dashboard grid. Defaults to 1x1
    /// (OneByOne) for providers that don't override it, matching current behaviour.
    /// </summary>
    DashboardTileSize TileSize => DashboardTileSize.OneByOne;

    Task<TileData> GetTileDataAsync(CancellationToken ct);
}
```

## New supporting type

```csharp
namespace StageFright.Plugins.Contracts;

public enum DashboardTileSize
{
    OneByOne,
    OneByTwo,
    TwoByOne,
    TwoByTwo
}
```

## Compatibility contract

- **Existing core providers** (e.g. `MembersDashboardTileProvider`) that do not override `TileSize`
  continue to render at `OneByOne`, identical to today — satisfies FR-003.
- **Existing/third-party plugin assemblies** built against the previous interface version continue
  to compile and load: `TileSize` is a default interface member, so it is not a breaking change to
  the ABI/contract — satisfies constitution §12.2 and spec.md FR-002/FR-003.
- **Consumers** (`Dashboard.razor` / `Dashboard.razor.cs`, `DashboardService`) read `provider.TileSize`
  the same way they already read `provider.NavigateRoute`/`provider.ActionText` — no change to
  `IDashboardService`, `TileLoadResult`, or `TileData`.

## Test contract (to be implemented as tasks)

- A provider that does not override `TileSize` reports `DashboardTileSize.OneByOne`.
- A provider that overrides `TileSize` reports the overridden value.
- `Dashboard.razor` applies a size-specific CSS class (e.g. `tile-size-2x1`) matching the provider's
  `TileSize` for every rendered tile, in both the Core Metrics and Extensions sections.
