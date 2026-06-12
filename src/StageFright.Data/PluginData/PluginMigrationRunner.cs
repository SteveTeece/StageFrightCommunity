using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using StageFright.Core.Exceptions;
using StageFright.Plugins.Contracts;

namespace StageFright.Data.PluginData;

/// <summary>
/// Discovers IDataAccessProvider implementations and runs their database migrations
/// against the shared SQLite connection. Failures are logged and the plugin is skipped.
/// </summary>
public class PluginMigrationRunner
{
    private readonly string _connectionString;
    private readonly IEnumerable<IDataAccessProvider> _providers;
    private readonly ILogger<PluginMigrationRunner> _logger;

    public PluginMigrationRunner(
        string connectionString,
        IEnumerable<IDataAccessProvider> providers,
        ILogger<PluginMigrationRunner> logger)
    {
        _connectionString = connectionString;
        _providers = providers;
        _logger = logger;
    }

    public async Task RunAsync(CancellationToken ct = default)
    {
        foreach (var provider in _providers)
        {
            try
            {
                _logger.LogInformation("Running migrations for plugin {PluginName}", provider.PluginName);

                var optionsBuilder = new DbContextOptionsBuilder();
                optionsBuilder.UseSqlite(_connectionString,
                    opts => opts.MigrationsHistoryTable($"__EFMigrationsHistory_{provider.PluginName}"));

                var dbContext = (DbContext)Activator.CreateInstance(provider.DbContextType, optionsBuilder.Options)!;
                await dbContext.Database.MigrateAsync(ct);

                _logger.LogInformation("Plugin {PluginName} migrations completed", provider.PluginName);
            }
            catch (Exception ex)
            {
                var error = new PluginLoadException(
                    $"Failed to run migrations for plugin '{provider.PluginName}': {ex.Message}",
                    provider.PluginName, "Migration", null, ex);

                _logger.LogError(error, "Plugin migration failed for {PluginName}; plugin skipped", provider.PluginName);
                // Continue to next plugin — startup must not be blocked
            }
        }
    }
}
