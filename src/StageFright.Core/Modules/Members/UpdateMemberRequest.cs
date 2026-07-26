namespace StageFright.Core.Modules.Members;

/// <summary>Input data for updating an existing member's profile fields.</summary>
public record UpdateMemberRequest
{
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public string StreetAddress { get; init; } = string.Empty;
    public string? Phone { get; init; }
    public string? Email { get; init; }
    public DateTime JoinDate { get; init; }
    public DateTime? DateOfBirth { get; init; }
}
