namespace StageFright.Core.Exceptions;

/// <summary>
/// Wraps unexpected database or persistence errors raised at the DAL boundary.
/// </summary>
public sealed class DataAccessException : Exception
{
    /// <summary>The entity type that was being accessed.</summary>
    public string EntityType { get; }

    /// <summary>The entity identifier involved in the failed operation.</summary>
    public Guid? EntityId { get; }

    /// <summary>Description of the operation that caused the failure.</summary>
    public string OperationContext { get; }

    /// <summary>UTC timestamp of the failure.</summary>
    public DateTime Timestamp { get; } = DateTime.UtcNow;

    /// <summary>Correlation identifier for cross-layer tracing.</summary>
    public Guid CorrelationId { get; } = Guid.NewGuid();

    public DataAccessException(string message, string entityType, string operationContext, Guid? entityId = null, Exception? innerException = null)
        : base(message, innerException)
    {
        EntityType = entityType;
        EntityId = entityId;
        OperationContext = operationContext;
    }
}
