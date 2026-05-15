# Dashboard Specification: StageFright Community
*Phase 1 Feature* | **Date**: February 4, 2026

## Overview

The Dashboard is the startup screen of StageFright Community, providing at-a-glance visibility into critical organizational metrics. It serves as a command center for volunteer coordinators, displaying key statistics for Members, Rehearsals, Events, and Finance in a clean, extensible tile-based layout.

The dashboard supports a **plugin architecture** that allows future extensions to add custom tiles without modifying the core system.
Built-in tiles follow the same model: each related module/slice owns its tile provider and injects tile data into the dashboard through provider registration. Rehearsals and Events are distinct modules in MVP and contribute through separate contracts.

---

## Design Principles

1. **At-a-Glance Clarity**: All essential metrics visible on app startup
2. **Modern, Compact Aesthetic**: Clean, space-efficient layouts that prioritize information density and readability; supports both **dark** and **light** themes with consistent visual language and a pastel/muted palette preference
3. **Minimal White Space**: Compact, space-efficient layouts that maximize information density while maintaining readability
4. **Accessibility First**: WCAG 2.1 AA compliant; keyboard navigation; screen reader support; sufficient color contrast in both themes
5. **Extensibility**: Plugin-driven tile system allows custom metrics without core changes
6. **Module Ownership**: Tiles are owned by the feature module they represent; dashboard acts as composition host, not business owner
7. **Performance**: Dashboard renders essential metrics on startup using progressive rendering; tiles should render progressively and degrade gracefully when data is slow or unavailable
8. **Responsive**: Adapts seamlessly to screen sizes from 1024×768 to 2560×1440 using Bootstrap 5 and/or free Radzen Blazor responsive layout components and breakpoints
9. **Implementation Language**: UI and all application logic MUST be C#/.NET with .NET MAUI host + Blazor Hybrid. All functional dashboard UI must be rendered by Blazor components. Do not implement business logic in JavaScript; avoid custom JavaScript where possible. Bootstrap 5 and free Radzen Blazor components may be used for styling/component composition while interactive behaviors remain in C#.
10. **Navigation Standard**: All dashboard-driven navigation actions MUST call `NavigationManager.NavigateTo(...)`.
11. **Future Visualization Ready**: Tile payloads may include graph/chart-ready summary data for future builds without altering registration contracts

---

## Layout & Composition

### 1. Dashboard Structure

**Header Section**:
- Organization name (from Settings.OrganizationName)
- Organization logo (from Settings.OrganizationLogo), if available
- Current date and quick navigation actions wired through `NavigationManager.NavigateTo(...)`

**Tile Grid**:
- Responsive grid layout (4 columns on 1920px; 3 columns on 1280px; 2 columns on 768px; 1 column on 480px)
- Each tile is a self-contained component displaying a single metric or category
- Tiles sized uniformly (responsive padding/margins)
- Tile presentation uses Bootstrap 5 card pattern and/or equivalent free Radzen card/panel components with rounded corners for a modern compact look
- Empty state: If no tiles registered, display "No dashboard data available" message

**Core Tiles** (Always Present):

#### Tile 1: Member Summary (Members)
```
┌─────────────────────┐
│  Members            │
├─────────────────────┤
│  Active: 24         │
│  Inactive: 3        │
│  Total: 27          │
│  Outstanding Fees   │
│  (shows count of    │
│   members with      │
│   unpaid fees)      │
└─────────────────────┘
```

**Data Points**:
- Active Members (count of Members where Status = "Active")
- Inactive Members (count of Members where Status = "Inactive")
- Total Members (Active + Inactive)
- Members with Outstanding Fees (count of members with combined annual + attendance fee balance > 0)

**Actions** (on click):
- Click on any statistic: Call `NavigationManager.NavigateTo(...)` to open Members page with appropriate filter applied
- "View Details" button: Call `NavigationManager.NavigateTo(...)` to open Members page

**Example Content**:
```
Members
═══════════════════════════════════
Active Members:        24
Inactive Members:       3
────────────────────────────────────
Total:                 27
────────────────────────────────────
Outstanding Fees:       8 members
═══════════════════════════════════
[View Members]
```

---

#### Tile 2: Rehearsal Summary (Rehearsals)
```
┌─────────────────────┐
│  Rehearsals         │
├─────────────────────┤
│  Upcoming: 5        │
│  Next: Feb 12       │
│  Last Attendance:   │
│  18 of 22 (82%)     │
└─────────────────────┘
```

