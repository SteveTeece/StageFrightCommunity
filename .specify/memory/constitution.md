<!--
SYNC IMPACT REPORT
==================
Version Change: 2.5.1 → 2.6.0

Modified Principles:
- §4.1 Vertical Slice Module Architecture → renamed "Layered Architecture with Module
  Slices". Corrected to match the real, implemented structure: one project per
  architectural layer (StageFright.App / Core / Data / UI / Reports /
  Plugins.Contracts) rather than a per-module Domain/Application/Infrastructure/UI
  folder tree. Repositories are explicitly centralized in StageFright.Data (one per
  entity), not module-owned — documented as a deliberate, spec-mandated deviation
  (FR-042) rather than left implicit. Dashboard-tile providers documented as living in
  StageFright.UI (need a Blazor component Type reference), not StageFright.Core.
  Current module list added: Agm, AuditTrail, Dashboard, Events, Finance, Members,
  Rehearsals, Settings.
- §4.2 Dashboard Tile System: added the real DashboardTileSize four-size system
  (OneByOne/OneByTwo/TwoByOne/TwoByTwo → tile-size-1x1/1x2/2x1/2x2 CSS grid classes)
  which previously went undocumented at the constitution level.
- §4.3 Settings System: corrected — built-in tabs (General, Tax, Committee, Event
  Types, Backup & Restore) are hardcoded in SettingsPage.razor, not contributed by
  each module via ISettingsTabProvider. That interface exists solely for
  plugin-contributed tabs and its real members are TabTitle/TabIcon/TabKey/
  DisplayOrder/SettingsComponentType — not the fictional
  GetSettingsAsync/ValidateAsync/SaveAsync method trio the previous text specified.
- §4.6 Navigation Menu System: corrected — the real shell is a fixed vertical sidebar
  (not a top nav bar). MenuItem's real properties are Title/Route/Icon/ShortLabel/
  DisplayOrder/SubItems/BadgeText (no IsActive field — active-state is computed by the
  shell at render time, not stored on the model). Removed the `builder.Services.Scan()`
  (Scrutor-style) auto-discovery code sample — the real app has no assembly-scanning
  DI anywhere; every provider is registered explicitly by name in MauiProgram.cs.
- §5.2 Custom Exceptions: corrected the exception type list to match
  StageFright.Core/Exceptions/ exactly (DataAccessException replaces the fictional
  PersistenceException; PluginLoadException replaces the fictional PluginException;
  ConnectionException removed — folded into DataAccessException; added
  ConcurrencyException, DataIntegrityException, GLBalanceException, ImportException,
  ReconciliationException, which previously went unlisted). Documented the one shared
  constructor shape every custom exception actually uses.
- §7.1 Technology Stack: named the real test frameworks (xUnit v3, bUnit, NSubstitute —
  explicitly not Moq, which is not a dependency anywhere in the solution), the real EF
  Core provider (SQLite via Microsoft.EntityFrameworkCore.Sqlite), and cross-referenced
  the OpenTelemetry requirement already present in §6.

Added Sections:
- None (all changes are corrections/expansions within existing numbered sections)

Removed Sections:
- None

Templates Requiring Updates:
- ✅ .specify/templates/plan-template.md — no update needed; Constitution Check is generic.
- ✅ .specify/templates/spec-template.md — no update needed.
- ✅ .specify/templates/tasks-template.md — no update needed.

Runtime Guidance Docs:
- ✅ CLAUDE.md — already independently documents the real module/layer structure,
  the real Settings/menu/dashboard-tile contracts, and the real exception list; no
  further action needed there as a result of this amendment.
- ✅ docs/ARCHITECTURE.md, docs/SETUP.md, docs/UI_COMPONENT_STYLE_GUIDE.md,
  docs/XML-DOCUMENTATION-STANDARDS.md — already rewritten to match the real
  implementation in a prior pass; this amendment brings the constitution into
  alignment with those docs rather than the other way around.
- ⚠ pending: CONTRIBUTING.md — describes the same superseded vertical-slice/
  per-module folder model this amendment corrects; being updated in the same work
  session as this amendment.

Follow-up TODOs:
- None outstanding.

Version Bump Rationale: MINOR
- Substantively corrects and expands the architectural description in §4.1–§4.6,
  §5.2, and §7.1 to match the actually-implemented system (module structure,
  Settings/menu contract shapes, exception taxonomy, tech stack specifics). No
  governing principle (SOLID, soft-delete, financial immutability, one-class-per-file,
  testing coverage, exception-boundary translation) was removed, weakened, or
  redefined — these sections describe *how* the architecture is realized, and that
  description was inaccurate; fixing it is additive clarification at the scale of a
  MINOR bump, not a PATCH-level wording fix (the vertical-slice folder model was
  materially wrong, not just imprecisely worded) and not a MAJOR breaking change to
  a principle itself.
-->

<!-- Previous Sync Impact Reports:
2.5.0 → 2.5.1: Clarifies §4.7.2: the .razor.css-per-component rule from v2.5.0 is refined to
  reflect that most CSS belongs in the global stylesheet and CSS isolation files are
  conditional. Code-behind (.razor.cs) remains unconditionally mandatory.
2.4.0 → 2.5.0: Added §4.7 (Blazor Component Patterns — code-behind + CSS isolation mandatory)
2.3.0 → 2.4.0: §7.1 §7.2 — BlazorBootstrap added as permitted library
-->

# Spec Kit Constitution  
*A guiding document for clean, modular, extensible software development*

**Version**: 2.6.0
**Ratification Date**: 2025-01-01
**Last Amended**: 2026-08-22

---

## 1. Purpose
This constitution defines the principles, expectations, and architectural standards that govern all specifications, plans, and implementations within this project.

Its goal is to ensure that every contribution—human or AI‑assisted—results in software that is:

- Clean and readable  
- Modular and maintainable  
- Extensible through well‑defined plug‑in boundaries  
- Consistent with SOLID design principles  
- Built with clear separation of concerns  

All contributors operate under this constitution when creating or modifying specifications.

---

## 2. Vision
The project aims to deliver a clean, modern, modular desktop application that reduces administrative overhead for community performing arts groups.  
The architectural vision includes:

- A modular plug‑in‑friendly architecture  
- Clean, maintainable code  
- Accurate and trustworthy financial and attendance tracking  
- A foundation that supports long‑term evolution without unnecessary complexity  

---

## 3. Core Engineering Principles

### 3.1 Clean Code
All specifications and generated code must prioritize clarity and simplicity.

- Code should be self‑explanatory or supported by minimal, meaningful comments.  
- Naming must be descriptive and domain‑aligned.  
- Functions and classes should be small, focused, and purposeful.  
- Avoid cleverness; prefer clarity.  
- Consistency across the codebase is mandatory.

### 3.2 SOLID Design Principles
All architectural decisions and specifications must adhere to the SOLID principles:

