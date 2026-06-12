namespace StageFright.Core.Exceptions;

/// <summary>
/// Thrown when a plugin assembly fails to load, reflect, or migrate its database schema.
/// Startup continues after this exception is caught and logged.
/// </summary>
public sealed class PluginLoadException : Exception
{
    /// <summary>Name of the plugin assembly or provider that failed to load.</summary>
    public string EntityType { get; }

    /// <summary>Not applicable for plugins; present for interface consistency.</summary>
    public Guid? EntityId { get; }

    /// <summary>The load step that failed (e.g., "AssemblyLoad", "Migration", "DI Registration").</summary>
    public string OperationContext { get; }

    /// <summary>UTC timestamp of the failure.</summary>
    public DateTime Timestamp { get; } = DateTime.UtcNow;

    /// <summary>Correlation identifier for cross-layer tracing.</summary>
    public Guid CorrelationId { get; } = Guid.NewGuid();

    public PluginLoadException(string message, string pluginName, string operationContext, Guid? entityId = null, Exception? innerException = null)
        : base(message, innerException)
    {
        EntityType = pluginName;
        EntityId = entityId;
        OperationContext = operationContext;
    }
}
