# StageFright Community MVP — Project Setup Guide

## Overview

StageFright Community is a desktop application built with .NET MAUI and Blazor, designed to streamline operations for small performing arts groups. This guide provides comprehensive setup instructions for developers.

## System Requirements

### Minimum Requirements
- **OS**: Windows 10.0.19041.0 or later, or macOS 10.15+ (Mac Catalyst)
- **.NET**: .NET 10.0 SDK or later
- **IDE**: Visual Studio 2022 or Visual Studio Code with C# Extensions
- **Database**: SQLite (included with EF Core)

### Recommended
- Visual Studio 2022 Community or Professional Edition
- 8 GB RAM
- SSD for faster build times

## Project Structure

```
StageFrightCommunity/
├── src/
│   ├── StageFright.Core/          # Domain entities, enums, exceptions, services
│   │   ├── Entities/              # Member, Rehearsal, Event, etc.
│   │   ├── Enums/                 # MemberStatus, CategoryType, etc.
│   │   ├── Exceptions/            # Custom exception hierarchy
│   │   └── Services/              # Business logic services
│   ├── StageFright.Data/          # Data access layer with EF Core
│   │   ├── Context/               # StageFrightContext and factory
│   │   ├── Repositories/          # Repository interfaces and implementations
│   │   └── Migrations/            # EF Core migrations
│   ├── StageFright.Maui/          # MAUI application shell
│   │   ├── Platforms/             # Platform-specific code
│   │   ├── Resources/             # Assets, fonts, images
│   │   ├── App.xaml(.cs)          # Application root
│   │   ├── MauiProgram.cs         # DI and logging setup
│   │   └── appsettings.json       # Configuration
│   ├── StageFright.UI/            # Blazor component library
│   │   ├── Pages/                 # Feature page components
│   │   ├── Shared/                # Shared layout and components
│   │   └── Styles/                # Component styles
│   ├── StageFright.Plugins/       # Plugin contracts and discovery
│   ├── StageFright.Reports/       # Reporting infrastructure
│   └── StageFright.Proto/         # Protocol buffer definitions
├── tests/
│   ├── StageFright.Core.Tests/    # Unit tests for Core layer
│   ├── StageFright.Data.Tests/    # Data access layer tests
│   ├── StageFright.UI.Tests/      # UI component tests
│   └── StageFright.Integration.Tests/ # End-to-end integration tests
├── docs/                          # Documentation
├── specs/                         # Feature specifications
│   └── 001-initial-mvp/
│       ├── spec.md               # Feature specification
│       ├── plan.md               # Implementation plan
│       └── tasks.md              # Task breakdown
└── StageFright.slnx              # Solution file
```

## Getting Started

### 1. Clone the Repository

```bash
git clone https://github.com/SteveTeece/StageFrightCommunity.git
cd StageFrightCommunity
```

### 2. Install Dependencies

The project uses NuGet packages for all dependencies. Restore them with:

```bash
dotnet restore
```

Key dependencies:
- **Microsoft.Maui**: Cross-platform UI framework
- **Microsoft.AspNetCore.Components.WebView.Maui**: Blazor integration
- **EntityFrameworkCore**: ORM for data access
- **EntityFrameworkCore.Sqlite**: SQLite provider
- **Serilog**: Structured logging
- **xUnit, Moq, FluentAssertions**: Testing frameworks

### 3. Build the Solution

```bash
dotnet build
```

To build for Release:

```bash
dotnet build --configuration Release
```

### 4. Run Tests

Run all tests:

```bash
dotnet test
```

Run specific test project:

```bash
dotnet test tests/StageFright.Core.Tests/
dotnet test tests/StageFright.Data.Tests/
dotnet test tests/StageFright.UI.Tests/
dotnet test tests/StageFright.Integration.Tests/
```

Run with code coverage:

```bash
dotnet test /p:CollectCoverage=true /p:CoverletOutput=./coverage/ /p:CoverletOutputFormat=opencover
```

### 5. Run the Application

To run the MAUI application:

```bash
dotnet run --project src/StageFright.Maui/
```

The application will launch with a Blazor web view embedded in a MAUI container.

## Database Setup

### Initial Migration

The database schema is managed through EF Core migrations. The initial migration (`20260520001_InitialSchema`) creates all tables and relationships.

#### Apply Migrations

The first run of the application will automatically apply pending migrations. To manually apply migrations:

```bash
dotnet ef database update --project src/StageFright.Data/ --startup-project src/StageFright.Maui/
```

#### Create a New Migration

When you modify entities, create a new migration:

```bash
dotnet ef migrations add MigrationName --project src/StageFright.Data/ --startup-project src/StageFright.Maui/
```

#### Remove Last Migration

```bash
dotnet ef migrations remove --project src/StageFright.Data/ --startup-project src/StageFright.Maui/
```

### Database File Location

By default, the SQLite database is created at:
- **Windows/MAUI**: `stagefright.db` in the application working directory
- **Connection String**: Configured in `src/StageFright.Maui/appsettings.json`

To use a different database location, update the connection string:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=C:\\path\\to\\stagefright.db"
  }
}
```

## Configuration

### Application Settings

Edit `src/StageFright.Maui/appsettings.json` to configure:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=stagefright.db"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning"
    }
  },
  "Serilog": {
    "MinimumLevel": "Information"
  }
}
```

