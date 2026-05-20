namespace StageFright.Core.Enums;

/// <summary>
/// Represents the action type recorded in the audit trail.
/// </summary>
public enum AuditAction
{
	/// <summary>Entity was created.</summary>
	Create,

	/// <summary>Entity was updated.</summary>
	Update,

	/// <summary>Entity was deleted.</summary>
	Delete
}
