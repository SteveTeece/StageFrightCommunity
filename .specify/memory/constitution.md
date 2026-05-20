<!--
SYNC IMPACT REPORT
==================
Version Change: 2.2.0 → 2.2.1

Modified Principles:
- 4.0 Architectural Identity (added Settings System)
- Renumbered: 4.3 UI Design Principles → 4.4 UI Design Principles

Added Sections:
- 4.3 Settings System (tabbed interface with module-specific tabs and application settings)

Removed Sections:
- None

Templates Requiring Updates:
- ✅ already done: .specify/templates/plan-template.md
- ✅ already done: .specify/templates/spec-template.md
- ✅ already done: .specify/templates/tasks-template.md

Runtime Guidance Docs:
- ⚠ pending: CONTRIBUTING.md (add settings tab implementation guide)
- ⚠ pending: ARCHITECTURE.md (add Settings System architecture section)
- ⚠ pending: UI_COMPONENT_STYLE_GUIDE.md (add settings form patterns)
- ⚠ pending: README.md (reference settings configuration)

Follow-up TODOs:
- Document Application Settings data model for organization, fees, membership rules
- Create settings validation patterns
- Document module settings tab registration

Version Bump Rationale: PATCH
- Adds tabbed settings architecture with module extensibility. Non-breaking change; affects UI organization and new module onboarding.
-->

# Spec Kit Constitution  
*A guiding document for clean, modular, extensible software development*

**Version**: 2.2.1  
**Ratification Date**: 2025-01-01  
**Last Amended**: 2026-05-15

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
- Vertical Slice module architecture (see §4.1)  
- Dashboard tile system for feature exposure (see §4.2)  
- Settings system with module-specific tabs (see §4.3)  
- Navigation menu system with module-defined items (see §4.5)  
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

### 4.1 Vertical Slice Module Architecture

Each feature or functional domain must be organized as a self-contained vertical slice with its own dedicated folder. This pattern promotes modularity, independent development, and clear separation of concerns.

**Module Structure Requirements:**

- **One Module Per Folder**: Each feature or functional domain gets its own folder at the appropriate level in the project hierarchy.  
- **No MediaTr or CQRS**: Vertical slices must NOT use MediaTr for command/query dispatch or implement CQRS patterns. Instead, use:  
  - Direct service injection and method calls  
  - Dependency-injected handlers for business logic  
  - Clear, explicit request/response models  
  - Standard repository and service patterns  
- **Ownership**: Each module owns its own:  
  - Domain entities and value objects  
  - Application services and handlers  
  - Infrastructure adapters (repositories, external service clients)  
  - UI components (pages, forms, shared controls scoped to this module)  
  - Unit and integration tests  
  - Dashboard tile definitions (§4.2)  
- **Folder Naming**: Module folders should use domain language (e.g., `Members`, `FinancialAuditing`, `EventScheduling`) and follow naming conventions consistent with project structure.  
- **Internal Structure**: Within each module folder:  
  ```
  ModuleName/
  ├── Domain/              # Entities, value objects, contracts
  ├── Application/         # Services, handlers, orchestration
  ├── Infrastructure/      # Repositories, external integrations
  ├── UI/                  # Blazor components, pages (if applicable)
  ├── Tests/               # Unit and integration tests scoped to this module
  └── DashboardTile.cs     # Tile provider implementation (see §4.2)
  ```
- **No Cross-Module Dependencies**: Modules must NOT import from sibling modules' private implementation details. Modules communicate through:  
  - Dependency injection of published interfaces  
  - Event-driven patterns for decoupled pub/sub  
  - Shared contracts defined at the application or domain level  
- **Testing Isolation**: Each module's tests must be independently executable and isolated from other modules.  

### 4.2 Dashboard Tile System

The dashboard is the primary user-facing interface for feature discovery and interaction. Each module exposes its functionality through dashboard tiles. Tiles are extensible, composable, and support rich content.

**Tile Requirements:**

- **Tile Definition**: Each module MUST define one or more dashboard tiles through an implementation of a well-defined tile provider contract.  
- **Tile Content**: Tiles MAY contain:  
  - Summary information or metrics (e.g., count of active members, outstanding fees)  
  - Charts and graphs (e.g., revenue trends, attendance distribution)  
  - Quick-action buttons (e.g., "Add Member", "Record Payment")  
  - Recent activity feeds (e.g., last 5 scheduled events)  
  - Status indicators  