**Data Points**:
- Upcoming Rehearsals (count of Rehearsal records where Date >= today, ordered by date)
- Next Rehearsal Date (nearest upcoming rehearsal date)
- Last Rehearsal Attendance Rate (most recent rehearsal attendance: count of attendees / total active members)

**Actions** (on click):
- Click on "Upcoming Rehearsals": Navigate to Rehearsals page with future date filter
- "View Details" button: Open Rehearsals page

**Example Content**:
```
Rehearsals
═══════════════════════════════════
Upcoming:               5
────────────────────────────────────
Next Rehearsal:       Feb 12, 2026
Attendance:            18 of 22 (82%)
═══════════════════════════════════
[View Rehearsals]
```

---

#### Tile 3: Event Summary (Events)
```
┌─────────────────────┐
│  Events             │
├─────────────────────┤
│  Upcoming: 2        │
│  Next: Mar 8        │
│  Last Participants: │
│  16 of 22 (73%)     │
└─────────────────────┘
```

**Data Points**:
- Upcoming Performances (count of Performance records where Date >= today, ordered by date)
- Next Performance Date (nearest upcoming performance date)
- Last Performance Participant Rate (participants in most recent performance / active members)

**Actions** (on click):
- Click on "Upcoming": Navigate to Events page with future date filter
- "View Details" button: Open Events page

**Example Content**:
```
Events
═══════════════════════════════════
Upcoming Performances:  2
Next Performance:      Mar 8, 2026
────────────────────────────────────
Last Performance:      Feb 1, 2026
Participants:          16 of 22 (73%)
═══════════════════════════════════
[View Events]
```

---

#### Tile 4: Financial Summary (Finance)
```
┌─────────────────────┐
│  Finance            │
├─────────────────────┤
│  Total Income       │
│  (2026): $2,145.00  │
│  Total Expenses     │
│  (2026): $890.50    │
│  Net Balance:       │
│  $1,254.50          │
│  MTD Income: $485   │
│  Outstanding Fees:  │
│  $420.00            │
└─────────────────────┘
```

**Data Points**:
- Total Income (sum of all Income records from January 1 to December 31 of current calendar year; non-deleted)
- Total Expenses (sum of all Expense records from January 1 to December 31 of current calendar year; non-deleted)
- Net Balance (Total Income - Total Expenses for current year)
- Outstanding Fees (sum of all unpaid annual + attendance fees across all members)
- Month-to-Date Income (sum of Income records from first day of current month to today)

**Actions** (on click):
- Click on "Total Income": Navigate to Finance page with Income filter
- Click on "Total Expenses": Navigate to Finance page with Expense filter
- "View Details" button: Open Finance page

**Color Coding**:
- Positive balance: Muted Green
- Negative balance: Muted Red
- Outstanding fees: Muted Orange/Amber (warning)

**Example Content**:
```
Finance
═══════════════════════════════════
Total Income (2026):   $2,145.00
Total Expenses (2026):   $890.50
────────────────────────────────────
Net Balance (2026):    $1,254.50 ✓
────────────────────────────────────
MTD Income (Feb):        $485.00
Outstanding Fees:        $420.00
═══════════════════════════════════
[View Finance]
```

---

### 2. Plugin Tile Architecture

#### Purpose
Allow module slices and third-party plugins to register dashboard tiles without modifying the core dashboard component.

#### Ownership and Injection Model
- A tile provider is implemented in the module/slice that owns the underlying data and business rules.
- Dashboard composition loads all registered providers at startup and injects their returned tile payloads into the grid.
- Adding a new function module tile or plugin tile requires only provider registration; no direct edits to Dashboard page layout/composition logic.
- Core tiles (Members, Rehearsals, Events, Finance) follow this same provider-based injection model.
- Rehearsals and Events remain separate modules in MVP and each owns its own tile provider and summary payload.

#### Plugin Interface

