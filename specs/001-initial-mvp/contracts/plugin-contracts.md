# Contracts: Plugin & Provider Extension Points

**Assembly**: `StageFright.Plugins.Contracts` (zero dependencies; one type per file). Referenced by core MVP modules and external plugins alike. MVP modules register implementations explicitly in DI; plugin assemblies in `Plugins/` are discovered at startup via `AssemblyLoadContext` reflection scan (research.md R4). Every provider failure is isolated: caught → `PluginLoadException`/structured log → provider skipped.

## IDashboardTileProvider (FR-010, FR-011)

```csharp
public interface IDashboardTileProvider
{
    string TileId { get; }            // Unique; duplicates skipped with warning log
    string Title { get; }
    string ModuleName { get; }
    int DisplayOrder { get; }         // Core tiles 0-99 (Members=10, Rehearsals=20, Events=30, Finance=40); plugins 100+
    Type TileComponentType { get; }   // Blazor component rendered in the tile body
    Task<TileData> GetTileDataAsync(CancellationToken ct);
}

public class TileData
{
    public IReadOnlyDictionary<string, string> Metrics { get; init; }  // label → formatted value
    public string? AccentColor { get; init; }                          // e.g. muted green/red for Finance balance
    public string? NavigateRoute { get; init; }                        // click-through target
}
```

Behavioral contract: tiles load **in parallel**; the dashboard renders the grid immediately and each tile populates independently. A throwing/slow provider shows "Unable to load" in its own tile and never blocks others (NFR-007). Core tiles render in a fixed top section (2-column grid); plugin tiles in a labeled "Extensions" section below.

## ISettingsTabProvider (FR-018, Constitution §4.3)

```csharp
public interface ISettingsTabProvider
{
    string TabTitle { get; }
    string TabIcon { get; }
    string TabKey { get; }            // For deep-linking: /settings?tab={TabKey}; duplicates skipped + logged
    int DisplayOrder { get; }         // Core tabs 0-99; plugin tabs 100+
    Type SettingsComponentType { get; }   // Blazor component owning content, validation, save/cancel
}
```

Core tabs: General (0), Categories (10), Event Types (20), Backup (30), Restore (40). A failing tab provider is skipped; remaining tabs render (spec edge case).

## IMenuItemProvider (Constitution §4.6, spec Menu Extensibility)

```csharp
public interface IMenuItemProvider
{
    string ModuleName { get; }
    int DisplayOrder { get; }
    IReadOnlyList<MenuItem> GetMenuItems();
}

public class MenuItem
{
    public string Title { get; set; }
    public string Route { get; set; }            // Blazor route; navigated via NavigationManager only
    public string? Icon { get; set; }
    public int DisplayOrder { get; set; }
    public List<MenuItem> SubItems { get; set; } = new();
    public string? BadgeText { get; set; }
}
```

Rendering order: Dashboard (core, always first) → module items by provider DisplayOrder (Members=1, Rehearsals=2, Events=3, Finance=4, Reports=5) → plugin items → Settings (core, always last). Route prefixes are module-owned; conflicts are a registration error (logged, later registrant skipped).

## IDataAccessProvider (FR-042/FR-043, NFR-017)

```csharp
public interface IDataAccessProvider
{
    string PluginName { get; }                    // Used as table prefix + migrations-history table suffix
    Type DbContextType { get; }                   // Plugin-owned DbContext targeting the shared SQLite file
    void RegisterServices(IServiceCollection services);  // Plugin repositories + services
}
```

Startup sequence (DAL `PluginMigrationRunner`):

1. Core `StageFrightDbContext.Database.Migrate()` runs first.
2. For each discovered provider: construct plugin DbContext on the shared connection string with `MigrationsHistoryTable("__EFMigrationsHistory_" + PluginName)`; run `Database.Migrate()`.
3. Any failure → `PluginLoadException` logged with plugin name; plugin skipped; startup continues.

Plugin tables MUST be prefixed `{PluginName}_` to avoid collisions with core tables. Plugin data participates in backup only from Phase 2+ (MVP backup covers the 10 core entity types).

## Discovery & error-handling contract (all providers)

- `Plugins/` directory auto-created at app root on startup if missing (FR-021); read-only filesystem errors handled gracefully (logged, discovery skipped).
- Each `*.dll` loads in its own `AssemblyLoadContext`; `StageFright.Plugins.Contracts` resolves to the host copy (type identity).
- Discovery logs (Serilog, structured): assemblies found, providers registered per contract, failures with exception detail (NFR-008).
- Provider exceptions during use (not just load) are caught at the consuming registry (dashboard/settings/menu/reports) and degrade gracefully per NFR-007.
