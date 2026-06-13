using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Modules.Members;

namespace StageFright.Core.Contracts;

/// <summary>Application service contract for member lifecycle management.</summary>
public interface IMemberService
{
    Task<Member> CreateAsync(CreateMemberRequest request, CancellationToken ct = default);
    Task UpdateAsync(Guid id, UpdateMemberRequest request, CancellationToken ct = default);
    Task<Member?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Member>> GetByStatusAsync(MemberStatus status, CancellationToken ct = default);
    Task<IReadOnlyList<Member>> GetArchivedAsync(CancellationToken ct = default);
    Task InactivateAsync(Guid id, CancellationToken ct = default);
    Task ActivateAsync(Guid id, CancellationToken ct = default);
    Task ArchiveAsync(Guid id, CancellationToken ct = default);
    Task RestoreAsync(Guid id, CancellationToken ct = default);
}
