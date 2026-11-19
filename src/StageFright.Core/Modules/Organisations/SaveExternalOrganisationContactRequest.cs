namespace StageFright.Core.Modules.Organisations;

/// <summary>Input for adding or editing a contact person on an external organisation.</summary>
public class SaveExternalOrganisationContactRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Role { get; set; }
    public string? Phone { get; set; }
    public string? Mobile { get; set; }
    public string? Email { get; set; }
}
