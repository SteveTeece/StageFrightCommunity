using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Exceptions;
using SettingsEntity = StageFright.Core.Entities.Settings;

namespace StageFright.Core.Modules.Members;

/// <summary>
/// Handles member lifecycle: creation, status transitions (Active ↔ Inactive), and archival.
/// ArchiveAsync cascades soft-delete to the current-year CommitteeMembership only.
/// </summary>
public class MemberService : IMemberService
{
    private readonly IMemberRepository _memberRepo;
    private readonly ICommitteeMembershipRepository _committeeRepo;
    private readonly MemberValidationService _validation;
    private readonly ISettingsRepository _settingsRepo;
    private readonly IAuditTrailService _audit;
    private readonly IUnitOfWork _unitOfWork;

    public MemberService(
        IMemberRepository memberRepo,
        ICommitteeMembershipRepository committeeRepo,
        MemberValidationService validation,
        ISettingsRepository settingsRepo,
        IAuditTrailService audit,
        IUnitOfWork unitOfWork)
    {
        _memberRepo = memberRepo;
        _committeeRepo = committeeRepo;
        _validation = validation;
        _settingsRepo = settingsRepo;
        _audit = audit;
        _unitOfWork = unitOfWork;
    }

    public async Task<Member> CreateAsync(CreateMemberRequest request, CancellationToken ct = default)
    {
        var settings = await RequireSettingsAsync(ct);
        _validation.Validate(request, settings);

        var member = new Member
        {
            Id = Guid.NewGuid(),
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            StreetAddress = request.StreetAddress.Trim(),
            Phone = request.Phone?.Trim(),
            Email = request.Email?.Trim(),
            JoinDate = request.JoinDate,
            DateOfBirth = request.DateOfBirth,
            Status = MemberStatus.Active,
            ActivateDate = DateTime.UtcNow.Date,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        Member saved = null!;
        await _unitOfWork.ExecuteInTransactionAsync(async innerCt =>
        {
            saved = await _memberRepo.AddAsync(member, innerCt);
            await _audit.LogAsync("Member", saved.Id, AuditAction.Create, ct: innerCt);
        }, ct);

        return saved;
    }

    public async Task UpdateAsync(Guid id, UpdateMemberRequest request, CancellationToken ct = default)
    {
        var member = await RequireMemberAsync(id, "UpdateAsync", ct);
        var settings = await RequireSettingsAsync(ct);
        _validation.Validate(request, settings);

        var oldFirstName = member.FirstName;
        var oldLastName = member.LastName;

        member.FirstName = request.FirstName.Trim();
        member.LastName = request.LastName.Trim();
        member.StreetAddress = request.StreetAddress.Trim();
        member.Phone = request.Phone?.Trim();
        member.Email = request.Email?.Trim();
        member.JoinDate = request.JoinDate;
        member.DateOfBirth = request.DateOfBirth;
        member.UpdatedAt = DateTime.UtcNow;

        await _unitOfWork.ExecuteInTransactionAsync(async innerCt =>
        {
            await _memberRepo.UpdateAsync(member, innerCt);
            await _audit.LogAsync("Member", id, AuditAction.Update,
                oldValue: $"{oldFirstName} {oldLastName}",
                newValue: $"{request.FirstName} {request.LastName}",
                ct: innerCt);
        }, ct);
    }

    public Task<Member?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        _memberRepo.GetByIdAsync(id, ct);

    public Task<IReadOnlyList<Member>> GetByStatusAsync(MemberStatus status, CancellationToken ct = default) =>
        _memberRepo.GetByStatusAsync(status, ct);

    public Task<IReadOnlyList<Member>> GetArchivedAsync(CancellationToken ct = default) =>
        _memberRepo.GetArchivedAsync(ct);

    public async Task InactivateAsync(Guid id, CancellationToken ct = default)
    {
        var member = await RequireMemberAsync(id, "InactivateAsync", ct);

        member.Status = MemberStatus.Inactive;
        member.InactivateDate = DateTime.UtcNow.Date;
        member.UpdatedAt = DateTime.UtcNow;

        // CommitteeMembership records are NOT cascaded on inactivation
        await _unitOfWork.ExecuteInTransactionAsync(async innerCt =>
        {
            await _memberRepo.UpdateAsync(member, innerCt);
            await _audit.LogAsync("Member", id, AuditAction.StatusChange,
                oldValue: MemberStatus.Active.ToString(),
                newValue: MemberStatus.Inactive.ToString(),
                ct: innerCt);
        }, ct);
    }

    public async Task ActivateAsync(Guid id, CancellationToken ct = default)
    {
        var member = await RequireMemberAsync(id, "ActivateAsync", ct);

        member.Status = MemberStatus.Active;
        member.ActivateDate = DateTime.UtcNow.Date;
        member.UpdatedAt = DateTime.UtcNow;

        await _unitOfWork.ExecuteInTransactionAsync(async innerCt =>
        {
            await _memberRepo.UpdateAsync(member, innerCt);
            await _audit.LogAsync("Member", id, AuditAction.StatusChange,
                oldValue: MemberStatus.Inactive.ToString(),
                newValue: MemberStatus.Active.ToString(),
                ct: innerCt);
        }, ct);
    }

    public async Task ArchiveAsync(Guid id, CancellationToken ct = default)
    {
        await RequireMemberAsync(id, "ArchiveAsync", ct);

        var currentYear = DateTime.UtcNow.Year;
        var committeeRecords = await _committeeRepo.GetByMemberAsync(id, ct);

        await _unitOfWork.ExecuteInTransactionAsync(async innerCt =>
        {
            await _memberRepo.ArchiveAsync(id, "system", innerCt);

            // Cascade soft-delete only to current-year committee record (preserves history)
            foreach (var record in committeeRecords.Where(r => r.Year == currentYear))
                await _committeeRepo.ArchiveAsync(record.Id, "system", innerCt);

            await _audit.LogAsync("Member", id, AuditAction.Delete, ct: innerCt);
        }, ct);
    }

    public async Task RestoreAsync(Guid id, CancellationToken ct = default)
    {
        await _unitOfWork.ExecuteInTransactionAsync(async innerCt =>
        {
            await _memberRepo.RestoreAsync(id, innerCt);
            await _audit.LogAsync("Member", id, AuditAction.Restore, ct: innerCt);
        }, ct);
    }

    private async Task<Member> RequireMemberAsync(Guid id, string operationContext, CancellationToken ct)
    {
        var member = await _memberRepo.GetByIdAsync(id, ct);
        if (member is null)
            throw new EntityNotFoundException("Member", id, operationContext);
        return member;
    }

    private async Task<SettingsEntity> RequireSettingsAsync(CancellationToken ct)
    {
        return await _settingsRepo.GetAsync(ct) ??
               new SettingsEntity { MaxAgeRangeYears = 150, MinimumMemberAge = 0 };
    }
}
