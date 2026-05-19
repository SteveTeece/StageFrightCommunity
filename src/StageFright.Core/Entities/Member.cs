namespace StageFright.Core.Entities;

/// <summary>
/// Represents a member of the performing arts organization.
/// Supports soft-delete and effective dating (activation/inactivation).
/// </summary>
public class Member
{
	public Guid Id { get; set; } = Guid.NewGuid();
	public string Name { get; set; } = string.Empty;
	public string StreetAddress { get; set; } = string.Empty;
	public string? Phone { get; set; }
	public string? Email { get; set; }
	public DateTime JoinDate { get; set; }
	public DateTime? DateOfBirth { get; set; }
	public string Status { get; set; } = "Active"; // MemberStatus enum as string
	public DateTime? ActivateDate { get; set; }
	public DateTime? InactivateDate { get; set; }
	public bool IsDeleted { get; set; }
	public DateTime? DeletedAt { get; set; }
	public string? DeletedBy { get; set; }
}
