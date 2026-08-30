using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Exceptions;
using StageFright.Core.Localization;
using StageFright.Core.Modules.Localization.Resources;

namespace StageFright.Core.Modules.Finance;

/// <summary>
/// Posts manually entered multi-line general journals verbatim under a
/// GeneralJournal journal entry. Lines to the Member Receivable account are
/// blocked — per-member balances may only change through fee/payment workflows.
/// </summary>
public class GeneralJournalService : IGeneralJournalService
{
    private readonly IAccountRepository _accountRepo;
    private readonly IGLRepository _glRepo;
    private readonly IJournalEntryRepository _journalRepo;
    private readonly IAuditTrailService _audit;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILocalizer _localizer;

    public GeneralJournalService(
        IAccountRepository accountRepo,
        IGLRepository glRepo,
        IJournalEntryRepository journalRepo,
        IAuditTrailService audit,
        IUnitOfWork unitOfWork,
        ILocalizer localizer)
    {
        _accountRepo = accountRepo;
        _glRepo = glRepo;
        _journalRepo = journalRepo;
        _audit = audit;
        _unitOfWork = unitOfWork;
        _localizer = localizer;
    }

    public async Task<IReadOnlyList<Account>> GetJournalAccountsAsync(CancellationToken ct = default)
    {
        var all = await _accountRepo.GetAllAsync(ct);
        return all
            .Where(a => a.Id != SystemAccounts.MemberReceivableId)
            .OrderBy(a => a.AccountNumber)
            .ToList();
    }

    public async Task RecordJournalAsync(RecordJournalRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Description))
            throw new ValidationException(
                _localizer.Get<ValidationResource>("Validation_Journal_DescriptionRequired"),
                nameof(JournalEntry), nameof(RecordJournalAsync));

        if (request.Lines.Count < 2)
            throw new ValidationException(
                _localizer.Get<ValidationResource>("Validation_Journal_MinimumTwoLines"),
                nameof(JournalEntry), nameof(RecordJournalAsync));

        foreach (var line in request.Lines)
        {
            if (line.DebitAmount < 0m || line.CreditAmount < 0m)
                throw new ValidationException(
                    _localizer.Get<ValidationResource>("Validation_Journal_LineAmountsNonNegative"),
                    nameof(JournalEntry), nameof(RecordJournalAsync));

            if ((line.DebitAmount != 0m) == (line.CreditAmount != 0m))
                throw new ValidationException(
                    _localizer.Get<ValidationResource>("Validation_Journal_LineOneNonZeroSide"),
                    nameof(JournalEntry), nameof(RecordJournalAsync));

            if (line.AccountId == SystemAccounts.MemberReceivableId)
                throw new ValidationException(
                    _localizer.Get<ValidationResource>("Validation_Journal_NoMemberReceivable"),
                    nameof(JournalEntry), nameof(RecordJournalAsync));
        }

        if (request.Lines.Sum(l => l.DebitAmount) != request.Lines.Sum(l => l.CreditAmount))
            throw new ValidationException(
                _localizer.Get<ValidationResource>("Validation_Journal_OutOfBalance"),
                nameof(JournalEntry), nameof(RecordJournalAsync));

        var all = await _accountRepo.GetAllAsync(ct);
        var accountsById = all.ToDictionary(a => a.Id);

        foreach (var line in request.Lines)
        {
            if (!accountsById.ContainsKey(line.AccountId))
                throw new EntityNotFoundException(nameof(Account), line.AccountId, nameof(RecordJournalAsync));
        }

        await _unitOfWork.ExecuteInTransactionAsync(async innerCt =>
        {
            var now = DateTime.UtcNow;
            var description = request.Description!.Trim();

            var entry = await _journalRepo.AddAsync(new JournalEntry
            {
                Id = Guid.NewGuid(),
                Type = JournalEntryType.GeneralJournal,
                Date = request.Date,
                Description = description,
                CreatedAt = now
            }, innerCt);

            var lines = request.Lines.Select(l =>
            {
                var account = accountsById[l.AccountId];
                return new Transaction
                {
                    Id = Guid.NewGuid(),
                    Date = request.Date,
                    AccountId = account.Id,
                    DebitAmount = l.DebitAmount,
                    CreditAmount = l.CreditAmount,
                    GLAccount = account.AccountNumber,
                    JournalEntryId = entry.Id,
                    Description = description,
                    CreatedAt = now
                };
            }).ToList();

            await _glRepo.AddBalancedSetAsync(lines, innerCt);

            await _audit.LogAsync(
                nameof(JournalEntry), entry.Id, AuditAction.Create,
                oldValue: null,
                newValue: $"General journal {request.Lines.Sum(l => l.DebitAmount):C} ({request.Lines.Count} lines) on {request.Date:yyyy-MM-dd}: {description}",
                ct: innerCt);

        }, ct);
    }
}
