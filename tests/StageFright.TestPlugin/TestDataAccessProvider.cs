using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StageFright.Plugins.Contracts;

namespace StageFright.TestPlugin;

/// <summary>
/// Proves the plugin data-access pattern: supplies a DbContext that the core migration runner
/// merges into the shared SQLite database under its own migrations history table.
/// </summary>
public class TestDataAccessProvider : IDataAccessProvider
{
    public string PluginName => "TestPlugin";

    public Type DbContextType => typeof(TestPluginDbContext);

    public void RegisterServices(IServiceCollection services)
    {
        // No additional services needed for the test fixture; the DbContext is
        // registered by PluginMigrationRunner using the shared connection string.
        services.AddScoped<TestPluginDbContext>();
    }
}
