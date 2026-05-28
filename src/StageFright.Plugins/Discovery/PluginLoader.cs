using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace StageFright.Plugins.Discovery;

/// <summary>
/// Plugin discovery and loader for dashboard tiles and reports.
/// Discovers all implementations of plugin contracts via assembly reflection and registers with DI.
/// </summary>
public static class PluginLoader
{
    /// <summary>
    /// Discover and register all plugin providers in the dependency injection container.
    /// Scans all assemblies in the current AppDomain for implementations of IDashboardTileProvider and IReportProvider.
    /// </summary>
    /// <param name="services">The IServiceCollection to register plugins in</param>
    /// <returns>The IServiceCollection for chaining</returns>
    public static IServiceCollection AddPlugins(this IServiceCollection services)
    {
        var pluginTypes = DiscoverPlugins();

        foreach (var (interfaceType, implementationType) in pluginTypes)
        {
            services.AddScoped(interfaceType, implementationType);
        }

        return services;
    }

    /// <summary>
    /// Discover all plugin implementations in loaded assemblies.
    /// </summary>
    /// <returns>List of (interface, implementation) type pairs</returns>
    private static List<(Type InterfaceType, Type ImplementationType)> DiscoverPlugins()
    {
        var pluginTypes = new List<(Type, Type)>();
        var pluginAssemblies = new[]
        {
            typeof(PluginLoader).Assembly, // StageFright.Plugins
            // Additional plugin assemblies can be loaded here in the future
        };

        foreach (var assembly in pluginAssemblies)
        {
            try
            {
                DiscoverPluginsInAssembly(assembly, pluginTypes);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error discovering plugins in {assembly.FullName}: {ex}");
            }
        }

        return pluginTypes;
    }

    /// <summary>
    /// Discover plugin implementations within a specific assembly.
    /// </summary>
    private static void DiscoverPluginsInAssembly(Assembly assembly, List<(Type, Type)> results)
    {
        try
        {
            var types = assembly.GetTypes();

            foreach (var type in types)
            {
                if (type.IsInterface || type.IsAbstract)
                    continue;

                // Check for IDashboardTileProvider implementation
                if (typeof(Contracts.IDashboardTileProvider).IsAssignableFrom(type))
                {
                    results.Add((typeof(Contracts.IDashboardTileProvider), type));
                }

                // Check for IReportProvider implementation
                if (typeof(Contracts.IReportProvider).IsAssignableFrom(type))
                {
                    results.Add((typeof(Contracts.IReportProvider), type));
                }
            }
        }
        catch (ReflectionTypeLoadException ex)
        {
            System.Diagnostics.Debug.WriteLine($"ReflectionTypeLoadException in {assembly.FullName}: {ex}");
        }
    }

    /// <summary>
    /// Get all registered dashboard tile providers from the service provider.
    /// </summary>
    public static IEnumerable<Contracts.IDashboardTileProvider> GetDashboardTiles(this IServiceProvider provider)
    {
        return provider.GetServices<Contracts.IDashboardTileProvider>();
    }

    /// <summary>
    /// Get all registered report providers from the service provider.
    /// </summary>
    public static IEnumerable<Contracts.IReportProvider> GetReports(this IServiceProvider provider)
    {
        return provider.GetServices<Contracts.IReportProvider>();
    }
}