**IDashboardTileProvider**:
```csharp
namespace StageFright.Domain.Dashboard;

/// <summary>
/// Plugin interface for providing custom dashboard tiles.
/// Plugins implement this interface to register tiles on the dashboard.
/// </summary>
public interface IDashboardTileProvider
{
    /// <summary>
    /// Unique identifier for this tile provider plugin.
    /// </summary>
    string ProviderId { get; }

    /// <summary>
    /// Display name of the tile (shown in UI).
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// Optional description of what this tile displays.
    /// </summary>
    string? Description { get; }

    /// <summary>
    /// Order in which this tile appears on the dashboard (lower = earlier).
    /// </summary>
    int DisplayOrder { get; }

    /// <summary>
    /// Generate the tile data asynchronously.
    /// Returns a DashboardTileDto ready for rendering.
    /// </summary>
    /// <returns>
    /// DashboardTileDto with Title, Statistics, Actions, and optional metadata.
    /// </returns>
    /// <exception cref="DashboardTileException">
    /// Thrown if tile data cannot be generated.
    /// </exception>
    Task<DashboardTileDto> GenerateTileAsync();

    /// <summary>
    /// Optional: Called when dashboard initializes.
    /// Plugins can perform setup operations here.
    /// </summary>
    Task InitializeAsync();

    /// <summary>
    /// Optional: Called when dashboard unloads.
    /// Plugins can clean up resources here.
    /// </summary>
    Task CleanupAsync();
}
```

#### DashboardTileDto
```csharp
namespace StageFright.Domain.Dashboard;

/// <summary>
/// Data transfer object for a dashboard tile.
/// </summary>
public class DashboardTileDto
{
    /// <summary>Unique identifier (inherited from provider ID).</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Display title of the tile.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Key-value pairs of statistics to display (e.g., "Active Members": "24").</summary>
    public Dictionary<string, string> Statistics { get; set; } = new();

    /// <summary>Color theme (Primary, Success, Warning, Danger, Info).</summary>
    public string Theme { get; set; } = "Primary";

    /// <summary>Optional list of action buttons.</summary>
    public List<DashboardActionDto> Actions { get; set; } = new();

    /// <summary>Summary sentence describing essential information shown in this tile.</summary>
    public string Summary { get; set; } = string.Empty;

    /// <summary>Optional chart descriptor for future dashboard visualizations (MVP may leave null).</summary>
    public DashboardChartDto? Chart { get; set; }

    /// <summary>Optional metadata (e.g., last updated timestamp).</summary>
    public Dictionary<string, object> Metadata { get; set; } = new();

    /// <summary>Tile height preference (Auto, Small, Medium, Large).</summary>
    public string HeightPreference { get; set; } = "Medium";
}
```

#### DashboardChartDto
```csharp
namespace StageFright.Domain.Dashboard;

/// <summary>
/// Optional chart payload for future tile visualizations.
/// MVP implementations may omit this payload.
/// </summary>
public class DashboardChartDto
{
    /// <summary>Chart type (Line, Bar, Area, Donut).</summary>
    public string Type { get; set; } = "Line";

    /// <summary>Series data points keyed by series name.</summary>
    public Dictionary<string, IReadOnlyList<decimal>> Series { get; set; } = new();

    /// <summary>Axis/category labels aligned by index with series values.</summary>
    public IReadOnlyList<string> Labels { get; set; } = Array.Empty<string>();

    /// <summary>Optional unit suffix or prefix (e.g., "$", "%", "members").</summary>
    public string? Unit { get; set; }
}
```

**Chart payload rules (future builds):**
- Tile summaries remain the primary information path; charts are supplemental.
- If `Chart` is present, `Summary` must still be populated with essential information.
- Chart payload generation must respect graceful-degradation requirements; tiles must not block initial dashboard render when providers are slow or failing.

**Example — Unpaid Member Fees Pie Chart payload:**
```csharp
var unpaidFeesPieChart = new DashboardChartDto
{
    Type = "Donut",
    Labels = new[] { "Members With Unpaid Fees", "Members Up To Date" },
    Series = new Dictionary<string, IReadOnlyList<decimal>>
    {
        ["Members"] = new decimal[] { 8m, 19m }
    },
    Unit = "members"
};
```

**Example — Unpaid Fees by Type Pie Chart payload (Annual vs Attendance):**
```csharp
var unpaidFeesByTypePieChart = new DashboardChartDto
{
    Type = "Donut",
    Labels = new[] { "Annual Fees Unpaid", "Attendance Fees Unpaid" },
    Series = new Dictionary<string, IReadOnlyList<decimal>>
    {
        ["Amount"] = new decimal[] { 320m, 100m }
    },
    Unit = "$"
};
```

**Finance tile chart selection guidance:**
- Use the **member-count donut** when the goal is quick operational triage (how many members need follow-up).
- Use the **fee-type amount donut** when the goal is financial analysis (which unpaid fee type contributes most to outstanding balance).
- **MVP default behavior**: Render the member-count donut by default. Fee-type amount donut is optional for future builds (secondary/drill-down view).