- **Tile Characteristics**:  
  - Self-contained rendering (tile handles its own data loading and rendering)  
  - No inter-tile dependencies  
  - Consistent sizing and layout within dashboard grid  
  - Responsive to user interactions without leaving the dashboard  
- **Multiple Tiles per Module**: A module MAY define multiple tiles to represent different aspects (e.g., "Members Overview" and "Member Onboarding Quick Action").  
- **Tile Registration**: Tiles are registered with the dashboard system via DI or a tile discovery mechanism; no hardcoding of tile instances.  
- **Failure Isolation**: If a tile fails to load or render, it must gracefully degrade without breaking the entire dashboard.  

### 4.3 Settings System

The Settings page is a core application feature where configuration and preferences are managed. The settings architecture uses a **tabbed interface** where each module provides its own settings tab, and the core application provides an "Application Settings" tab.

**Settings Architecture:**

- **Settings Page**: A base application page with tabbed interface at `/settings`
- **Module Settings Tabs**: Each module MAY define a settings tab through an `ISettingsTabProvider` contract
- **Tab Registry**: Settings tabs are discovered and registered at application startup via DI
- **Application Settings Tab**: Core application settings (built-in, not from a module)

**Module Settings Tab Requirements:**

- **Tab Definition**: Modules that have configurable settings MUST implement `ISettingsTabProvider` to define their settings tab
- **Tab Interface**: Each provider specifies:
  - Tab title and icon
  - Display order (tabs ordered by priority)
  - Blazor component for settings content
  - Validation and persistence logic
- **Tab Content**: Modules own the layout and content of their settings tab:
  - Form fields, controls, and validation
  - Save/cancel buttons
  - Error handling and user feedback
- **Isolation**: Each module's settings are independent; changes to one tab don't affect others
- **Persistence**: Module settings are persisted through the module's infrastructure layer
- **Validation**: Settings validation happens at module level; custom exceptions used for validation errors

**Application Settings Tab** (Core Application):

The base application provides a built-in "Application Settings" tab containing:

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

**Settings Tab Structure**:

```csharp
public interface ISettingsTabProvider
{
    string TabTitle { get; }
    string TabIcon { get; }
    int DisplayOrder { get; }
    
    Type SettingsComponentType { get; } // Blazor component
    
    Task<ISettingsTab> GetSettingsAsync();
    Task<ValidationResult> ValidateAsync(ISettingsTab settings);
    Task SaveAsync(ISettingsTab settings);
}

// Example: Members module settings
public class MembersSettingsTabProvider : ISettingsTabProvider
{
    public string TabTitle => "Members";
    public string TabIcon => "users";
    public int DisplayOrder => 2;
    
    public Type SettingsComponentType => typeof(MembersSettingsTab);
    
    public async Task<ISettingsTab> GetSettingsAsync()
    {
        return new MembersSettings
        {
            DefaultMemberStatus = "Active",
            AutoArchiveInactiveDays = 365
        };
    }
}
```

**Settings Page Layout**:

```
┌─────────────────────────────────────────────────┐
│  Settings                                       │
├─────────────────────────────────────────────────┤
│ [Application] [Members] [Events] [Finances] ... │
├─────────────────────────────────────────────────┤
│                                                 │
│  Application Settings Content                  │
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

### 4.5 Navigation Menu System

The application provides a hierarchical navigation menu where each module defines its own menu items and sub-items. The menu system is modular, extensible, and always displays Settings as the final menu item.

**Menu Architecture**:

- **Menu Items**: Each module can define primary menu items and optional sub-items
- **Optional Icons**: Menu items may include icons to visually represent functionality
- **Module Order**: Modules contribute menu items in a customizable order
- **Settings Always Last**: The Settings menu item is reserved for the core application and appears last
- **Sub-menus**: Each menu item may have child items for feature grouping

**Menu Item Structure**:

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
    public string Route { get; set; }             // Navigation route (e.g., "/members/list")
    public string Icon { get; set; }              // Optional icon (e.g., "users", "calendar")
    public int DisplayOrder { get; set; }         // Order within module
    public List<MenuItem> SubItems { get; set; }  // Optional sub-menu items
    public string BadgeText { get; set; }         // Optional badge (e.g., count)
    public bool IsActive { get; set; }            // Current page indicator
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
                DisplayOrder = 1,
                SubItems = new List<MenuItem>
                {
                    new() { Title = "Active Members", Route = "/members/list", DisplayOrder = 1 },
                    new() { Title = "Pending Approval", Route = "/members/pending", DisplayOrder = 2 },
                    new() { Title = "Add Member", Route = "/members/new", DisplayOrder = 3 }
                }
            }
        };
    }
}

// Example: Finances module menu
public class FinancesMenuItemProvider : IMenuItemProvider
{
    public string ModuleName => "Finances";
    public int DisplayOrder => 3;
    
    public IReadOnlyList<MenuItem> GetMenuItems()
    {
        return new List<MenuItem>
        {
            new()
            {
                Title = "Finances",
                Route = "/finances",
                Icon = "dollar-sign",
                DisplayOrder = 1,
                SubItems = new List<MenuItem>
                {
                    new() { Title = "Transactions", Route = "/finances/transactions", DisplayOrder = 1 },
                    new() { Title = "Reports", Route = "/finances/reports", DisplayOrder = 2 },
                    new() { Title = "Invoices", Route = "/finances/invoices", DisplayOrder = 3 }
                }
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
├── [Module 3 items by DisplayOrder]        // e.g., Finances (order 3)
├── ... [other modules] ...
└── Settings (Core)                         // Always last
```

