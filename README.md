# StageFright Community

A clean, modern, modular desktop application that reduces administrative overhead for community performing arts groups. Built with .NET MAUI and Blazor Hybrid, featuring accurate financial and attendance tracking with a plugin-friendly architecture.

## Vision

StageFright Community delivers a robust platform for managing members, finances, events, and attendance with:

- **Clean, maintainable code** following SOLID principles
- **Modular vertical slice architecture** for independent feature development
- **Dashboard-driven interface** for intuitive feature discovery
- **Accurate financial tracking** with immutable transaction records
- **Extensible plugin system** for community-specific customization
- **Modern, compact UI design** minimizing whitespace and visual clutter

## Quick Start

### Prerequisites

- **.NET 8.0 or later**
- **Visual Studio 2022** (recommended) or **Visual Studio Code** with C# extensions
- **Windows 11** or **macOS 14+** for desktop platforms

### Building the Project

```bash
# Restore dependencies
dotnet restore

# Build the solution
dotnet build

# Run the application
dotnet run
```

### Running Tests

```bash
# Run all tests with code-path coverage
dotnet test --verbosity normal

# Run specific test project
dotnet test tests/StageFright.Tests.csproj
```

## Architecture

### Vertical Slice Module Architecture

Each feature is organized as a self-contained vertical slice in its own folder:

```
src/Features/
├── Members/                    # Member management module
│   ├── Domain/                # Entities, value objects, contracts
│   ├── Application/           # Services, handlers, orchestration
│   ├── Infrastructure/        # Repositories, data access
│   ├── UI/                    # Blazor components and pages
│   ├── Tests/                 # Unit and integration tests
│   └── DashboardTile.cs       # Dashboard tile provider
├── FinancialTracking/         # Financial tracking module
├── EventScheduling/           # Event scheduling module
└── [Other modules...]
```

**Key Principles:**
- **No MediaTr or CQRS** — modules use direct service injection and standard patterns
- **Self-contained ownership** — each module owns its full vertical slice
- **Independent testing** — modules can be tested in isolation
- **Dashboard tiles** — each module defines how it appears on the dashboard

See [Architecture Guide](docs/ARCHITECTURE.md) for detailed patterns.

### Dashboard Tile System

The dashboard is the primary user interface for feature discovery. Each module exposes functionality through dashboard tiles that can contain:

- Summary metrics and information
- Interactive charts and graphs
- Quick-action buttons
- Activity feeds and status indicators

Tiles are self-contained, independently rendered, and fail gracefully without breaking the dashboard.

### Settings System

Configuration and preferences are managed through the **Settings page** (`/settings`), organized into tabs:

- **Application Settings Tab** — Organization information, annual/rehearsal fees, membership renewal date
- **Module Settings Tabs** — Each module provides its own settings tab for module-specific configuration (e.g., Members tab, Events tab)

Module settings are optional; modules that have configurable options implement the `ISettingsTabProvider` interface to register a settings tab. The application auto-discovers and registers tabs at startup.

**Core Application Settings**:
- Organization/Group Name
- Annual Membership Fee
- Rehearsal/Event Fee  
- Membership Renewal Due Date

### Navigation Menu System

Each module defines its own menu items in the main navigation bar. Menu items can include optional icons and are automatically ordered.

- **Module Menu Items** — Each module contributes menu items for feature navigation
- **Icon Support** — Optional icons visually represent menu functions
- **Sub-menus** — Menu items can have child items for grouping related features
- **Dynamic Badges** — Real-time notification counts (e.g., "5" pending items)
- **Settings Always Last** — Settings menu item is reserved and always appears at the end

Menu items are registered via the `IMenuItemProvider` interface and auto-discovered at application startup. The application renders Dashboard first, then module menu items in order, and Settings last.

**Example Navigation**:
```
Dashboard
├── Members (users icon)
│   ├── Active Members
│   ├── Pending Approval [3]
│   └── Add Member
├── Events (calendar icon)
│   ├── Upcoming Events
│   ├── Past Events
│   └── Create Event
├── Finances (dollar-sign icon)
│   ├── Transactions
│   ├── Reports [2]
│   └── Income Summary
... [other modules] ...
└── Settings (cog icon)
```

See [ARCHITECTURE.md](docs/ARCHITECTURE.md#navigation-menu-system) for menu system implementation details.

## UI Design

The application follows a **clean, simple, modern design** philosophy:

- **Minimal whitespace** — compact layouts optimized for information density
- **Purposeful design** — every visual element serves a user goal
- **Modern aesthetics** — professional color palettes, smooth animations, clear hierarchy
- **Consistent components** — unified design language across all screens
- **Accessible** — keyboard-navigable, screen-reader compatible

See [UI Component Style Guide](docs/UI_COMPONENT_STYLE_GUIDE.md) for detailed design standards and component examples.

## Development Guidelines

### Testing Requirements

**All code must be tested.** Testing is a first-class citizen with mandatory coverage requirements:

- **Every reachable code path** must have automated tests before merge
- **Unit tests** for business logic and component behavior
- **Integration tests** for service interactions and UI workflows
- **UI integration tests** for all user-facing functions
- Tests must cover: success paths, validation failures, exceptions, boundary conditions, and state transitions

### Code Quality Standards

- Follow SOLID principles (Single Responsibility, Open/Closed, Liskov Substitution, Interface Segregation, Dependency Inversion)
- Use custom domain exceptions at architectural boundaries
- Implement soft-delete pattern for data preservation (financial records are immutable)
- Maintain separation of concerns: Domain, Application, Infrastructure, UI, Cross-Cutting

### Custom Exceptions

All exceptions crossing architectural boundaries must use project-defined custom exceptions:

- `PersistenceException`
- `EntityNotFoundException`
- `DuplicateEntityException`
- `ConcurrencyException`
- `DataIntegrityException`
- `ValidationException`
- `PluginException`

Raw framework exceptions must be translated at boundaries.

## Constitution & Governance

This project operates under the **Spec Kit Constitution** (version 2.2.0), which defines:

- Architectural patterns and standards
- Testing requirements and coverage expectations
- Data preservation and soft-delete rules
- Module organization and plugin architecture
- Specification structure and quality gates

See [Constitution](\.specify\memory\constitution.md) for the complete governance framework.

## Contributing

Contributions are welcome! Please read [CONTRIBUTING.md](CONTRIBUTING.md) for:

- Development workflow
- Module structure guidelines
- Creating new features with specifications
- Testing and quality requirements
- Code review process
- Commit conventions

## Technology Stack

- **Framework**: .NET MAUI with Blazor Hybrid
- **Language**: C# 14
- **UI**: Blazor components, Radzen Blazor (free components)
- **Platforms**: Windows desktop, macOS desktop
- **Testing**: xUnit, bUnit, Playwright
- **Logging**: Serilog + OpenTelemetry
- **Data**: Entity Framework Core

## Key Features

- **Member Management** — Add, track, and manage member information
- **Financial Tracking** — Immutable transaction records, fee tracking, payment processing
- **Event Scheduling** — Schedule rehearsals and events, manage attendance
- **Attendance Tracking** — Record attendance, generate reports
- **Dashboard** — At-a-glance feature access through modular dashboard tiles

## License

[Specify your license here]

## Support

- **Documentation**: [docs/](docs/) folder
- **Issues**: GitHub Issues
- **Questions**: GitHub Discussions

## Roadmap

- **MVP** — Core member, financial, and event modules
- **Phase 2** — Plugin architecture and extension points
- **Phase 3** — Cloud sync and backup capabilities
- **Phase 4** — Multi-discipline support and advanced reporting
