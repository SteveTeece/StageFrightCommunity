namespace StageFright.Core.Modules.Members;

/// <summary>Input data for creating a new member.</summary>
public record CreateMemberRequest
{
    public string Name { get; init; } = string.Empty;
    public string StreetAddress { get; init; } = string.Empty;
    public string? Phone { get; init; }
    public string? Email { get; init; }
    public DateTime JoinDate { get; init; }
    public DateTime? DateOfBirth { get; init; }
}
