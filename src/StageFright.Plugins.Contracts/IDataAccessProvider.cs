using Microsoft.Extensions.DependencyInjection;

namespace StageFright.Plugins.Contracts;

/// <summary>
/// Implemented by plugins that require their own database schema.
/// The plugin DbContext targets the shared SQLite file; its migration history table is named
/// "__EFMigrationsHistory_{PluginName}" to avoid collisions with the core history table.
/// Plugin tables must be prefixed "{PluginName}_".
/// </summary>
public interface IDataAccessProvider
{
    /// <summary>Unique plugin name used as the table prefix and migration history table suffix.</summary>
    string PluginName { get; }

    /// <summary>The plugin-owned DbContext type that manages the plugin's schema.</summary>
    Type DbContextType { get; }

    /// <summary>Registers plugin repositories and services into the application DI container.</summary>
    void RegisterServices(IServiceCollection services);
}
