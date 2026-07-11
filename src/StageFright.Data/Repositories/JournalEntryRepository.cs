using Microsoft.EntityFrameworkCore;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Exceptions;

namespace StageFright.Data.Repositories;

public class JournalEntryRepository : IJournalEntryRepository
{
    private readonly StageFrightDbContext _db;

    public JournalEntryRepository(StageFrightDbContext db)
    {
        _db = db;
    }

    public async Task<JournalEntry> AddAsync(JournalEntry entry, CancellationToken ct = default)
    {
        try
        {
            var added = await _db.JournalEntries.AddAsync(entry, ct);
            await _db.SaveChangesAsync(ct);
            return added.Entity;
        }
        catch (Exception ex) when (ex is not DataAccessException)
        {
            throw new DataAccessException(ex.Message, nameof(JournalEntry), nameof(AddAsync), null, ex);
        }
    }

    public async Task<JournalEntry?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _db.JournalEntries
            .Include(j => j.Transactions)
            .FirstOrDefaultAsync(j => j.Id == id, ct);
    }

    public async Task<bool> AnyOfTypeAsync(JournalEntryType type, CancellationToken ct = default)
    {
        return await _db.JournalEntries.AnyAsync(j => j.Type == type, ct);
    }
}
