using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Exceptions;
using StageFright.Core.Localization;
using StageFright.Core.Modules.Localization.Resources;

namespace StageFright.Core.Modules.Finance;

/// <summary>
/// Records non-member income (raffles, donations, fundraising) directly to the GL.
/// GL set under an Income journal entry: Debit the account <see cref="ReceiptRouting"/> picks for the
/// payment method (Petty Cash 1100 for cash, Undeposited Funds 1105 for cheque and other, the chosen
/// bank account for card and electronic transfer) / Credit the selected Income account.
/// A cheque or other receipt also adds one <see cref="ReceivedItem"/> for the gross (spec 033).
/// </summary>
public class IncomeEntryService : IIncomeEntryService
{
    private const int MaxReferenceLength = 50;

    private readonly IAccountRepository _accountRepo;
    private readonly IGLRepository _glRepo;
    private readonly IJournalEntryRepository _journalRepo;
    private readonly ISettingsRepository _settingsRepo;
    private readonly IAuditTrailService _audit;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILocalizer _localizer;
    private readonly IExternalOrganisationRepository _organisationRepo;
    private readonly IReceivedItemRepository _receivedItemRepo;

    public IncomeEntryService(
        IAccountRepository accountRepo,
        IGLRepository glRepo,
        IJournalEntryRepository journalRepo,
        ISettingsRepository settingsRepo,
        IAuditTrailService audit,
        IUnitOfWork unitOfWork,
        ILocalizer localizer,
        IExternalOrganisationRepository organisationRepo,
        IReceivedItemRepository receivedItemRepo)
    {
        _accountRepo = accountRepo;
        _glRepo = glRepo;
        _journalRepo = journalRepo;
        _settingsRepo = settingsRepo;
        _audit = audit;
        _unitOfWork = unitOfWork;
        _localizer = localizer;
        _organisationRepo = organisationRepo;
        _receivedItemRepo = receivedItemRepo;
    }

    public async Task<IReadOnlyList<Account>> GetIncomeAccountsAsync(CancellationToken ct = default)
    {
        var all = await _accountRepo.GetAllAsync(ct);
        return all
            .Where(c => c.Type == AccountType.Income && !c.IsSystem)
            .OrderBy(c => c.SortOrder)
            .ThenBy(c => c.Name)
            .ToList();
    }

