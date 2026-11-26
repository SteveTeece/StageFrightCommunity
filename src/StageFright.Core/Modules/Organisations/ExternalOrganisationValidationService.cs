using System.Text.RegularExpressions;
using Microsoft.Extensions.Localization;
using StageFright.Core.Modules.Localization.Resources;

namespace StageFright.Core.Modules.Organisations;

/// <summary>
/// The one owner of the field length and email-format rules for external organisations and their
/// contacts. Returns a field→message map keyed by request property name, so a form can show each
/// message next to its field (FR-004). The duplicate-name rule is not checked here because it needs
/// the repository; the organisation service checks it.
/// </summary>
public partial class ExternalOrganisationValidationService
{
    private const int NameMaxLength = 200;
    private const int AddressMaxLength = 1000;
    private const int EmailMaxLength = 254;
    private const int RoleMaxLength = 100;
    private const int PhoneMaxLength = 50;

    private readonly IStringLocalizer<ValidationResource> _localizer;

    // Same pattern as MemberValidationService.
    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex EmailRegex();

    public ExternalOrganisationValidationService(IStringLocalizer<ValidationResource> localizer)
    {
        _localizer = localizer;
    }

    /// <summary>Field errors for an organisation request; empty when valid.</summary>
    public IReadOnlyDictionary<string, string> GetErrors(SaveExternalOrganisationRequest request)
    {
        var errors = new Dictionary<string, string>();

        AddNameError(errors, nameof(request.Name), request.Name,
            "Validation_Organisation_NameRequired", "Validation_Organisation_NameMaxLength");
        AddMaxLengthError(errors, nameof(request.Address), request.Address, AddressMaxLength,
            "Validation_Organisation_AddressMaxLength");
        AddEmailError(errors, nameof(request.Email), request.Email,
            "Validation_Organisation_EmailMaxLength", "Validation_Organisation_EmailInvalid");

        return errors;
    }

    /// <summary>Field errors for a contact request; empty when valid.</summary>
    public IReadOnlyDictionary<string, string> GetErrors(SaveExternalOrganisationContactRequest request)
    {
        var errors = new Dictionary<string, string>();

        AddNameError(errors, nameof(request.Name), request.Name,
            "Validation_OrganisationContact_NameRequired", "Validation_OrganisationContact_NameMaxLength");
        AddMaxLengthError(errors, nameof(request.Role), request.Role, RoleMaxLength,
            "Validation_OrganisationContact_RoleMaxLength");
        AddMaxLengthError(errors, nameof(request.Phone), request.Phone, PhoneMaxLength,
            "Validation_OrganisationContact_PhoneMaxLength");
        AddMaxLengthError(errors, nameof(request.Mobile), request.Mobile, PhoneMaxLength,
            "Validation_OrganisationContact_MobileMaxLength");
        AddEmailError(errors, nameof(request.Email), request.Email,
            "Validation_OrganisationContact_EmailMaxLength", "Validation_OrganisationContact_EmailInvalid");

        return errors;
    }

    private void AddNameError(Dictionary<string, string> errors, string field, string? name,
        string requiredKey, string maxLengthKey)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            errors[field] = _localizer[requiredKey];
            return;
        }

        AddMaxLengthError(errors, field, name, NameMaxLength, maxLengthKey);
    }

    private void AddMaxLengthError(Dictionary<string, string> errors, string field, string? value,
        int maxLength, string key)
    {
        if (value is not null && value.Trim().Length > maxLength)
            errors[field] = _localizer[key];
    }

    private void AddEmailError(Dictionary<string, string> errors, string field, string? email,
        string maxLengthKey, string invalidKey)
    {
        if (string.IsNullOrWhiteSpace(email))
            return;

        var trimmed = email.Trim();
        if (trimmed.Length > EmailMaxLength)
            errors[field] = _localizer[maxLengthKey];
        else if (!EmailRegex().IsMatch(trimmed))
            errors[field] = _localizer[invalidKey];
    }
}
