using System.Runtime.Loader;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StageFright.Core.Exceptions;
using StageFright.Plugins.Contracts;

namespace StageFright.App;

/// <summary>
/// Discovers plugin assemblies in the Plugins/ directory, loads each in its own
/// AssemblyLoadContext, and registers discovered providers into DI.
/// Failures per assembly are caught → PluginLoadException logged → assembly skipped.
/// </summary>
public static class PluginLoader
{
    public static void DiscoverAndRegister(IServiceCollection services, string pluginsDirectory, ILogger logger)
    {
        if (!Directory.Exists(pluginsDirectory))
        {
            logger.LogInformation("Plugins directory not found at {Path}; no plugins loaded", pluginsDirectory);
            return;
        }

        foreach (var dllPath in Directory.GetFiles(pluginsDirectory, "*.dll"))
        {
            try
            {
                var context = new AssemblyLoadContext(Path.GetFileNameWithoutExtension(dllPath), isCollectible: false);
                var assembly = context.LoadFromAssemblyPath(dllPath);

                var providerTypes = assembly.GetTypes()
                    .Where(t => !t.IsAbstract && !t.IsInterface)
                    .ToList();

                RegisterProviderTypes<IDashboardTileProvider>(services, providerTypes, logger);
                RegisterProviderTypes<ISettingsTabProvider>(services, providerTypes, logger);
                RegisterProviderTypes<IMenuItemProvider>(services, providerTypes, logger);
                RegisterProviderTypes<IDataAccessProvider>(services, providerTypes, logger);

                logger.LogInformation("Plugin loaded: {Assembly}", assembly.FullName);
            }
            catch (Exception ex)
            {
                var error = new PluginLoadException(
                    $"Failed to load plugin assembly '{dllPath}': {ex.Message}",
                    Path.GetFileName(dllPath), "AssemblyLoad", null, ex);

                logger.LogError(error, "Plugin load failed for {Assembly}; skipped", Path.GetFileName(dllPath));
            }
        }
    }

    private static void RegisterProviderTypes<TInterface>(IServiceCollection services, List<Type> types, ILogger logger)
    {
        foreach (var type in types.Where(t => typeof(TInterface).IsAssignableFrom(t)))
        {
            services.AddSingleton(typeof(TInterface), type);
            logger.LogDebug("Registered plugin provider: {Type} as {Interface}", type.FullName, typeof(TInterface).Name);
        }
    }
}