    public async Task RecordIncomeAsync(RecordIncomeRequest request, CancellationToken ct = default)
    {
        if (request.Amount <= 0m)
            throw new ValidationException(
                _localizer.Get<ValidationResource>("Validation_Income_AmountPositive"),
                nameof(Transaction), nameof(RecordIncomeAsync));

        var all = await _accountRepo.GetAllAsync(ct);
        var account = all.FirstOrDefault(c => c.Id == request.AccountId)
            ?? throw new EntityNotFoundException(
                nameof(Account), request.AccountId, nameof(RecordIncomeAsync));

        if (account.Type != AccountType.Income)
            throw new ValidationException(
                _localizer.Get<ValidationResource>("Validation_Income_AccountNotIncome"),
                nameof(Account), nameof(RecordIncomeAsync));

        if (account.IsSystem)
            throw new ValidationException(
                _localizer.Get<ValidationResource>("Validation_Income_SystemAccountNotAllowed"),
                nameof(Account), nameof(RecordIncomeAsync));

        // Spec 033 research §6: route the money side by payment method.
        var method = request.PaymentMethod;
        var createsItem = ReceiptRouting.CreatesReceivedItem(method);
        var reference = createsItem && !string.IsNullOrWhiteSpace(request.Reference)
            ? request.Reference.Trim()
            : null;

        if (reference is { Length: > MaxReferenceLength })
            throw new ValidationException(
                _localizer.Get<ValidationResource>("Validation_Receipt_ReferenceTooLong"),
                nameof(ReceivedItem), nameof(RecordIncomeAsync));

        Account? bankAccount = null;
        if (method != PaymentMethod.Cash && !createsItem)
        {
            bankAccount = all.FirstOrDefault(a => a.Id == request.BankAccountId);
            if (bankAccount is null || !bankAccount.IsBankAccount || bankAccount.Id == SystemAccounts.CashId)
                throw new ValidationException(
                    _localizer.Get<ValidationResource>("Validation_Receipt_BankAccountRequired"),
                    nameof(Account), nameof(RecordIncomeAsync));
        }

        var destinationId = ReceiptRouting.Destination(method, bankAccount?.Id);
        var destinationNumber = bankAccount?.AccountNumber
            ?? (createsItem ? SystemAccounts.UndepositedFundsNumber : SystemAccounts.CashNumber);
        var destinationName = all.FirstOrDefault(a => a.Id == destinationId)?.Name ?? destinationNumber;

        // Spec 032 research §6: the organisation link is checked before the transaction opens.
        if (request.ExternalOrganisationId is { } organisationId)
        {
            var organisation = await _organisationRepo.GetByIdIncludingArchivedAsync(organisationId, ct)
                ?? throw new EntityNotFoundException(nameof(ExternalOrganisation), organisationId, nameof(RecordIncomeAsync));

            if (organisation.IsDeleted)
                throw new ValidationException(
                    _localizer.Get<ValidationResource>("Validation_Organisation_ArchivedNotSelectable"),
                    nameof(ExternalOrganisation), nameof(RecordIncomeAsync));
        }

        var settings = await _settingsRepo.GetAsync(ct);
        var isTaxApplicable = settings?.IsTaxApplicable ?? false;
        var taxCode = isTaxApplicable ? (request.TaxCode ?? TaxCode.TaxExempt) : (TaxCode?)null;

        await _unitOfWork.ExecuteInTransactionAsync(async innerCt =>
        {
            var now = DateTime.UtcNow;
            var description = string.IsNullOrWhiteSpace(request.Description)
                ? $"Income — {account.Name}"
                : request.Description.Trim();

            var entry = await _journalRepo.AddAsync(new JournalEntry
            {
                Id = Guid.NewGuid(),
                Type = JournalEntryType.Income,
                Date = request.Date,
                Description = description,
                CreatedAt = now,
                ExternalOrganisationId = request.ExternalOrganisationId
            }, innerCt);

            // Taxable while tax applies: DR destination gross / CR Income net / CR Tax Collected.
            // Under Exclusive entry mode request.Amount is the net and tax is added on top (the
            // money-side line still takes the gross); under Inclusive it is the gross and tax splits out.
            // Otherwise a 2-line pair; postings while tax doesn't apply carry no tax code at all.
            var (grossAmount, incomeAmount, taxAmount) = taxCode == TaxCode.Taxable
                ? TaxCalculator.Split(request.Amount, settings?.TaxEntryMode ?? TaxEntryMode.Inclusive, settings?.TaxRate ?? 0m,
                    CurrencyCatalog.Get(settings?.CurrencyCode ?? CurrencyCatalog.Default.Code).MinorUnitDigits)
                : (request.Amount, request.Amount, 0m);

            var lines = new List<Transaction>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    Date = request.Date,
                    AccountId = destinationId,
                    DebitAmount = grossAmount,
                    CreditAmount = 0m,
                    GLAccount = destinationNumber,
                    JournalEntryId = entry.Id,
                    TaxCode = taxCode,
                    Description = description,
                    CreatedAt = now
                },
                new()
                {
                    Id = Guid.NewGuid(),
                    Date = request.Date,
                    AccountId = account.Id,
                    DebitAmount = 0m,
                    CreditAmount = incomeAmount,
                    GLAccount = account.AccountNumber,
                    JournalEntryId = entry.Id,
                    TaxCode = taxCode,
                    Description = description,
                    CreatedAt = now
                }
            };

            if (taxAmount != 0m)
            {
                lines.Add(new Transaction
                {
                    Id = Guid.NewGuid(),
                    Date = request.Date,
                    AccountId = SystemAccounts.TaxCollectedId,
                    DebitAmount = 0m,
                    CreditAmount = taxAmount,
                    GLAccount = SystemAccounts.TaxCollectedNumber,
                    JournalEntryId = entry.Id,
                    TaxCode = taxCode,
                    Description = $"Tax collected — {description}",
                    CreatedAt = now
                });
            }

            await _glRepo.AddBalancedSetAsync(lines, innerCt);

            // A cheque or other receipt waits in Undeposited Funds for its gross (research §3).
            if (createsItem)
            {
                await _receivedItemRepo.AddAsync(new ReceivedItem
                {
                    Id = Guid.NewGuid(),
                    Date = request.Date,
                    Amount = grossAmount,
                    Method = method,
                    Reference = reference,
                    JournalEntryId = entry.Id,
                    CreatedAt = now
                }, innerCt);
            }

            await _audit.LogAsync(
                nameof(JournalEntry), entry.Id, AuditAction.Create,
                oldValue: null,
                newValue: $"Other income {MoneyFormatter.Format(grossAmount)} to account '{account.Name}' received by {method} into '{destinationName}' on {request.Date:yyyy-MM-dd}",
                ct: innerCt);

        }, ct);
    }
}
