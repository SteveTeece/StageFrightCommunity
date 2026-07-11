namespace StageFright.Core.Exceptions;

/// <summary>
/// Thrown when a referential integrity or business-rule invariant is violated
/// (e.g., attempting to archive a Account referenced by active Transactions).
/// </summary>
public sealed class DataIntegrityException : Exception
{
    /// <summary>The entity type that violated an integrity rule.</summary>
    public string EntityType { get; }

    /// <summary>The entity identifier involved in the violation.</summary>
    public Guid? EntityId { get; }

    /// <summary>Description of the integrity rule that was violated.</summary>
    public string OperationContext { get; }

    /// <summary>UTC timestamp of the failure.</summary>
    public DateTime Timestamp { get; } = DateTime.UtcNow;

    /// <summary>Correlation identifier for cross-layer tracing.</summary>
    public Guid CorrelationId { get; } = Guid.NewGuid();

    public DataIntegrityException(string message, string entityType, string operationContext, Guid? entityId = null, Exception? innerException = null)
        : base(message, innerException)
    {
        EntityType = entityType;
        EntityId = entityId;
        OperationContext = operationContext;
    }
}
