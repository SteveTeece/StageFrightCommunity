namespace StageFright.Core.Exceptions;

/// <summary>
/// Thrown when a domain validation rule is violated (required fields, format, range, etc.).
/// </summary>
public sealed class ValidationException : Exception
{
    /// <summary>The entity type that failed validation (e.g., "Member", "Fee").</summary>
    public string EntityType { get; }

    /// <summary>The entity identifier, if available at the time of the failure.</summary>
    public Guid? EntityId { get; }

    /// <summary>Context describing the operation that triggered the failure.</summary>
    public string OperationContext { get; }

    /// <summary>UTC timestamp of the failure.</summary>
    public DateTime Timestamp { get; } = DateTime.UtcNow;

    /// <summary>Correlation identifier for cross-layer tracing.</summary>
    public Guid CorrelationId { get; } = Guid.NewGuid();

    public ValidationException(string message, string entityType, string operationContext, Guid? entityId = null, Exception? innerException = null)
        : base(message, innerException)
    {
        EntityType = entityType;
        EntityId = entityId;
        OperationContext = operationContext;
    }
}
