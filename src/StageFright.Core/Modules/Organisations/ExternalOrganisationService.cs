using Microsoft.Extensions.Localization;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Exceptions;
using StageFright.Core.Modules.Localization.Resources;

namespace StageFright.Core.Modules.Organisations;

/// <summary>
/// The external organisation register (spec 032, US1). Every write trims its input, runs
/// <see cref="ExternalOrganisationValidationService"/>, checks the name against the other active
/// organisations (case-insensitive, FR-003), then saves and audits inside one transaction (FR-014).
/// Archiving is a soft delete that leaves contacts and linked journal entries untouched.
/// </summary>
public class ExternalOrganisationService : IExternalOrganisationService
{
    private const string AuditUser = "system";

    private readonly IExternalOrganisationRepository _repo;
    private readonly ExternalOrganisationValidationService _validation;
    private readonly IAuditTrailService _audit;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IStringLocalizer<ValidationResource> _localizer;

    public ExternalOrganisationService(
        IExternalOrganisationRepository repo,
        ExternalOrganisationValidationService validation,
        IAuditTrailService audit,
        IUnitOfWork unitOfWork,
        IStringLocalizer<ValidationResource> localizer)
    {
        _repo = repo;
        _validation = validation;
        _audit = audit;
        _unitOfWork = unitOfWork;
        _localizer = localizer;
    }

    public Task<IReadOnlyList<ExternalOrganisation>> GetActiveAsync(CancellationToken ct = default) =>
        _repo.GetAllAsync(ct);

    public Task<IReadOnlyList<ExternalOrganisation>> GetArchivedAsync(CancellationToken ct = default) =>
        _repo.GetArchivedAsync(ct);

    public Task<ExternalOrganisation?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        _repo.GetByIdIncludingArchivedAsync(id, ct);

    public async Task<ExternalOrganisation> CreateAsync(SaveExternalOrganisationRequest request, CancellationToken ct = default)
    {
        var clean = Normalise(request);
        Validate(clean, nameof(CreateAsync), null);
        await EnsureNameFreeAsync(clean.Name, null, nameof(CreateAsync), ct);

        var now = DateTime.UtcNow;
        var organisation = new ExternalOrganisation
        {
            Id = Guid.NewGuid(),
            Name = clean.Name,
            Address = clean.Address,
            Email = clean.Email,
            CreatedAt = now,
            UpdatedAt = now
        };

        ExternalOrganisation saved = null!;
        await _unitOfWork.ExecuteInTransactionAsync(async innerCt =>
        {
            saved = await _repo.AddAsync(organisation, innerCt);
            await _audit.LogAsync(nameof(ExternalOrganisation), saved.Id, AuditAction.Create,
                newValue: Describe(saved), ct: innerCt);
        }, ct);

        return saved;
    }

    public async Task UpdateAsync(Guid id, SaveExternalOrganisationRequest request, CancellationToken ct = default)
    {
        var organisation = await RequireAsync(id, nameof(UpdateAsync), ct);
        if (organisation.IsDeleted)
            throw Refusal("Validation_Organisation_Archived", nameof(UpdateAsync), id);

        var clean = Normalise(request);
        Validate(clean, nameof(UpdateAsync), id);
        await EnsureNameFreeAsync(clean.Name, id, nameof(UpdateAsync), ct);

        var oldValue = Describe(organisation);
        organisation.Name = clean.Name;
        organisation.Address = clean.Address;
        organisation.Email = clean.Email;
        organisation.UpdatedAt = DateTime.UtcNow;

        await _unitOfWork.ExecuteInTransactionAsync(async innerCt =>
        {
            await _repo.UpdateAsync(organisation, innerCt);
            await _audit.LogAsync(nameof(ExternalOrganisation), id, AuditAction.Update,
                oldValue: oldValue, newValue: Describe(organisation), ct: innerCt);
        }, ct);
    }

    public async Task ArchiveAsync(Guid id, CancellationToken ct = default)
    {
        var organisation = await RequireAsync(id, nameof(ArchiveAsync), ct);
        if (organisation.IsDeleted)
            throw Refusal("Validation_Organisation_Archived", nameof(ArchiveAsync), id);

        await _unitOfWork.ExecuteInTransactionAsync(async innerCt =>
        {
            await _repo.ArchiveAsync(id, AuditUser, innerCt);
            await _audit.LogAsync(nameof(ExternalOrganisation), id, AuditAction.Delete,
                oldValue: organisation.Name, ct: innerCt);
        }, ct);
    }

    public async Task RestoreAsync(Guid id, CancellationToken ct = default)
    {
        var organisation = await RequireAsync(id, nameof(RestoreAsync), ct);
        if (!organisation.IsDeleted)
            return; // Already active: nothing to restore, nothing to audit.

        await EnsureNameFreeAsync(organisation.Name, id, nameof(RestoreAsync), ct);

        await _unitOfWork.ExecuteInTransactionAsync(async innerCt =>
        {
            await _repo.RestoreAsync(id, innerCt);
            await _audit.LogAsync(nameof(ExternalOrganisation), id, AuditAction.Restore,
                newValue: organisation.Name, ct: innerCt);
        }, ct);
    }

    /// <summary>A trimmed copy of the request, with blank optional fields as null.</summary>
    private static SaveExternalOrganisationRequest Normalise(SaveExternalOrganisationRequest request) => new()
    {
        Name = request.Name?.Trim() ?? string.Empty,
        Address = BlankToNull(request.Address),
        Email = BlankToNull(request.Email)
    };

    private static string? BlankToNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>Audit text holding every editable field, so an update records what changed.</summary>
    private static string Describe(ExternalOrganisation organisation) =>
        string.Join(" | ", organisation.Name, organisation.Address ?? string.Empty, organisation.Email ?? string.Empty);

    private void Validate(SaveExternalOrganisationRequest request, string operation, Guid? id)
    {
        var errors = _validation.GetErrors(request);
        if (errors.Count > 0)
            throw new ValidationException(errors.First().Value, nameof(ExternalOrganisation), operation, id);
    }

    private async Task EnsureNameFreeAsync(string name, Guid? excludeId, string operation, CancellationToken ct)
    {
        if (await _repo.IsActiveNameInUseAsync(name, excludeId, ct))
            throw Refusal("Validation_Organisation_NameDuplicate", operation, excludeId);
    }

    private async Task<ExternalOrganisation> RequireAsync(Guid id, string operation, CancellationToken ct) =>
        await _repo.GetByIdIncludingArchivedAsync(id, ct)
            ?? throw new EntityNotFoundException(nameof(ExternalOrganisation), id, operation);

    private ValidationException Refusal(string key, string operation, Guid? id) =>
        new(_localizer[key], nameof(ExternalOrganisation), operation, id);
}
