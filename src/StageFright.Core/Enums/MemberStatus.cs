namespace StageFright.Core.Enums;

/// <summary>
/// Represents a member's participation status in the organization.
/// Status is independent from archival (IsDeleted flag) per Constitution §3.5.
/// </summary>
public enum MemberStatus
{
	/// <summary>Member is actively participating; fees apply.</summary>
	Active,

	/// <summary>Member exists but is not participating; no fees accrue.</summary>
	Inactive
}
