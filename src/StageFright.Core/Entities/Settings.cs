namespace StageFright.Core.Entities;

/// <summary>
/// Represents global organization settings (singleton pattern).
/// Stores organization configuration, fees, and UI preferences.
/// </summary>
public class Settings
{
	public Guid Id { get; set; } = Guid.NewGuid();
	public string OrganizationName { get; set; } = string.Empty;
	public decimal AnnualFee { get; set; }
	public decimal AttendanceFee { get; set; }
	public int RenewalMonth { get; set; } = 1; // 1-12, for annual fee application
	public int CommitteeRenewalMonth { get; set; } = 1; // 1-12, for committee annual reset
	public int LastCommitteeResetYear { get; set; }
	public int MaxAgeRange { get; set; } = 150;
	public int MinimumMemberAge { get; set; } = 0;
	public string Theme { get; set; } = "Dark"; // Theme enum as string
	public DateTime CreatedAt { get; set; }
	public DateTime ModifiedAt { get; set; }
}