**Menu Item Characteristics**:

- **Title**: User-facing text displayed in the menu
- **Route**: Target URL when menu item is clicked
- **Icon**: Optional icon name for visual identification (from icon set)
- **DisplayOrder**: Order within the module (lower numbers first)
- **SubItems**: Optional child menu items for grouping related features
- **BadgeText**: Optional notification badge (e.g., "5" for pending items)
- **IsActive**: Computed property indicating current page

**Menu Registration**:

Modules register menu items through dependency injection:

```csharp
// Features/Members/DependencyInjection.cs
public static IServiceCollection AddMembersModule(this IServiceCollection services)
{
    services.AddScoped<IMemberService, MemberService>();
    services.AddScoped<IMenuItemProvider, MembersMenuItemProvider>();
    
    return services;
}

// Program.cs - Auto-discover all IMenuItemProvider implementations
builder.Services.Scan(scan => scan
    .FromAssemblies(typeof(Program).Assembly)
    .AddClasses(classes => classes.AssignableTo(typeof(IMenuItemProvider)))
    .AsImplementedInterfaces()
    .WithScopedLifetime());
```

**Menu Component Usage**:

The application's main layout component discovers and renders all menu items:

```razor
@* App/Layout/MainLayout.razor *@
@using StageFright.Application.Navigation

<nav class="main-navigation">
    <div class="nav-brand">
        <img src="logo.svg" alt="StageFright" />
        <span>StageFright</span>
    </div>
    
    <ul class="nav-menu">
        @* Dashboard always first *@
        <li class="nav-item">
            <a href="/dashboard" class="nav-link">
                <i class="icon icon-home"></i>
                <span>Dashboard</span>
            </a>
        </li>
        
        @* Module menu items sorted by DisplayOrder *@
        @foreach (var provider in MenuProviders.OrderBy(p => p.DisplayOrder))
        {
            @foreach (var item in provider.GetMenuItems().OrderBy(m => m.DisplayOrder))
            {
                <li class="nav-item">
                    <a href="@item.Route" class="nav-link @(item.IsActive ? "active" : "")">
                        @if (!string.IsNullOrEmpty(item.Icon))
                        {
                            <i class="icon icon-@item.Icon"></i>
                        }
                        <span>@item.Title</span>
                        @if (!string.IsNullOrEmpty(item.BadgeText))
                        {
                            <span class="badge">@item.BadgeText</span>
                        }
                    </a>
                    
                    @* Render sub-items if present *@
                    @if (item.SubItems?.Count > 0)
                    {
                        <ul class="nav-submenu">
                            @foreach (var subItem in item.SubItems.OrderBy(s => s.DisplayOrder))
                            {
                                <li class="nav-subitem">
                                    <a href="@subItem.Route" class="nav-sublink @(subItem.IsActive ? "active" : "")">
                                        @subItem.Title
                                    </a>
                                </li>
                            }
                        </ul>
                    }
                </li>
            }
        }
        
        @* Settings always last *@
        <li class="nav-item nav-settings">
            <a href="/settings" class="nav-link">
                <i class="icon icon-cog"></i>
                <span>Settings</span>
            </a>
        </li>
    </ul>
</nav>
```