### Logging

Logs are configured using Serilog and output to:
- **Console**: Development debugging
- **File**: `logs/stagefright-YYYYMMDD.txt` (rolling daily)

Log levels:
- `Debug`: Detailed diagnostic information
- `Information`: General application flow
- `Warning`: Potentially problematic situations
- `Error`: Error events with potential recovery
- `Fatal`: Critical failures requiring immediate attention

## Development Workflow

### Local Development

1. **Create a feature branch**: `git checkout -b feature/your-feature`
2. **Make changes** to entities, services, or UI
3. **Run tests** to verify: `dotnet test`
4. **Commit changes**: `git commit -m "feat: description"`
5. **Push and create PR**: GitHub will run CI/CD checks

### Adding New Features

When adding new features:

1. **Create/modify entities** in `src/StageFright.Core/Entities/`
2. **Create migrations**: `dotnet ef migrations add FeatureName`
3. **Implement repositories** in `src/StageFright.Data/Repositories/`
4. **Add services** in `src/StageFright.Core/Services/` for business logic
5. **Create UI components** in `src/StageFright.UI/Pages/`
6. **Write tests** in appropriate test projects
7. **Update documentation** in `docs/` and `specs/`

### Code Style

The project follows C# coding standards:
- Use PascalCase for class and method names
- Use camelCase for local variables and parameters
- Include XML documentation for public types and methods
- Follow SOLID principles
- Use async/await for I/O operations

Example:

```csharp
/// <summary>
/// Gets a member by their ID.
/// </summary>
/// <param name="id">The member ID.</param>
/// <returns>The member if found; otherwise null.</returns>
public async Task<Member?> GetMemberAsync(int id)
{
    return await _context.Members.FindAsync(id);
}
```

## Continuous Integration

### GitHub Actions

The project uses GitHub Actions for CI/CD. The workflow file `.github/workflows/build-and-test.yml`:

1. **Restores** NuGet packages
2. **Builds** the solution in Release configuration
3. **Runs** all test projects
4. **Uploads** test results as artifacts

Workflows run automatically on:
- Push to `master`, `main`, or `develop` branches
- Pull requests to these branches

### Running Workflows Locally

Test the workflow locally with act:

```bash
act -j build-and-test
```

## Common Tasks

### Rebuild Everything

```bash
dotnet clean
dotnet build
```

### Run with Debugging

In Visual Studio:
1. Set breakpoints in your code
2. Press **F5** or click **Start Debugging**
3. Use the Debug toolbar to step through code

From command line:
```bash
dotnet run --project src/StageFright.Maui/ --configuration Debug
```

### Clear Database

To reset the database to a clean state:

1. Delete `stagefright.db` (or the file specified in connection string)
2. Rebuild the project or run migrations again
3. The database will be recreated on next run

### Update a Single Package

```bash
dotnet add src/StageFright.Core/ package PackageName
```

### View Dependency Tree

```bash
dotnet add src/StageFright.Core/ reference --show-resolved-version
```

## Troubleshooting

### Build Fails with "Project not found"

Ensure you're in the correct directory:
```bash
cd C:\SourceCode\StageFrightCommunity
```

### Tests Won't Run

1. Ensure .NET SDK is installed: `dotnet --version`
2. Restore packages: `dotnet restore`
3. Build the solution: `dotnet build`
4. Run tests: `dotnet test`

### Database Migration Issues

If migrations fail:

```bash
# Remove last migration
dotnet ef migrations remove --project src/StageFright.Data/

# Recreate it
dotnet ef migrations add MigrationName --project src/StageFright.Data/

# Apply
dotnet ef database update --project src/StageFright.Data/
```

### MAUI Application Won't Start

1. Check logs in `logs/` directory
2. Verify `appsettings.json` exists in `src/StageFright.Maui/`
3. Ensure database file path is writable
4. Try: `dotnet clean && dotnet build && dotnet run`

## Additional Resources

- [ARCHITECTURE.md](./ARCHITECTURE.md) — System architecture and design patterns
- [specs/001-initial-mvp/spec.md](./specs/001-initial-mvp/spec.md) — Feature specification
- [specs/001-initial-mvp/plan.md](./specs/001-initial-mvp/plan.md) — Implementation plan
- [.NET MAUI Documentation](https://learn.microsoft.com/en-us/dotnet/maui/)
- [Entity Framework Core Documentation](https://learn.microsoft.com/en-us/ef/core/)
- [Blazor Documentation](https://learn.microsoft.com/en-us/aspnet/core/blazor/)

## Getting Help

- Check existing [GitHub Issues](https://github.com/SteveTeece/StageFrightCommunity/issues)
- Review [Pull Requests](https://github.com/SteveTeece/StageFrightCommunity/pulls) for similar work
- Review documentation in `docs/` folder
- Check log files in `logs/` directory for error details

## Contributing

Contributions are welcome! Please:

1. Fork the repository
2. Create a feature branch
3. Write tests for new features
4. Ensure all tests pass
5. Submit a pull request with a clear description

For more details, see [CONTRIBUTING.md](./CONTRIBUTING.md).

---

**Last Updated**: May 20, 2026  
**Version**: 1.0.0
