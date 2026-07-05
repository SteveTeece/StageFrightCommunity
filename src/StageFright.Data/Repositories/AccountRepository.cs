using Microsoft.EntityFrameworkCore;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Exceptions;

namespace StageFright.Data.Repositories;

public class AccountRepository : SoftDeletableBaseRepository<Account>, IAccountRepository
{
    public AccountRepository(StageFrightDbContext db) : base(db) { }

    public async Task<bool> IsReferencedByTransactionsAsync(Guid accountId, CancellationToken ct = default)
    {
        return await _db.Transactions.AnyAsync(t => t.AccountId == accountId, ct);
    }

    public async Task<string> GetNextAccountNumberAsync(AccountType type, bool isBank, CancellationToken ct = default)
    {
        var (rangeStart, rangeEnd, firstNumber) = GetRange(type, isBank);

        // Archived accounts still own their numbers — IgnoreQueryFilters is mandatory here.
        var candidates = await _db.Accounts
            .IgnoreQueryFilters()
            .Where(a => a.Type == type && !a.IsSystem && a.IsBankAccount == isBank)
            .Select(a => a.AccountNumber)
            .ToListAsync(ct);

        var maxInRange = candidates
            .Select(n => int.TryParse(n, out var value) ? value : -1)
            .Where(n => n >= rangeStart && n <= rangeEnd)
            .DefaultIfEmpty(firstNumber - 1)
            .Max();

        var next = Math.Max(maxInRange + 1, firstNumber);
        if (next > rangeEnd)
            throw new DataIntegrityException(
                $"Account number range {rangeStart}–{rangeEnd} for {type} is exhausted.",
                nameof(Account), nameof(GetNextAccountNumberAsync));

        var nextNumber = $"{next:D4}";

        // Defensive duplicate assertion: numbers are unique across the whole chart,
        // including system and archived accounts.
        var taken = await _db.Accounts
            .IgnoreQueryFilters()
            .AnyAsync(a => a.AccountNumber == nextNumber, ct);
        if (taken)
            throw new DataIntegrityException(
                $"Computed account number {nextNumber} is already in use.",
                nameof(Account), nameof(GetNextAccountNumberAsync));

        return nextNumber;
    }

    public async Task ReorderAsync(IReadOnlyList<(Guid Id, int SortOrder)> order, CancellationToken ct = default)
    {
        foreach (var (id, sortOrder) in order)
        {
            var account = await _db.Accounts.FindAsync(new object[] { id }, ct);
            if (account is not null)
            {
                account.SortOrder = sortOrder;
                account.UpdatedAt = DateTime.UtcNow;
            }
        }
        await _db.SaveChangesAsync(ct);
    }

    public override async Task ArchiveAsync(Guid id, string deletedBy, CancellationToken ct = default)
    {
        var account = await _db.Accounts.FindAsync(new object[] { id }, ct)
            ?? throw new EntityNotFoundException(nameof(Account), id, nameof(ArchiveAsync));

        if (account.IsSystem)
            throw new ValidationException("System accounts cannot be archived.", nameof(Account), nameof(ArchiveAsync), id);

        if (await IsReferencedByTransactionsAsync(id, ct))
            throw new ValidationException(
                "This account cannot be archived because it is referenced by one or more transactions.",
                nameof(Account), nameof(ArchiveAsync), id);

        await base.ArchiveAsync(id, deletedBy, ct);
    }

    private static (int RangeStart, int RangeEnd, int FirstNumber) GetRange(AccountType type, bool isBank) => type switch
    {
        AccountType.Asset when isBank => (1000, 1999, 1110),
        AccountType.Asset => (1000, 1999, 1300),
        AccountType.Liability => (2000, 2999, 2000),
        AccountType.Equity => (3000, 3999, 3300),
        AccountType.Income => (4000, 4999, 4000),
        _ => (6000, 6999, 6000)
    };
}
