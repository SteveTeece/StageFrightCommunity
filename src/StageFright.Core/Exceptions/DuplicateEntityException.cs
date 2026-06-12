namespace StageFright.Core.Exceptions;

/// <summary>
/// Thrown when an insert or update would violate a unique constraint.
/// </summary>
public sealed class DuplicateEntityException : Exception
{
    /// <summary>The entity type that would be duplicated.</summary>
    public string EntityType { get; }

    /// <summary>The identifier of the conflicting existing entity, if known.</summary>
    public Guid? EntityId { get; }

    /// <summary>Description of the operation that triggered the conflict.</summary>
    public string OperationContext { get; }

    /// <summary>UTC timestamp of the failure.</summary>
    public DateTime Timestamp { get; } = DateTime.UtcNow;

    /// <summary>Correlation identifier for cross-layer tracing.</summary>
    public Guid CorrelationId { get; } = Guid.NewGuid();

    public DuplicateEntityException(string message, string entityType, string operationContext, Guid? entityId = null, Exception? innerException = null)
        : base(message, innerException)
    {
        EntityType = entityType;
        EntityId = entityId;
        OperationContext = operationContext;
    }
}
