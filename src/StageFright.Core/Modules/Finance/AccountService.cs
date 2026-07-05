using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Exceptions;

namespace StageFright.Core.Modules.Finance;

/// <summary>
/// Application service for chart-of-accounts management.
/// Assigns account numbers sequentially on creation, enforces system-account immutability,
/// restricts the bank flag to Asset accounts, blocks archival of referenced accounts,
/// and writes audit trail entries.
/// </summary>
public class AccountService : IAccountService
{
    private readonly IAccountRepository _repo;
    private readonly AccountNumberAssignmentService _numberAssignment;
    private readonly IAuditTrailService _audit;

    public AccountService(
        IAccountRepository repo,
        AccountNumberAssignmentService numberAssignment,
        IAuditTrailService audit)
    {
        _repo = repo;
        _numberAssignment = numberAssignment;
        _audit = audit;
    }

    public Task<IReadOnlyList<Account>> GetAllAsync(CancellationToken ct = default) =>
        _repo.GetAllAsync(ct);

    public Task<IReadOnlyList<Account>> GetArchivedAsync(CancellationToken ct = default) =>
        _repo.GetArchivedAsync(ct);

    public async Task<Account> CreateAsync(string name, AccountType type, bool isBankAccount = false, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ValidationException("Account name is required.", nameof(Account), nameof(CreateAsync));

        if (isBankAccount && type != AccountType.Asset)
            throw new ValidationException(
                "The bank/cash flag can only be set on Asset accounts.",
                nameof(Account), nameof(CreateAsync));

        var trimmed = name.Trim();
        await EnsureNameIsUniqueAsync(trimmed, excludeId: null, nameof(CreateAsync), ct);

        var accountNumber = await _numberAssignment.AssignNextAsync(type, isBankAccount, ct);
        var now = DateTime.UtcNow;

        var account = new Account
        {
            Id = Guid.NewGuid(),
            Name = trimmed,
            Type = type,
            AccountNumber = accountNumber,
            SortOrder = 0,
            IsSystem = false,
            IsBankAccount = isBankAccount,
            CreatedAt = now,
            UpdatedAt = now
        };

        var saved = await _repo.AddAsync(account, ct);
        await _audit.LogAsync(nameof(Account), saved.Id, AuditAction.Create,
            oldValue: null, newValue: $"{trimmed} ({type} {accountNumber})", ct: ct);
        return saved;
    }

    public async Task UpdateAsync(Guid id, string name, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ValidationException("Account name is required.", nameof(Account), nameof(UpdateAsync), id);

        var account = await _repo.GetByIdAsync(id, ct)
            ?? throw new EntityNotFoundException(nameof(Account), id, nameof(UpdateAsync));

        if (account.IsSystem)
            throw new ValidationException(
                "System accounts cannot be edited.",
                nameof(Account), nameof(UpdateAsync), id);

        var trimmed = name.Trim();
        await EnsureNameIsUniqueAsync(trimmed, excludeId: id, nameof(UpdateAsync), ct);

        var oldName = account.Name;
        account.Name = trimmed;
        account.UpdatedAt = DateTime.UtcNow;
        await _repo.UpdateAsync(account, ct);

        await _audit.LogAsync(nameof(Account), id, AuditAction.Update,
            oldValue: oldName, newValue: trimmed, ct: ct);
    }

    public async Task ArchiveAsync(Guid id, CancellationToken ct = default)
    {
        var account = await _repo.GetByIdAsync(id, ct)
            ?? throw new EntityNotFoundException(nameof(Account), id, nameof(ArchiveAsync));

        if (account.IsSystem)
            throw new ValidationException(
                "System accounts cannot be archived.",
                nameof(Account), nameof(ArchiveAsync), id);

        if (await _repo.IsReferencedByTransactionsAsync(id, ct))
            throw new ValidationException(
                "This account cannot be archived because it is referenced by one or more transactions.",
                nameof(Account), nameof(ArchiveAsync), id);

        await _repo.ArchiveAsync(id, "system", ct);
        await _audit.LogAsync(nameof(Account), id, AuditAction.Delete,
            oldValue: account.Name, newValue: null, ct: ct);
    }

    public async Task RestoreAsync(Guid id, CancellationToken ct = default)
    {
        await _repo.RestoreAsync(id, ct);
        await _audit.LogAsync(nameof(Account), id, AuditAction.Restore,
            oldValue: null, newValue: null, ct: ct);
    }

    public Task ReorderAsync(IReadOnlyList<(Guid Id, int SortOrder)> order, CancellationToken ct = default) =>
        _repo.ReorderAsync(order, ct);

    private async Task EnsureNameIsUniqueAsync(string name, Guid? excludeId, string operation, CancellationToken ct)
    {
        var existing = await _repo.GetAllAsync(ct);
        if (existing.Any(a => a.Id != excludeId && string.Equals(a.Name, name, StringComparison.OrdinalIgnoreCase)))
            throw new ValidationException(
                $"An account named '{name}' already exists.",
                nameof(Account), operation);
    }
}