#### DashboardActionDto
```csharp
namespace StageFright.Domain.Dashboard;

/// <summary>
/// Represents an action button on a dashboard tile.
/// </summary>
public class DashboardActionDto
{
    /// <summary>Display text on button.</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>Navigation route or handler command.</summary>
    public string Target { get; set; } = string.Empty;

    /// <summary>Style (Primary, Secondary, Danger, Info, Success, Warning).</summary>
    public string Style { get; set; } = "Primary";

    /// <summary>Optional icon identifier (CSS class or icon name).</summary>
    public string? Icon { get; set; }

    /// <summary>Tooltip text displayed on hover.</summary>
    public string? Tooltip { get; set; }
}
```

#### DashboardTileException
```csharp
namespace StageFright.Domain.Dashboard;

/// <summary>
/// Exception thrown when a dashboard tile provider encounters an error.
/// </summary>
public class DashboardTileException : DomainException
{
    public DashboardTileException(string providerId, string message, Exception? innerException = null)
        : base($"Dashboard tile provider '{providerId}' error: {message}", innerException)
    {
    }
}
```

---

### 3. Dashboard Service Interface

```csharp
namespace StageFright.Application.Dashboard;

/// <summary>
/// Service for managing dashboard tile providers and rendering the dashboard.
/// </summary>
public interface IDashboardService
{
    /// <summary>
    /// Register a tile provider plugin.
    /// </summary>
    /// <param name="provider">Implementation of IDashboardTileProvider.</param>
    void RegisterTileProvider(IDashboardTileProvider provider);

    /// <summary>
    /// Unregister a tile provider by ID.
    /// </summary>
    void UnregisterTileProvider(string providerId);

    /// <summary>
    /// Get all registered tile providers (in display order).
    /// </summary>
    IEnumerable<IDashboardTileProvider> GetTileProviders();

    /// <summary>
    /// Generate all dashboard tiles asynchronously.
    /// Tiles are generated in parallel for performance.
    /// If a tile generation fails, it is logged but does not halt dashboard load.
    /// </summary>
    /// <returns>
    /// List of DashboardTileDto objects, ordered by DisplayOrder.
    /// </returns>
    Task<List<DashboardTileDto>> GenerateDashboardAsync();

    /// <summary>
    /// Refresh a specific tile by provider ID.
    /// Useful for manual refresh or periodic updates.
    /// </summary>
    Task<DashboardTileDto> RefreshTileAsync(string providerId);
}
```

---

### 4. Plugin Registration

Plugins are registered during application startup in the dependency injection (DI) container.

**Core Plugins** (Built-In):
```csharp
// In Startup.cs or DI Configuration
services
    .AddScoped<IDashboardService, DashboardService>()
    
    // Register core tile providers
    .AddScoped<IDashboardTileProvider, MemberSummaryTileProvider>()
    .AddScoped<IDashboardTileProvider, EventSummaryTileProvider>()
    .AddScoped<IDashboardTileProvider, FinancialSummaryTileProvider>();

// Plugin registration (discovered at runtime)
var pluginAssemblies = PluginDiscovery.DiscoverPlugins("plugins/");
foreach (var pluginAssembly in pluginAssemblies)
{
    var tileProviders = pluginAssembly
        .GetTypes()
        .Where(t => typeof(IDashboardTileProvider).IsAssignableFrom(t) && !t.IsInterface);
    
    foreach (var providerType in tileProviders)
    {
        services.AddScoped(typeof(IDashboardTileProvider), providerType);
    }
}
```

---

### 5. Dashboard Component Rendering

**Blazor Component: Dashboard.razor**
```razor
@implements IAsyncDisposable
@inject IDashboardService DashboardService

<div class="dashboard-container">
    <div class="dashboard-header">
        <h1>@(Settings?.OrganizationName ?? "Dashboard")</h1>
        <p>@DateTime.Now.ToLongDateString()</p>
    </div>

    @if (Tiles == null)
    {
        <div class="loading">Loading dashboard...</div>
    }
    else if (Tiles.Count == 0)
    {
        <div class="empty-state">
            <p>No dashboard data available.</p>
        </div>
    }
    else
    {
        <div class="tile-grid">
            @foreach (var tile in Tiles)
            {
                <DashboardTile Tile="@tile" />
            }
        </div>
    }
</div>

@code {
    private List<DashboardTileDto>? Tiles;
    private SettingsDto? Settings;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            Settings = await SettingsService.GetSettingsAsync();
            Tiles = await DashboardService.GenerateDashboardAsync();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to load dashboard");
            Tiles = new();
        }
    }

    async ValueTask IAsyncDisposable.DisposeAsync()
    {
        // Cleanup
    }
}
```

