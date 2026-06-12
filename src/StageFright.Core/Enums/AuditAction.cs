namespace StageFright.Core.Enums;

/// <summary>Action recorded in an audit trail entry.</summary>
public enum AuditAction
{
    /// <summary>A new entity was created.</summary>
    Create,

    /// <summary>An existing entity was updated.</summary>
    Update,

    /// <summary>An entity was soft-deleted (archived).</summary>
    Delete,

    /// <summary>A previously archived entity was restored.</summary>
    Restore,

    /// <summary>A member status transition (Active ↔ Inactive).</summary>
    StatusChange,

    /// <summary>Outstanding fees were written off via reactivation forgiveness.</summary>
    Forgiveness,

    /// <summary>Annual committee positions were reset.</summary>
    CommitteeReset,

    /// <summary>Data was imported from a backup file.</summary>
    Import,

    /// <summary>Data was exported to a backup file.</summary>
    Export
}
