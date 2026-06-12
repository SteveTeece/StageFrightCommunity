namespace StageFright.Core.Enums;

/// <summary>Member lifecycle status — Active or Inactive.</summary>
public enum MemberStatus
{
    /// <summary>Member is currently active and eligible for fees and attendance.</summary>
    Active,

    /// <summary>Member has been inactivated; excluded from fee generation but retains history.</summary>
    Inactive
}