- **Single Responsibility:** Each module, class, or function must have one reason to change.  
- **Open/Closed:** Systems should be open for extension but closed for modification.  
- **Liskov Substitution:** Derived types must be substitutable for their base types.  
- **Interface Segregation:** Prefer small, specific interfaces over large, general ones.  
- **Dependency Inversion:** High‑level modules must not depend on low‑level modules; both depend on abstractions.

#### 3.2.1 Single Responsibility Principle – File Organization (NON-NEGOTIABLE)

Adherence to the Single Responsibility Principle through file organization is **mandatory and non-negotiable**.

**Mandatory Rules**:

- **One Class Per File**: Every C# class, interface, record, struct, or enum must be defined in its own dedicated file. No file may contain more than one class/interface/record/struct/enum definition.
  - **Exception**: Nested types (private nested classes used only within a parent class) may remain in the same file as their parent if they are non-public and serve a single, tightly-scoped purpose within that class.
  - **Exception**: Compiler-generated nested types (e.g., backing fields, auto-property implementations) are not subject to this rule.
  
- **File Naming Convention**: File name must exactly match the class/interface/record name it contains.
  - Example: Class `MemberService` → file `MemberService.cs`
  - Example: Interface `IMemberRepository` → file `IMemberRepository.cs`
  - Example: Record `MemberDto` → file `MemberDto.cs`
  
- **Rationale**: Each file containing a single, well-defined type:
  - Enforces the Single Responsibility Principle at file level
  - Improves code discoverability and navigation
  - Reduces cognitive load when reading and maintaining code
  - Simplifies version control (fewer merge conflicts on multi-type files)
  - Enables IDE features (Go to Definition, refactoring) to work intuitively
  
- **Consequences of Violation**:
  - Code review **must reject** any pull request with multiple classes in a single file
  - Refactoring tasks must be created to split violating files before merge
  - This is a **blocking** requirement; no exceptions permitted except those explicitly listed above
  
- **Verification**: Automated tooling (analyzers, linters, CI pipeline checks) should be configured to enforce this rule. Manual review must verify compliance at every code review stage.

### 3.3 Separation of Concerns
Specifications must enforce clear boundaries between:

- Domain logic  
- Application logic  
- Infrastructure concerns  
- UI or presentation layers  
- Cross‑cutting concerns (logging, caching, telemetry, etc.)

No specification may introduce coupling across these boundaries without explicit justification.

### 3.4 Soft Delete Pattern
All specifications must enforce soft delete pattern for data preservation:

- **Soft-Delete Required:** All application data MUST implement and use the soft-delete pattern. Physical hard deletes of application data are prohibited unless explicitly permitted by a documented exception below.
- **Soft Delete Fields:** All entities must include soft delete tracking fields:  
  - `IsDeleted` (boolean) — Indicates if the record is deleted  
  - `DeletedAt` (DateTime?) — Timestamp when record was deleted  
  - `DeletedBy` (string, optional) — User or system identifier who performed deletion  
- **Query Filtering:** All queries must automatically filter out soft-deleted records unless explicitly requesting deleted records.  
- **Restore Capability:** System must provide functionality to restore (undelete) soft-deleted records.  
- **Cascade Soft Delete:** When a parent entity is soft-deleted, related child entities should also be soft-deleted if appropriate.  
- **Audit Trail:** Soft delete operations must be logged with full context (entity type, ID, user, timestamp).  
- **Data Integrity:** Soft-deleted records must remain in the database to preserve referential integrity and historical data.  
- **UI Considerations:**  
  - Default views show only active (non-deleted) records  
  - Provide optional "Show Archived" or "Show Deleted" toggle  
  - Archive/trash views for reviewing and restoring deleted items  
- **Validation:** Prevent operations on soft-deleted entities (e.g., updating a deleted member) unless explicitly restoring.
- **Exception — Error Logs:** Error logs and structured log records (for example, Serilog sink entries or external log store records) MAY be hard-deleted under a documented retention policy (see Logging & Observability §6). This exception applies only to log records and does NOT permit hard deletion of transactional, audit, or financial entity data. Ensure audit trails for financial and member data are preserved in non-log stores.

- **Exception — Financial Records:** Financial transaction entities (for example: `Income`, `Expense`, `Payment`) ARE EXPLICITLY EXEMPT from the soft-delete pattern described above. Financial records MUST be treated as immutable, permanent records and MUST NOT be soft-deleted or hard-deleted. See §3.5 (Member and Financial Data Preservation) for full rules on financial immutability and preservation. This exception exists to preserve canonical auditability and aligns with the project's financial data preservation principles.

### 3.5 Member and Financial Data Preservation
Special rules apply to members and financial data to ensure permanent preservation:

- **Members Deletion Policy:**  
  - Members MUST be soft-deleted and MUST NOT be hard-deleted.  
  - Member records must be permanently preserved to maintain historical financial accuracy.  
  - Inactive members should be filtered from default views but remain queryable for reports.  
  - Provide "Show Inactive Members" toggle in member views.  
- **Financial Data Immutability:**  
  - All financial records (Income, Expense, Payment) must NEVER be deleted (soft or hard).  
  - Financial transactions must be immutable once created to preserve audit trail.  
  - To correct errors, create reversing transactions rather than deleting or editing.  
  - Member balance history must be preserved indefinitely.  
  - Fee application records must be retained permanently.  
- **Status-Based Management:**  
  - Members have `Status` field with values: "Active", "Inactive"  
  - Active members appear in default views and can participate in events.  
  - Inactive members are hidden by default but accessible via archive views.  
  - Inactive members retain all historical data (fees, payments, attendance).  
- **Reactivation:**  
  - Inactive members can be reactivated by changing `Status` back to "Active".  
  - All historical data remains intact during reactivation.  
  - Historical outstanding balances remain preserved for audit and reporting.  
  - On reactivation, historical balances are not actively owed by default.  
  - If legacy debt must be reactivated, it must be reinstated through an explicit,
    auditable financial adjustment workflow (e.g., reversing transaction).  

### 3.6 Financial Corrections Pattern
All financial transactions must use a reversing transaction pattern for corrections:

- **No Direct Edits:** Original financial records (Fees, Payments, GL Transactions) MUST NEVER be edited after creation.
- **No Deletions:** Original financial records MUST NEVER be deleted (soft or hard).
- **Corrections via Reversals:** To correct errors or reverse operational impacts, create new reversing GL transaction pairs:
  - Reversing transactions are new GL entries with opposite signs (debit becomes credit, credit becomes debit)
  - Link reversing transactions to original transaction via audit trail for traceability
  - Example: If $100 Income fee created in error, create reversing GL pair: Debit $100 (Income), Credit $100 (Receivable) to net the original impact
  - Example: If member attendance cleared, create GL reversals to negate fee impact while preserving original Fee record immutability