**Sub-Component: DashboardTile.razor**
```razor
@using StageFright.Application.Dashboard

<div class="dashboard-tile tile-@(Tile.Theme.ToLower())">
    <div class="tile-header">
        <h3>@Tile.Title</h3>
    </div>
    <div class="tile-body">
        @foreach (var stat in Tile.Statistics)
        {
            <div class="statistic">
                <span class="label">@stat.Key:</span>
                <span class="value">@stat.Value</span>
            </div>
        }
    </div>
    @if (Tile.Actions.Any())
    {
        <div class="tile-footer">
            @foreach (var action in Tile.Actions)
            {
                <button 
                    class="btn btn-@action.Style" 
                    @onclick="@(() => HandleAction(action))"
                    title="@action.Tooltip">
                    @action.Label
                </button>
            }
        </div>
    }
</div>

@code {
    [Parameter]
    public DashboardTileDto Tile { get; set; } = null!;

    [Inject]
    private NavigationManager NavigationManager { get; set; } = null!;

    private void HandleAction(DashboardActionDto action)
    {
        if (!string.IsNullOrEmpty(action.Target))
        {
            NavigationManager.NavigateTo(action.Target);
        }
    }
}
```

---

## Non-Functional Requirements

### 7.3.1 Performance
 - Dashboard should render essential tiles on startup; prioritize showing critical metrics first
 - Tile data should be generated in parallel when possible to improve perceived performance
 - Tiles with network/database dependencies should be implemented to allow graceful degradation when external calls are slow; providers must ensure such dependencies do not block dashboard render.
 - Failed tiles must not block dashboard display (show an inline error or empty-state in the tile)
 - Caching: Settings cached in memory; dashboard data refreshed on user action or a configurable interval

### 7.3.2 Reliability
- Graceful degradation: If a tile provider fails, display error message in tile; do not halt dashboard
 - Logging: All tile generation attempts logged (start, success, errors)
- Retry: Tile providers may implement retry logic internally
- No transactional guarantees needed (read-only display)

### 7.3.3 Accessibility
- Keyboard navigation: Tab through tiles; Enter to activate actions
- Screen reader support: All statistics and actions have descriptive labels
- Color contrast: 4.5:1 minimum ratio for text
- Focus indicators: Visible focus rings on interactive elements

### 7.3.4 Responsive Design
- 4 columns: 1920px and above
- 3 columns: 1280px to 1919px
- 2 columns: 768px to 1279px
- 1 column: Below 768px (narrow desktop window widths)
- Tiles maintain aspect ratio and readability at all breakpoints

---

## Success Criteria

1. ✅ Dashboard displays at app startup (no navigation required)
2. ✅ All four core tiles (Members, Rehearsals, Events, Finance) render with accurate data
3. ✅ Dashboard renders essential information promptly on startup (progressive rendering and graceful degradation)
4. ✅ Plugins can register custom tiles via IDashboardTileProvider
5. ✅ Tile provider failures do not block dashboard display
6. ✅ Dashboard responsive across all supported screen sizes
7. ✅ All statistics clickable and navigate to relevant pages
8. ✅ WCAG 2.1 AA accessibility compliance

---

## Implementation Phases

### Phase 1: Core Dashboard + Built-in Tiles
- Create Dashboard.razor and DashboardTile.razor components
- Implement IDashboardService
- Implement core tile providers: MemberSummaryTileProvider, RehearsalSummaryTileProvider, EventSummaryTileProvider, FinancialSummaryTileProvider
- Register core tiles in DI container
- Add navigation to dashboard from app startup

### Phase 2: Plugin Architecture
- Implement runtime plugin discovery
- Create plugin registration infrastructure
- Document plugin development guide
- Add example plugin

### Phase 3: Dashboard Enhancements
- Add dashboard refresh button
- Implement per-tile refresh
- Add dashboard customization (hide/show tiles, reorder)
- Add dashboard alerts (overdue fees, upcoming events)

---

## Open Questions

1. Should dashboard support real-time updates (websockets)? → Not for MVP (read-only on startup)
2. Should tiles be customizable/hideable? → Phase 3 enhancement
3. Should plugins be hot-loaded at runtime? → Phase 2 decision point
4. Should dashboard auto-refresh? → Phase 3 enhancement (5-minute interval suggested)
