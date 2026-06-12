using StageFright.Core.Enums;

namespace StageFright.Core.Entities;

/// <summary>
/// An audit log entry recording a state change on any domain entity.
/// No soft-delete fields — the audit trail is governed by a 12-month retention policy
/// (hard delete at startup via IAuditTrailRepository.PurgeOlderThanAsync). Purge failure
/// logs a structured error but does not prevent startup.
/// </summary>
public class AuditTrailEntry
{
    /// <summary>Primary key (GUID).</summary>
    public Guid Id { get; set; }

    /// <summary>Domain entity type that was changed (e.g., "Member", "Payment").</summary>
    public string EntityType { get; set; } = string.Empty;

    /// <summary>Primary key of the entity that was changed.</summary>
    public Guid EntityId { get; set; }

    /// <summary>The action that was performed on the entity.</summary>
    public AuditAction Action { get; set; }

    /// <summary>JSON snapshot of the field values before the change. Null for create operations.</summary>
    public string? OldValue { get; set; }

    /// <summary>JSON snapshot of the field values after the change. Null for delete operations.</summary>
    public string? NewValue { get; set; }

    /// <summary>Identity of the user who performed the action. Fixed to "system" in MVP (NFR-013).</summary>
    public string UserId { get; set; } = "system";

    /// <summary>UTC timestamp when the audit entry was recorded.</summary>
    public DateTime Timestamp { get; set; }
}