- **Operational vs. Error Reversals:** Reversals are created for both:
  - **Error Corrections:** User or system corrected a mistake in a prior transaction
  - **Operational Reversals:** Normal business process (e.g., attendance flag cleared, fee reconsidered) requiring financial impact reversal
- **Audit Trail:** All reversals must include explicit reference to what was reversed and why (via GL Transaction notes/description field)
- **Balance Preservation:** Original balance history remains intact; reversals offset impacts in current calculations but do not rewrite history

---

## 4. Architectural Identity
All specifications and implementations must follow these architectural principles:

- Clean, intention‑revealing code  
- SOLID design  
- Strict separation of concerns  
- Layered architecture with module slices inside the Core layer (see §4.1)  
- Dashboard tile system for feature exposure (see §4.2)  
- Settings system with a hardcoded core tab set plus plugin-contributed tabs (see §4.3)  
- Navigation menu system with module-defined items rendered in a fixed sidebar (see §4.6)  
- Plug‑in architecture with extension points (see §8)  
- Composition over inheritance  
- Testability of all core logic  
- Test isolation from external dependencies  
- Reusable UI components in a separate control library  
- Observability through Serilog + OpenTelemetry  
- Mandatory custom exceptions at architectural boundaries  
- Exhaustive test coverage for all reachable code paths  
- Soft delete pattern for all data removal operations  
- Members and financial data are NEVER HARD deleted  
- Clean, simple, modern UI design with minimal whitespace (see §4.4)  

### 4.1 Layered Architecture with Module Slices

The system is a **layered solution**: one project per architectural layer, with each business capability ("module") organized as a folder *within* the Core layer rather than as its own top-level Domain/Application/Infrastructure/UI tree. This is a deliberate refinement of pure vertical-slice architecture, made for a desktop MAUI Blazor Hybrid application with a shared SQLite database.

**Layer/Project Requirements:**

- **One Project Per Layer**: `StageFright.App` (MAUI Blazor Hybrid host — composition root only, zero application logic), `StageFright.Core` (domain entities, enums, custom exceptions, service/repository contracts, module services), `StageFright.Data` (centralized DAL: `DbContext`, migrations, one repository per entity, unit of work), `StageFright.Plugins.Contracts` (extension-point interfaces, no project dependencies), `StageFright.Reports` (report pipeline), `StageFright.UI` (all Blazor UI as a Razor class library).
- **Module Folders Inside Core**: Each business capability gets a folder at `StageFright.Core/Modules/<ModuleName>/` containing that module's services and request/response DTOs. Current modules: `Agm`, `AuditTrail`, `Dashboard`, `Events`, `Finance`, `Members`, `Rehearsals`, `Settings`.
- **No MediaTr or CQRS**: Modules must NOT use MediaTr for command/query dispatch or implement CQRS patterns. Instead, use:  
  - Direct service injection and method calls  
  - Dependency-injected services for business logic  
  - Clear, explicit request/response models  
  - Standard repository and service patterns  
