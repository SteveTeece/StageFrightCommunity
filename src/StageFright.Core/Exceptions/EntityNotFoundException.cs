namespace StageFright.Core.Exceptions;

/// <summary>
/// Thrown when a requested entity does not exist (or is soft-deleted and not accessible in context).
/// </summary>
public sealed class EntityNotFoundException : Exception
{
    /// <summary>The entity type that was not found (e.g., "Member").</summary>
    public string EntityType { get; }

    /// <summary>The identifier that was looked up.</summary>
    public Guid EntityId { get; }

    /// <summary>Description of the operation that triggered the lookup.</summary>
    public string OperationContext { get; }

    /// <summary>UTC timestamp of the failure.</summary>
    public DateTime Timestamp { get; } = DateTime.UtcNow;

    /// <summary>Correlation identifier for cross-layer tracing.</summary>
    public Guid CorrelationId { get; } = Guid.NewGuid();

    public EntityNotFoundException(string entityType, Guid entityId, string operationContext, Exception? innerException = null)
        : base($"{entityType} with id '{entityId}' was not found.", innerException)
    {
        EntityType = entityType;
        EntityId = entityId;
        OperationContext = operationContext;
    }
}
