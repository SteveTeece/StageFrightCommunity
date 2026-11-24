using Microsoft.Extensions.Localization;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Exceptions;
using StageFright.Core.Modules.Localization.Resources;

namespace StageFright.Core.Modules.Organisations;

/// <summary>
/// Contact people on an external organisation (spec 032, US2). Every write requires the owning
/// organisation to be active, trims its input, runs <see cref="ExternalOrganisationValidationService"/>,
/// then saves and audits inside one transaction (FR-014). Removing a contact is a soft delete (FR-009).
/// </summary>
public class ExternalOrganisationContactService : IExternalOrganisationContactService
{
    private const string AuditUser = "system";

    private readonly IExternalOrganisationContactRepository _contacts;
    private readonly IExternalOrganisationRepository _organisations;
    private readonly ExternalOrganisationValidationService _validation;
    private readonly IAuditTrailService _audit;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IStringLocalizer<ValidationResource> _localizer;

    public ExternalOrganisationContactService(
        IExternalOrganisationContactRepository contacts,
        IExternalOrganisationRepository organisations,
        ExternalOrganisationValidationService validation,
        IAuditTrailService audit,
        IUnitOfWork unitOfWork,
        IStringLocalizer<ValidationResource> localizer)
    {
        _contacts = contacts;
        _organisations = organisations;
        _validation = validation;
        _audit = audit;
        _unitOfWork = unitOfWork;
        _localizer = localizer;
    }

    public async Task<ExternalOrganisationContact> AddAsync(Guid organisationId, SaveExternalOrganisationContactRequest request, CancellationToken ct = default)
    {
        await EnsureOrganisationActiveAsync(organisationId, nameof(AddAsync), ct);
        var clean = Normalise(request);
        Validate(clean, nameof(AddAsync), null);

        var now = DateTime.UtcNow;
        var contact = new ExternalOrganisationContact
        {
            Id = Guid.NewGuid(),
            ExternalOrganisationId = organisationId,
            CreatedAt = now,
            UpdatedAt = now
        };
        Apply(clean, contact);

        ExternalOrganisationContact saved = null!;
        await _unitOfWork.ExecuteInTransactionAsync(async innerCt =>
        {
            saved = await _contacts.AddAsync(contact, innerCt);
            await _audit.LogAsync(nameof(ExternalOrganisationContact), saved.Id, AuditAction.Create,
                newValue: Describe(saved), ct: innerCt);
        }, ct);

        return saved;
    }

    public async Task UpdateAsync(Guid contactId, SaveExternalOrganisationContactRequest request, CancellationToken ct = default)
    {
        var contact = await RequireContactAsync(contactId, nameof(UpdateAsync), ct);
        await EnsureOrganisationActiveAsync(contact.ExternalOrganisationId, nameof(UpdateAsync), ct);
        var clean = Normalise(request);
        Validate(clean, nameof(UpdateAsync), contactId);

        var oldValue = Describe(contact);
        Apply(clean, contact);
        contact.UpdatedAt = DateTime.UtcNow;

        await _unitOfWork.ExecuteInTransactionAsync(async innerCt =>
        {
            await _contacts.UpdateAsync(contact, innerCt);
            await _audit.LogAsync(nameof(ExternalOrganisationContact), contactId, AuditAction.Update,
                oldValue: oldValue, newValue: Describe(contact), ct: innerCt);
        }, ct);
    }

    public async Task RemoveAsync(Guid contactId, CancellationToken ct = default)
    {
        var contact = await RequireContactAsync(contactId, nameof(RemoveAsync), ct);
        await EnsureOrganisationActiveAsync(contact.ExternalOrganisationId, nameof(RemoveAsync), ct);

        await _unitOfWork.ExecuteInTransactionAsync(async innerCt =>
        {
            await _contacts.ArchiveAsync(contactId, AuditUser, innerCt);
            await _audit.LogAsync(nameof(ExternalOrganisationContact), contactId, AuditAction.Delete,
                oldValue: Describe(contact), ct: innerCt);
        }, ct);
    }

    /// <summary>A trimmed copy of the request, with blank optional fields as null.</summary>
    private static SaveExternalOrganisationContactRequest Normalise(SaveExternalOrganisationContactRequest request) => new()
    {
        Name = request.Name?.Trim() ?? string.Empty,
        Role = BlankToNull(request.Role),
        Phone = BlankToNull(request.Phone),
        Mobile = BlankToNull(request.Mobile),
        Email = BlankToNull(request.Email)
    };

    private static string? BlankToNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static void Apply(SaveExternalOrganisationContactRequest clean, ExternalOrganisationContact contact)
    {
        contact.Name = clean.Name;
        contact.Role = clean.Role;
        contact.Phone = clean.Phone;
        contact.Mobile = clean.Mobile;
        contact.Email = clean.Email;
    }

    /// <summary>Audit text holding every editable field, so an update records what changed.</summary>
    private static string Describe(ExternalOrganisationContact contact) =>
        string.Join(" | ", contact.Name, contact.Role ?? string.Empty, contact.Phone ?? string.Empty,
            contact.Mobile ?? string.Empty, contact.Email ?? string.Empty);

    private void Validate(SaveExternalOrganisationContactRequest request, string operation, Guid? id)
    {
        var errors = _validation.GetErrors(request);
        if (errors.Count > 0)
            throw new ValidationException(errors.First().Value, nameof(ExternalOrganisationContact), operation, id);
    }

    /// <summary>A removed contact is hidden by the soft-delete filter, so it reads as unknown.</summary>
    private async Task<ExternalOrganisationContact> RequireContactAsync(Guid id, string operation, CancellationToken ct) =>
        await _contacts.GetByIdAsync(id, ct)
            ?? throw new EntityNotFoundException(nameof(ExternalOrganisationContact), id, operation);

    private async Task EnsureOrganisationActiveAsync(Guid organisationId, string operation, CancellationToken ct)
    {
        var organisation = await _organisations.GetByIdIncludingArchivedAsync(organisationId, ct)
            ?? throw new EntityNotFoundException(nameof(ExternalOrganisation), organisationId, operation);
        if (organisation.IsDeleted)
            throw new ValidationException(_localizer["Validation_Organisation_Archived"],
                nameof(ExternalOrganisation), operation, organisationId);
    }
}