- **Repositories Are Centralized, Not Module-Owned**: Unlike pure vertical-slice architecture, repositories do NOT live inside each module's folder. All repositories live in `StageFright.Data/Repositories/` — one repository class per entity, implementing an interface declared in `StageFright.Core/Contracts/`. This centralization is intentional: it keeps EF Core configuration, migrations, and the `DbContext` in one place for a single shared SQLite database, and is a documented, permanent deviation from the strict per-module ownership described in earlier drafts of this constitution — not a violation to be refactored away.
- **Menu-Item Providers Live With Their Module**: Each module that contributes navigation defines its `IMenuItemProvider` implementation inside its own `StageFright.Core/Modules/<ModuleName>/` folder.
- **Dashboard-Tile Providers Live in the UI Layer**: `IDashboardTileProvider` implementations live in `StageFright.UI/Modules/<ModuleName>/`, not in `StageFright.Core` — a tile provider must reference a Blazor component `Type` for its tile content (`TileComponentType`), and `StageFright.Core` intentionally has no reference to `StageFright.UI`.
- **Contracts Are Centralized in Core**: Service and repository interfaces (`I<Service>`, `I<Entity>Repository`) live in `StageFright.Core/Contracts/`, grouped together rather than colocated with each implementation — this keeps the "one type per file" rule intact while giving developers one place to scan every published contract.
- **Module Ownership**: Each module owns its own:  
  - Application services, request/response DTOs, and menu-item provider (in `StageFright.Core/Modules/<ModuleName>/`)  
  - Its repository/repositories (in `StageFright.Data/Repositories/`, implementing that module's `StageFright.Core/Contracts/` interfaces)  
  - Its UI pages/components and dashboard-tile provider (in `StageFright.UI/Pages/<ModuleName>/` and `StageFright.UI/Modules/<ModuleName>/`)  
  - Its unit and integration tests, in the matching `tests/StageFright.*.Tests/` project  
- **No Cross-Module Dependencies**: Modules must NOT import from a sibling module's concrete service class or reach into `StageFright.Data` repositories directly from `StageFright.UI`. Modules communicate through:  
  - Dependency injection of published interfaces from `StageFright.Core/Contracts/`  
  - Shared contracts defined at the Core layer  
- **Testing Isolation**: Each module's tests must be independently executable and isolated from other modules.  

### 4.2 Dashboard Tile System

The dashboard is the primary user-facing interface for feature discovery and interaction. Each module exposes its functionality through dashboard tiles. Tiles are extensible, composable, and support rich content.

**Tile Requirements:**

- **Tile Definition**: Each module MUST define one or more dashboard tiles through an implementation of `IDashboardTileProvider` (`StageFright.Plugins.Contracts`), located in `StageFright.UI/Modules/<ModuleName>/` (see §4.1).  
- **Tile Content**: Tiles MAY contain:  
  - Summary information or metrics (e.g., count of active members, outstanding fees)  
  - Charts and graphs (e.g., revenue trends, attendance distribution)  
  - Quick-action buttons (e.g., "Add Member", "Record Payment")  
  - Recent activity feeds (e.g., last 5 scheduled events)  
  - Status indicators  
- **Tile Characteristics**:  
  - Self-contained rendering (tile handles its own data loading and rendering)  
  - No inter-tile dependencies  
  - Consistent sizing and layout within the dashboard grid (see Tile Sizing below)  
  - Responsive to user interactions without leaving the dashboard  
- **Tile Sizing**: Each provider opts into one of four grid footprints via `DashboardTileSize` (`OneByOne` default = 1×1, `OneByTwo` = 2 cols × 1 row, `TwoByOne` = 1 col × 2 rows, `TwoByTwo` = 2×2), overriding `IDashboardTileProvider.TileSize`. The dashboard's CSS grid maps each value to a `tile-size-1x1`/`1x2`/`2x1`/`2x2` class; resizing a tile requires only the provider's `TileSize` override plus its own inner layout, never a change to the shared grid CSS.  
- **Multiple Tiles per Module**: A module MAY define multiple tiles to represent different aspects (e.g., "Members Overview" and "Member Onboarding Quick Action").  
- **Tile Registration**: Tiles are registered with DI (explicitly, by name — see §4.6's registration note); no hardcoding of tile instances outside the DI registration itself.  
- **Failure Isolation**: If a tile fails to load or render, it must gracefully degrade without breaking the entire dashboard.  

### 4.3 Settings System

The Settings page is a core application feature where configuration and preferences are managed. The settings architecture uses a **tabbed interface**: the core application ships a fixed, hardcoded set of built-in tabs, and external plugins MAY append additional tabs through a published contract.

**Settings Architecture:**

- **Settings Page**: A base application page with tabbed interface at `/settings`, supporting deep-linking to a specific tab via a tab key in the query string.
- **Built-in Tabs Are Hardcoded, Not Module-Contributed**: The core application's own tabs (organization/application settings, tax configuration, committee configuration, event types, backup & restore) are implemented directly as components hosted by the Settings page — they are NOT registered through the plugin tab contract described below. This is a deliberate simplification: these tabs are permanent, ship with every install, and gain nothing from an indirection layer only external plugins need.
- **`ISettingsTabProvider` Is For Plugin-Contributed Tabs Only**: The Settings page separately discovers all registered `ISettingsTabProvider` implementations via DI and renders their tabs after the built-in ones. A provider whose tab key collides with an existing one MUST be skipped with a logged warning, not allowed to silently overwrite or crash the page.
- **Tab Isolation**: Each tab manages its own UI, validation, and persistence internally within its own Blazor component — the provider contract does not itself carry `GetSettingsAsync`/`ValidateAsync`/`SaveAsync` methods; it only identifies and locates the tab.

**`ISettingsTabProvider` Contract** (the real, minimal shape — persistence and validation are the tab component's own responsibility, not the provider's):

```csharp
public interface ISettingsTabProvider
{
    string TabTitle { get; }              // Display title shown in the tab strip
    string TabIcon { get; }               // Icon name/CSS class for the tab
    string TabKey { get; }                // Unique key for deep-linking: /settings?tab={TabKey}
    int DisplayOrder { get; }             // Core tabs: 0–99. Plugin tabs: 100+.
    Type SettingsComponentType { get; }   // Blazor component that owns the tab's content,
                                           // validation, and save/cancel behavior
}
```

**Application Settings** (Core, part of the built-in General tab, not a module contribution):

- **Organization Information**:
  - Organization/Group Name
  - Contact information (if applicable)
- **Financial Configuration**:
  - Annual Membership Fee amount
  - Rehearsal/Event Fee amount
  - Currency (if multi-currency support needed)
- **Membership Rules**:
  - Membership Renewal Due Date (month and day, e.g., "September 1")
  - Grace period for renewal (days before/after due date)
- **Fee Application Periods**:
  - Fee renewal frequency (annual, per-season, etc.)
  - Auto-renewal configuration

**Settings Page Layout**:

```
┌─────────────────────────────────────────────────┐
│  Settings                                       │
├─────────────────────────────────────────────────┤
│ [General] [Tax] [Committee] [Event Types] [...] │
├─────────────────────────────────────────────────┤
│                                                 │
│  Active Tab Content                            │
│  - Organization Name                           │
│  - Annual Fee                                  │
│  - Rehearsal Fee                               │
│  - Membership Renewal Due Date                 │
│                                                 │
│  [Cancel] [Save Settings]                      │
│                                                 │
└─────────────────────────────────────────────────┘
```

### 4.4 UI Design Principles

The user interface must embody simplicity, clarity, and modern design principles. Every screen and component must prioritize usability and visual efficiency.

**UI Design Standards:**

- **Clean and Simple**: Interfaces must be free of unnecessary visual clutter. Remove elements that do not directly contribute to user goals.  
- **Minimal Whitespace**: Use whitespace purposefully but economically. Compact layouts should be the default; information density must be optimized without sacrificing readability.  
- **Modern Aesthetics**: Use contemporary design patterns:  
  - Subtle, professional color palettes  
  - Smooth transitions and animations (where purposeful)  
  - Consistent typography and spacing scales  
  - Clear visual hierarchy  
- **Component Consistency**: All UI components must follow a unified design language across the application.  
- **Accessibility**: Clean design must not sacrifice accessibility; all interactive elements must be keyboard-navigable and screen-reader compatible.  
- **Responsive and Performant**: UI must respond immediately to user input; avoid blocking operations on the UI thread.  

### 4.6 Navigation Menu System

The application provides a hierarchical navigation menu where each module defines its own menu items and sub-items. The menu is rendered as a **fixed vertical sidebar**, is modular and extensible, and always displays Settings as the final item.

**Menu Architecture**:

- **Rendering Surface**: The menu renders as a fixed-position vertical sidebar (not a top nav bar), occupying a constant width down the left edge of the application shell.
- **Menu Items**: Each module can define primary menu items and optional sub-items
- **Optional Icons**: Menu items may include icons to visually represent functionality
- **Module Order**: Modules contribute menu items in a customizable order
- **Settings Always Last**: The Settings menu item is reserved for the core application and appears last
- **Sub-menus**: Each menu item may have child items, rendered as an expandable/collapsible group that auto-expands while a descendant route is active

**Menu Item Structure** (the real contract — note there is no `IsActive` field; which item is "active" is computed by the rendering shell from the current route at render time, not stored on the model):

```csharp
public interface IMenuItemProvider
{
    IReadOnlyList<MenuItem> GetMenuItems();
    string ModuleName { get; }
    int DisplayOrder { get; }
}

public class MenuItem
{
    public string Title { get; set; }              // Display title
    public string Route { get; set; }               // Navigation route (e.g., "/members/list")
    public string? Icon { get; set; }                // Optional icon name/CSS class
    public string? ShortLabel { get; set; }          // Optional short label for compact surfaces
    public int DisplayOrder { get; set; }            // Order within module
    public List<MenuItem> SubItems { get; set; } = new();  // Optional sub-menu items
    public string? BadgeText { get; set; }           // Optional badge (e.g., count)
}

// Example: Members module menu
public class MembersMenuItemProvider : IMenuItemProvider
{
    public string ModuleName => "Members";
    public int DisplayOrder => 1;

    public IReadOnlyList<MenuItem> GetMenuItems()
    {
        return new List<MenuItem>
        {
            new()
            {
                Title = "Members",
                Route = "/members",
                Icon = "users",
                DisplayOrder = 1
            }
        };
    }
}
```

**Menu Rendering Order**:

```
├── Dashboard (Core)                        // Always first
├── [Module 1 items by DisplayOrder]        // e.g., Members (order 1)
├── [Module 2 items by DisplayOrder]        // e.g., Events (order 2)
├── [Module 3 items by DisplayOrder]        // e.g., Finance (order 3)
├── ... [other modules] ...
├── [Plugin-contributed items]              // Discovered at runtime
└── Settings (Core)                         // Always last
```

**Menu Item Characteristics**:

- **Title**: User-facing text displayed in the menu
- **Route**: Target URL when menu item is clicked
- **Icon**: Optional icon name for visual identification (from icon set)
- **ShortLabel**: Optional short label for compact navigation surfaces
- **DisplayOrder**: Order within the module (lower numbers first)
- **SubItems**: Optional child menu items for grouping related features
- **BadgeText**: Optional notification badge (e.g., "5" for pending items)

**Menu Registration**:

`IMenuItemProvider` implementations are registered **explicitly by name** in the application's DI composition root, one `services.AddSingleton<IMenuItemProvider, XProvider>()` call per provider — there is no assembly-scanning/auto-discovery mechanism (no Scrutor `.Scan()`, no reflection-based registration) for core, in-solution providers. Explicit registration is a deliberate choice: it keeps every registered provider visible at a single call site and avoids surprising behavior from reflection picking up an unintended type. Only plugin assemblies discovered at runtime from the `Plugins/` directory are registered reflectively (see §8), and that reflection is scoped to the five plugin-contract interfaces only.

```csharp
// StageFright.Core/Modules/Members/DependencyInjection or MauiProgram.cs equivalent —
// explicit, one line per provider; NOT an assembly scan.
services.AddSingleton<IMenuItemProvider, MembersMenuItemProvider>();
services.AddSingleton<IMenuItemProvider, EventsMenuItemProvider>();
services.AddSingleton<IMenuItemProvider, FinanceMenuItemProvider>();
```

**Menu Component Usage**:

The application's shell layout component discovers and renders all menu items as a fixed sidebar:

```razor
@* Layout/ShellLayout.razor *@
@inject IEnumerable<IMenuItemProvider> MenuProviders

<nav class="shell-sidebar" role="navigation" aria-label="Main navigation">
    <ul class="sidebar-list">
        @* Dashboard always first *@
        <li class="sidebar-item">
            <a href="/dashboard" class="sidebar-link">
                <span class="nav-icon" aria-hidden="true"></span>
                <span class="sidebar-label">Dashboard</span>
            </a>
        </li>

        @* Module menu items sorted by DisplayOrder *@
        @foreach (var provider in MenuProviders.OrderBy(p => p.DisplayOrder))
        {
            @foreach (var item in provider.GetMenuItems().OrderBy(m => m.DisplayOrder))
            {
                <li class="sidebar-item">
                    <a href="@item.Route" class="sidebar-link @(IsActive(item.Route) ? "active" : "")">
                        <span class="sidebar-label">@item.Title</span>
                        @if (!string.IsNullOrEmpty(item.BadgeText))
                        {
                            <span class="sidebar-badge">@item.BadgeText</span>
                        }
                    </a>
                </li>
            }
        }

        @* Settings always last *@
        <li class="sidebar-item">
            <a href="/settings" class="sidebar-link">
                <span class="sidebar-label">Settings</span>
            </a>
        </li>
    </ul>
</nav>
```

**Menu Isolation Rules**:

- Modules MUST NOT depend on menu items from other modules
- Menu items MUST be independent and self-contained
- Route conflicts MUST be avoided (each module owns its route prefix)
- Menu item active-state MUST be computed dynamically from the current route at render time, not stored on the `MenuItem` model

### 4.5 Code Organization and File Structure

The physical organization of source files directly reflects the Single Responsibility Principle and architectural clarity. All code must follow strict file organization rules to ensure maintainability, discoverability, and consistency.

**Mandatory File Organization Rules**:

- **One Class Per File (Enforced)**: Every public class, interface, record, struct, or enum occupies its own dedicated file. This is **non-negotiable** (see §3.2.1 for exceptions and consequences).
  
- **File Naming**: File names must match the type they contain exactly:
  - `public class MemberService` → file `MemberService.cs`
  - `public interface IMemberRepository` → file `IMemberRepository.cs`
  - `public record MemberDto` → file `MemberDto.cs`
  - `public enum MemberStatus` → file `MemberStatus.cs`
  
- **Folder Structure by Responsibility**: Files are organized by architectural layer/responsibility, per §4.1 — not by a per-module Domain/Application/Infrastructure/UI tree:
  ```
  StageFright.Core/
  ├── Entities/                    # Domain entities
  │   └── Member.cs
  ├── Enums/                       # Shared enums
  │   └── MemberStatus.cs
  ├── Exceptions/                  # Custom exception hierarchy (see §5.2)
  │   └── ValidationException.cs
  ├── Contracts/                   # Service/repository interfaces, all modules
  │   ├── IMemberService.cs
  │   └── IMemberRepository.cs
  └── Modules/
      └── Members/                 # This module's services + DTOs + menu provider
          ├── MemberService.cs
          ├── CreateMemberRequest.cs
          └── MemberMenuItemProvider.cs

  StageFright.Data/Repositories/
  └── MemberRepository.cs          # Centralized — implements IMemberRepository

  StageFright.UI/
  ├── Pages/Members/
  │   ├── MemberList.razor
  │   ├── MemberList.razor.cs
  │   ├── MemberDetail.razor
  │   └── MemberDetail.razor.cs
  └── Modules/Members/
      └── MembersDashboardTileProvider.cs

  tests/StageFright.Core.Tests/Modules/Members/
  └── MemberServiceTests.cs
  ```

- **File Responsibility**: Each file must contain only the types and logic necessary to fulfill one specific responsibility:
  - A service class and its interface should be in separate files (interface in `Contracts/`, implementation in `Modules/<Name>/`)
  - DTOs should be in separate files from domain entities
  - Request/response objects should be in separate files
  - Enums and value objects each get their own file
  - Extensions should be in separate files (e.g., `MemberRepositoryExtensions.cs`)
  
- **Blazor Components (MANDATORY — see §4.7)**:
  - `.razor` file: markup, `@page` directives, `@inject` directives, and component references ONLY
  - `.razor.cs` code-behind file: ALL C# logic, event handlers, lifecycle methods, and field declarations
  - `.razor.css` CSS isolation file: component-specific scoped styles ONLY — created when needed,
    not required on every component (see §4.7.2)
  - `@code { }` blocks inside `.razor` files are PROHIBITED
  - Every component MUST have paired `.razor` and `.razor.cs` files; `.razor.css` is added only
    when the component requires styles not suited for the global stylesheet
  
- **Verification and Enforcement**:
  - Code review **must reject** any PR with multiple types in a single file
  - CI/CD pipeline should run analyzers to detect multi-type files
  - Refactoring issues must be created for any violations found
  - No merge approval until compliance is achieved

### 4.7 Blazor Component Patterns (MANDATORY)

All Blazor components in the UI project MUST follow two mandatory structural patterns.
These rules are non-negotiable and apply to every `.razor` file without exception.

#### 4.7.1 Code-Behind Pattern

Every Blazor component MUST separate its markup from its C# logic using a paired code-behind
file. This is a binding rule — no inline `@code` blocks are permitted in production components.

**Mandatory Rules**:

- Every `.razor` component file MUST have a corresponding `.razor.cs` partial class file
  in the same directory.
- The `.razor` file MUST contain ONLY:
  - `@page` route directives
  - `@inject` dependency declarations
  - `@using` namespace imports
  - HTML/Razor markup and component references
- ALL of the following MUST live exclusively in the `.razor.cs` file:
  - Field and property declarations
  - Lifecycle overrides (`OnInitializedAsync`, `OnAfterRenderAsync`, `OnParametersSetAsync`, etc.)
  - Event handlers and callbacks
  - Computed values and helper methods
  - `[Parameter]`, `[CascadingParameter]`, `[Inject]` attributes
- `@code { }` blocks in `.razor` files are **PROHIBITED** — no exceptions.
- **Rationale**: Enforces the Single Responsibility Principle (§3.2.1) at the component
  level; separates structural markup from behavioral logic; enables better IDE tooling,
  refactoring support, and independent unit testing of component logic.

#### 4.7.2 CSS Isolation Pattern

The global stylesheet is the primary home for all application CSS. CSS isolation files
(`.razor.css`) are used only for styles that are genuinely scoped to a single component
and are not appropriate for global or shared stylesheets.

**Mandatory Rules**:

- The global stylesheet (`StageFright.App/wwwroot/app.css`) MUST be the default
  location for all CSS — layout, typography, utility classes, theme variables, Bootstrap
  overrides, and styles shared across two or more components.
- A `.razor.css` file MUST be created alongside a component only when that component
  requires styles that are unique to its own rendering and cannot be expressed cleanly
  in the global stylesheet without introducing overly specific selectors.
- Inline `<style>` tags inside `.razor` files are **PROHIBITED** regardless of scope.
- **Rationale**: Centralising the majority of CSS in the global stylesheet keeps styling
  maintainable and consistent. CSS isolation files are a precision tool for genuinely
  component-scoped concerns, not a blanket requirement.

**When to create a `.razor.css` file**:

- The component has unique structural or visual styles not found elsewhere in the application.
- Applying the styles globally would require class-name specificity hacks or would risk
  unintended side-effects on other components.
- The styles are tightly coupled to the component's internal DOM structure.

**When NOT to create a `.razor.css` file**:

- General layout, spacing, or typography — use the global stylesheet.
- Bootstrap utility-class overrides — use the global stylesheet.
- Styles that will be reused across two or more components — use the global stylesheet.
- The component has no custom styles at all — no `.razor.css` file needed.

**Summary — Required Files**:

Every Blazor component MUST have the following mandatory pairing, with CSS isolation
added conditionally:

```
ComponentName.razor        ← Markup and directives only             (REQUIRED)
ComponentName.razor.cs     ← All C# logic (partial class)          (REQUIRED)
ComponentName.razor.css    ← Genuinely component-scoped styles only (CONDITIONAL)
```

**Consequences of Violation**:

- Code review MUST reject any PR that introduces a `@code { }` block in a `.razor` file.
- Code review MUST reject any PR that adds a `.razor` component without a paired `.razor.cs`
  file.
- Code review MUST reject any PR that uses inline `<style>` tags in a `.razor` file.
- These are **BLOCKING** requirements; no exceptions are permitted.

**Verification**:

- Code review checklist MUST include verification that every new component has a paired
  `.razor.cs` file and that no `@code { }` blocks appear in `.razor` files.
- For components that include a `.razor.css` file, reviewers MUST confirm the styles
  genuinely belong there and are not candidates for the global stylesheet.

---

## 5. Error Handling and Custom Exceptions

### 5.1 Error Handling Rules
- All persistence operations must be wrapped in try-catch blocks.  
- Never swallow exceptions; rethrow with context when needed.  
- Provide graceful degradation and user‑friendly error messages.  
- Any exception crossing Domain, Application, Infrastructure, or UI boundaries MUST
  be represented as a project-defined custom exception type.  

### 5.2 Custom Exceptions
Custom exceptions are mandatory for domain and application behavior.
The project-defined custom exception types (`StageFright.Core/Exceptions/`) are:

- `DataAccessException` — unexpected database/persistence errors raised at the DAL boundary
- `EntityNotFoundException`  
- `DuplicateEntityException` — e.g. a `UNIQUE` constraint violation
- `ConcurrencyException` — e.g. `DbUpdateConcurrencyException`
- `DataIntegrityException`  
- `ValidationException` — domain validation rule violations
- `GLBalanceException` — a GL transaction pair fails to balance (Σdebits ≠ Σcredits); MUST
  trigger an immediate rollback of the enclosing unit-of-work transaction
- `ReconciliationException`  
- `ImportException`  
- `PluginLoadException` — a plugin assembly fails to load or register

Every custom exception type MUST share one consistent constructor shape:
`(string message, string entityType, string operationContext, Guid? entityId = null, Exception? innerException = null)`,
with `Guid? EntityId`, `DateTime Timestamp` (UTC, set at construction), and `Guid CorrelationId`
(for cross-layer tracing) as built-in properties — individual exception types MUST NOT invent
their own, inconsistent constructor signature or omit these properties.

Raw framework exceptions (for example `DbException`, `DbUpdateException`, `IOException`,
`InvalidOperationException`) MUST NOT leak across architectural boundaries.
They must be translated to one of the approved custom exceptions above with preserved inner
exception context.

All caught exceptions must be logged using Serilog.

### 5.3 Exception Boundary Translation
- Infrastructure adapters MUST translate dependency-specific failures into project
  custom exceptions before returning control to Application or UI layers.
- Application services MUST raise domain/application custom exceptions for business
  rule violations instead of generic exceptions.
- UI layers MUST handle custom exceptions explicitly and map them to deterministic,
  user-friendly states.
- Specifications, plans, and tasks MUST include an exception taxonomy and explicit
  exception-flow handling for each feature.

---

## 6. Logging and Observability

### 6.1 Hybrid Logging Model
- Serilog for structured logging  
- OpenTelemetry for distributed tracing and metrics  

### 6.2 Logging Requirements
- Use structured logging with semantic properties  
- Configure multiple sinks as needed  
- Never log sensitive data  
- Include correlation IDs for unified observability  

### 6.3 OpenTelemetry Requirements
- Instrument critical paths with spans  
- Export metrics for business and technical KPIs  
- Integrate Serilog logs with OpenTelemetry traces  

---

## 7. Architectural Model

### 7.1 Technology Stack
- **Framework:** .NET MAUI with Blazor Hybrid  
- **UI:** Blazor components, including free Radzen Blazor components (`Radzen.Blazor`) and
  BlazorBootstrap (`Blazor.Bootstrap`) for charting and Bootstrap-based UI composition  
- **Language:** C# 14  
- **Platforms:** Windows desktop and macOS desktop  
- **Hosting Model:** Blazor Hybrid  
- **Data Access:** Entity Framework Core against SQLite (`Microsoft.EntityFrameworkCore.Sqlite`) —
  a single shared database file, with plugin-owned schemas merged in via `IDataAccessProvider`
  (see §8)  
- **Observability:** Serilog (structured logging) + OpenTelemetry (tracing and runtime metrics) —
  see §6  
- **Testing:** xUnit v3 for unit/integration tests, bUnit for Blazor component tests, and
  NSubstitute for mocking. Moq MUST NOT be introduced as a dependency — NSubstitute is the
  established, exclusive mocking library for this project  

### 7.2 Architecture Requirements
- UI components in a separate class library  
- Use MAUI DI container  
- Platform-specific code via abstractions or conditional compilation  
- CSS isolation (`.razor.css`) MUST be used for genuinely component-scoped styles; most CSS
  belongs in the global stylesheet (`StageFright.App/wwwroot/app.css`) — inline `<style>` tags
  are prohibited (see §4.7.2)
- Reusable, testable UI components  
- Free Radzen components (`Radzen.Blazor`) are permitted for UI composition when implemented
  in Blazor components and backed by C# handlers/services  
- BlazorBootstrap components (`Blazor.Bootstrap`) are permitted for charting and
  Bootstrap-based UI elements not covered by Radzen. All usage MUST remain within Blazor
  C# components; no custom JavaScript is permitted (see §7.3)  

### 7.3 Prohibited
- Custom JavaScript files or business logic implemented in JavaScript  
- Platform-specific UI frameworks outside MAUI/Blazor  
- Web-hosted Blazor  

---

## 8. Plug‑In Architecture

### 8.1 Architecture Model
- Core system defines contracts and extension points (`StageFright.Plugins.Contracts`):
  `IDashboardTileProvider`, `ISettingsTabProvider`, `IMenuItemProvider`, `IReportProvider`,
  `IDataAccessProvider`  
- Plug‑ins implement contracts and are discovered at runtime from a `Plugins/` directory,
  each loaded into its own `AssemblyLoadContext` and reflectively registered against the
  five contract interfaces above  
- Plug‑ins must be isolated and independently testable  
- No plug‑in may depend on another unless explicitly defined  
- A plugin that fails to load, or a provider that throws/duplicates an identifier, MUST be
  caught, logged, and skipped — it must never block application startup or other providers  

### 8.2 MVP Modules vs. Extensible Plugins

The application distinguishes between MVP (Minimum Viable Product) modules and future extensible plugins:

**MVP Modules**:
- Core features included directly in the main application (not in a separate plugins folder)  
- MVP modules follow the layered architecture with module slices described in §4.1, with
  defined dashboard tiles (§4.2)  
- MVP modules live inside `StageFright.Core/Modules/<ModuleName>/` (services/DTOs/menu
  provider), `StageFright.Data/Repositories/` (their repositories), and
  `StageFright.UI/Pages/<ModuleName>/` + `StageFright.UI/Modules/<ModuleName>/` (their UI and
  dashboard tiles) — see §4.1 and §4.5  
- Examples: Members, Finance, Events, Rehearsals, Agm, AuditTrail, Dashboard, Settings  
- MVP modules do NOT require external loading or registration mechanisms — they are
  registered explicitly, by name, in the application's DI composition root  
- MVP modules are part of the shipped application and follow the standard layered/module-slice pattern  

**Extensible Plugins**:
- Third-party or optional community-specific extensions developed outside the core  
- Plugins implement one or more of the five `StageFright.Plugins.Contracts` interfaces (§8.1)  
- Plugins are physically located in a dedicated `Plugins/` folder and loaded from external
  assemblies at runtime  
- Plugins are discovered and registered reflectively at runtime (unlike core modules, which
  are registered explicitly — see §4.6)  
- Plugins implement well-defined contracts and extension points  
- Plugins maintain backward compatibility with core contracts  
- Plugins are independently distributable and versioned  
- A plugin MAY supply its own `DbContext` (via `IDataAccessProvider`) merged into the shared
  SQLite database, with its own `__EFMigrationsHistory_{PluginName}` table and
  `{PluginName}_`-prefixed tables to avoid collisions with core tables  

**Transition Strategy**:
- MVP modules may be refactored into discoverable plugins after initial release if needed  
- Plugin infrastructure is designed with future extensibility in mind, even though every
  current module ships bundled with the core application  

---

## 9. Specification Requirements

### 9.1 Structure of a Spec
Every spec must include:

- Purpose  
- Scope  
- Responsibilities  
- Interfaces / Contracts  
- Dependencies  
- Extension Points  
- Error Handling Requirements  
- Observability Requirements  
- Constraints  
- Acceptance Criteria  

### 9.2 Prohibited in Specs
- Tight coupling  
- Hidden side effects  
- Global state  
- Domain logic referencing infrastructure  

---

## 10. Planning and Implementation Rules

### 10.1 Plans
Plans must:

- Break work into small tasks  
- Map tasks to architectural layers  
- Identify plug‑in boundaries  
- Highlight risks  

### 10.2 Implementation
Implementations must:

- Follow this constitution  
- Use dependency injection  
- Avoid static state  
- Prefer composition  
- Maintain backward compatibility for plug‑ins  

---

## 11. Testing Standards

Testing is a FIRST-CLASS citizen in the project. All code must be testable and tested. Testing requirements are mandatory and non-negotiable.

### 11.0 Non-Negotiable Coverage Rule
- Every reachable code path MUST be covered by automated tests before merge.
- "Code path" includes success, validation failure, domain rule violation,
  boundary/null/empty inputs, state transitions, and exception/error paths.
- Work is not complete until path coverage evidence exists in tests for the
  feature's changed behavior.

### 11.1 Unit Testing
- **Coverage**: All reachable branches in public functions and critical business
  logic MUST have unit tests or justified integration/acceptance coverage  
- **Isolation**: Mock abstractions, not concrete implementations. Use **NSubstitute**
  (`Substitute.For<T>()`) — this is the project's exclusive mocking library; Moq MUST NOT
  be introduced  
- **Dependencies**: No live database or external dependencies allowed in unit tests
- **Blazor Components**: Use bUnit for isolated component testing  
- **Test Organization**: Unit tests live in `tests/[Project].Tests/` folders  
- **Naming**: Test methods follow `Should_[ExpectedBehavior]_When_[Condition]` naming convention  
- **Assertions**: Use clear, specific assertions. Fail fast with descriptive messages  
- **Focus**: Test behavior, not implementation details  

### 11.2 Integration Testing
Integration tests validate that multiple components work together correctly with realistic (non-mocked) dependencies while remaining isolated from external services.

- **Scope**: Integration tests must verify all service-to-service interactions, repository operations, multi-layer workflows, and *all user-facing UI functions end-to-end*  
- **Database**: Use a real SQLite connection (file-backed or in-memory SQLite, not EF Core's
  `UseInMemoryDatabase` provider) so SQLite-specific behavior (transactions, unique
  constraints) is exercised for real  
- **Real Dependencies**: Application services, repositories, and business logic operate with real code paths  
- **Isolation from External Services**: Mock or stub external APIs, file systems, messaging systems, and cloud services  
- **Fixtures and Setup**: Use fixture factories to create consistent test data; leverage database seeding or migrations  
- **Test Organization**: Integration tests live in `tests/[Project].Tests/` with descriptive class names (e.g., `MemberRepositoryIntegrationTests`, `FinancialServiceIntegrationTests`, `DashboardIntegrationTests`, `UIIntegrationTests`)  
- **Naming**: Follow pattern `Should_[ExpectedBehavior]_When_[Condition]_Integration` to distinguish from unit tests  
- **Transaction Isolation**: Each test must run in isolation; use transactions or database reset between tests to prevent cross-test pollution  
- **Performance**: Integration tests may be slower but must complete within reasonable time; use parallel test runners where appropriate  
- **Coverage**: *All user-facing UI functions must have full integration test coverage* in addition to critical workflows and repo/service interactions:
  - Member CRUD operations  
  - Financial transaction processing  
  - Rehearsal scheduling and attendance tracking  
  - Fee calculation and payment processing  
  - Data migrations and soft delete behavior  
  - **All UI workflows, navigation, and user journeys**  
- **Error Handling**: Integration tests must validate error scenarios (e.g., constraint violations, concurrent updates, missing entities, UI error states)  
- **Code Path Requirement**: Integration tests MUST cover cross-layer success,
  rollback, retry, and exception translation paths for affected workflows.

### 11.3 UI Testing
UI testing validates that Blazor components render correctly, respond to user interactions, and integrate with application services.

- **Component Testing**: Use bUnit for Blazor component unit tests (isolated component behavior)  
- **UI Integration Testing**: Use Blazor testing libraries and UI automation frameworks to test components in context with mocked services  
- **Full UI Function Integration**: *All user-facing UI functions must be exercised by integration tests that simulate real user journeys through the UI, including navigation, form input, validation, and error handling.*
- **User Interactions**: Test user gestures and input handling (click, input, navigation)  
- **Render Validation**: Assert on rendered HTML output and DOM state  
- **Service Integration**: Mock application services with NSubstitute to test components with realistic (mocked) data flows  
- **Test Organization**:  
  - Component unit tests: `tests/StageFright.UI.Tests/Pages/` and `tests/StageFright.UI.Tests/Shared/`  
  - UI integration tests: `tests/StageFright.UI.Tests/Integration/` and `tests/StageFright.Integration.Tests/UI/`  
- **Coverage Requirements**:  
  - All public pages and user-facing functions must have UI integration tests for primary and edge-case user journeys  
  - All reusable UI components (forms, modals, lists, tables) must have bUnit tests  
  - Custom controls and layout components must be tested for rendering and event handling  
  - Navigation and routing must be validated  
- **Test Data**: Use mock services and fixtures to provide consistent test data; avoid hardcoding UI text assertions (use i18n keys if applicable)  
- **Accessibility**: UI tests should include assertions on accessibility attributes (ARIA labels, semantic HTML)  
- **Blazor-Specific Patterns**:  
  - Test parameter binding and cascading parameters  
  - Test event callbacks and two-way data binding  
  - Test lifecycle hooks (OnInitializedAsync, OnAfterRenderAsync)  
  - Test render fragments and templated components  
- **Code Path Requirement**: UI tests MUST cover happy-path, validation, empty-state,
  and recoverable error journeys for each user-facing function.

### 11.4 Acceptance Testing
Acceptance tests validate that implemented features satisfy user stories and acceptance criteria end-to-end.

- **Scope**: End-to-end testing of complete user journeys with real or near-real data  
- **Mapping**: Each acceptance test maps directly to a user story and its acceptance scenarios  
- **Independence**: Each acceptance test should be executable independently and validate one primary user journey  
- **Test Data**: Use realistic data; may use test databases or staging environments  
- **Tools**: Use Selenium (web Blazor), UI automation frameworks (MAUI), or BDD frameworks (SpecFlow, xBehave) as appropriate  
- **Organization**: Acceptance tests organized by user story; clearly named to match spec acceptance scenarios  
- **Ownership**: Acceptance criteria drive these tests; tests should be understandable by non-technical stakeholders  
- **Coverage**: All P1 and P2 user stories must have acceptance tests before code is merged  
- **Fail-Safe**: Tests must not be flaky; use explicit waits, retry logic for transient failures  

### 11.5 Test Quality Standards
- **Determinism**: All tests must be deterministic (never flaky). Do not rely on timing; use explicit waits.  
- **Clarity**: Test names and assertions must be self-explanatory  
- **No Duplication**: Extract common test setup into shared fixtures or helper methods  
- **Error Messages**: Assertion messages must clearly indicate what failed and why  
- **Maintenance**: Tests must be maintained alongside code; broken tests must be fixed immediately  
- **CI/CD**: All tests must pass in CI/CD pipelines before code is merged  
- **Merge Gate**: Code-path coverage requirements from 11.0 MUST be verified as part
  of review and CI before merge.  
- **Performance**: Test suites must execute within:  
  - Unit tests: < 5 seconds total  
  - Integration tests: < 30 seconds total  
  - UI tests: < 60 seconds total  
  - Acceptance tests: < 5 minutes total (may be higher for comprehensive end-to-end suites)  
- **UI Integration Test Requirement**: *No UI function may be considered complete or merged without passing integration tests that exercise the full user journey through the UI, including navigation, input, validation, and error handling.*

---

## 12. Governance

### 12.1 Spec Review
Specs must be reviewed for:

- SOLID compliance  
- Separation of concerns  
- Extension points  
- Architectural consistency  
- Exhaustive code-path test coverage plan and evidence  
- Custom exception taxonomy and boundary translation rules  

### 12.2 Change Management
Changes must:

- Preserve backward compatibility  
- Avoid breaking plug‑in contracts  
- Document architectural impact  

### 12.3 Charter Governance
Changes to charter-level principles must:

- Include justification  
- Be reviewed by maintainers  
- Align with long-term architectural identity  

---

## 13. Versioning and Evolution
This constitution is a living document.  
Changes require:

- A formal proposal  
- Maintainer review  
- Justification aligned with long‑term goals  

### 13.1 Long‑Term Goals
- Support a robust plug‑in ecosystem  
- Optional cloud backup/sync  
- Support multiple performing arts disciplines  
- Provide reliable import/export  
- Maintain a sustainable, maintainable codebase  