**Menu Isolation Rules**:

- Modules MUST NOT depend on menu items from other modules
- Menu items MUST be independent and self-contained
- Route conflicts MUST be avoided (each module owns its route prefix)
- Menu item state (active, badge count) MUST be computed dynamically

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
Define domain-specific custom exceptions that inherit from appropriate base types:

- `PersistenceException`  
- `EntityNotFoundException`  
- `DuplicateEntityException`  
- `ConcurrencyException`  
- `DataIntegrityException`  
- `ConnectionException`  
- `ValidationException`  
- `PluginException`  

Custom exceptions must include:

- Detailed error message  
- Entity type and ID (when applicable)  
- Operation context  
- Original exception as inner exception  
- Timestamp  
- Correlation ID  

Raw framework exceptions (for example `SqlException`, `IOException`,
`InvalidOperationException`) MUST NOT leak across architectural boundaries.
They must be translated to approved custom exceptions with preserved inner
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
- **UI:** Blazor components, including free Radzen Blazor components (`Radzen.Blazor`)  
- **Language:** C# 14  
- **Platforms:** Windows desktop and macOS desktop  
- **Hosting Model:** Blazor Hybrid  

### 7.2 Architecture Requirements
- UI components in a separate class library  
- Use MAUI DI container  
- Platform-specific code via abstractions or conditional compilation  
- CSS isolation for component styling  
- Reusable, testable UI components  
- Free Radzen components are permitted for UI composition when implemented in Blazor components and backed by C# handlers/services  

### 7.3 Prohibited
- Custom JavaScript files or business logic implemented in JavaScript  
- Platform-specific UI frameworks outside MAUI/Blazor  
- Web-hosted Blazor  

---

## 8. Plug‑In Architecture

### 8.1 Architecture Model
- Core system defines contracts and extension points  
- Plug‑ins implement contracts and are discovered at runtime  
- Plug‑ins must be isolated and independently testable  
- No plug‑in may depend on another unless explicitly defined  

### 8.2 MVP Modules vs. Extensible Plugins

The application distinguishes between MVP (Minimum Viable Product) modules and future extensible plugins:

**MVP Modules**:
- Core features included directly in the main application (not in a separate plugins folder)  
- MVP modules are built as vertical slices (§4.1) with defined dashboard tiles (§4.2)  
- MVP modules are organized at the application root or within a feature-organized modules folder  
- Examples: Members, Financial Tracking, Event Scheduling, Attendance  
- MVP modules do NOT require external loading or registration mechanisms  
- MVP modules are part of the shipped application and follow the standard vertical slice pattern  

**Extensible Plugins** (Future Pattern):
- Third-party or optional community-specific extensions developed outside the core  
- Plugins follow the vertical slice pattern (§4.1) and define dashboard tiles (§4.2)  
- Plugins are physically located in a dedicated `Plugins/` folder or loaded from external assemblies  
- Plugins are discovered and registered at runtime  
- Plugins implement well-defined contracts and extension points  
- Plugins maintain backward compatibility with core contracts  
- Plugins are independently distributable and versioned  

**Transition Strategy**:
- MVP modules may be refactored into discoverable plugins after initial release if needed  
- Plugin infrastructure should be designed with future extensibility in mind, even if initial plugins are bundled  

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
- **Isolation**: Mock abstractions, not concrete implementations  
- **Dependencies**: No live database or external dependencies allowed in unit tests
- **Blazor Components**: Use bUnit for isolated component testing  
- **Test Organization**: Unit tests live in `tests/[Project].Tests/` folders  
- **Naming**: Test methods follow `Should_[ExpectedBehavior]_When_[Condition]` naming convention  
- **Assertions**: Use clear, specific assertions. Fail fast with descriptive messages  
- **Focus**: Test behavior, not implementation details  

### 11.2 Integration Testing
Integration tests validate that multiple components work together correctly with realistic (non-mocked) dependencies while remaining isolated from external services.

- **Scope**: Integration tests must verify all service-to-service interactions, repository operations, multi-layer workflows, and *all user-facing UI functions end-to-end*  
- **Database**: Use in-memory databases (e.g., Entity Framework Core In-Memory Provider) or test doubles for data persistence  
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
- **Service Integration**: Mock application services (IServiceProvider, custom services) to test components with realistic (mocked) data flows  
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
