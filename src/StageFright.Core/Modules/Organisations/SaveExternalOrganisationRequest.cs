namespace StageFright.Core.Modules.Organisations;

/// <summary>Input for creating or updating an external organisation.</summary>
public class SaveExternalOrganisationRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? Email { get; set; }
}
