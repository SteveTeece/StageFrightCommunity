using System.ComponentModel.DataAnnotations;

namespace StageFright.Core.Modules.Settings;

/// <summary>
/// Validates that a string is a well-formed Australian Business Number (ATO checksum).
/// Null/empty values are treated as valid so this attribute can be paired with
/// <see cref="RequiredAttribute"/> where an ABN is mandatory, or used alone where it is optional
/// but must be well-formed whenever supplied.
/// </summary>
public sealed class AbnAttribute : ValidationAttribute
{
    public AbnAttribute()
        : base("The ABN is not valid.")
    {
    }

    public override bool IsValid(object? value)
    {
        if (value is not string abn || string.IsNullOrEmpty(abn))
            return true;

        return AbnValidator.IsValid(abn);
    }
}
