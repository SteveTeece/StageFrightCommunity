using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Exceptions;
using StageFright.Core.Localization;
using StageFright.Core.Modules.Events;
using StageFright.Core.Modules.Finance;
using StageFright.Core.Modules.Localization.Resources;
using SettingsEntity = StageFright.Core.Entities.Settings;

namespace StageFright.Core.Modules.Settings;

/// <summary>
/// Handles first-run initialization. Validates the setup request, creates the Settings singleton,
/// seeds default event types, and audits the event.
/// System accounts (Cash/MemberReceivable/BadDebtExpense) are seeded by EF migrations.
/// </summary>
public class SetupService : ISetupService
{
    private readonly ISettingsRepository _settingsRepo;
    private readonly IAccountRepository _accountRepo;
    private readonly IEventTypeRepository _eventTypeRepo;
    private readonly ICommitteeOfficeHolderTypeService _officeHolderTypeService;
    private readonly IAccountService _accountService;
    private readonly IOpeningBalanceService _openingBalanceService;
    private readonly IAuditTrailService _audit;
    private readonly ILocalizer _localizer;

    public SetupService(
        ISettingsRepository settingsRepo,
        IAccountRepository accountRepo,
        IEventTypeRepository eventTypeRepo,
        ICommitteeOfficeHolderTypeService officeHolderTypeService,
        IAccountService accountService,
        IOpeningBalanceService openingBalanceService,
        IAuditTrailService audit,
        ILocalizer localizer)
    {
        _settingsRepo = settingsRepo;
        _accountRepo = accountRepo;
        _eventTypeRepo = eventTypeRepo;
        _officeHolderTypeService = officeHolderTypeService;
        _accountService = accountService;
        _openingBalanceService = openingBalanceService;
        _audit = audit;
        _localizer = localizer;
    }

    public async Task<bool> IsSetupCompleteAsync(CancellationToken ct = default)
    {
        var settings = await _settingsRepo.GetAsync(ct);
        return settings is not null;
    }

    public async Task InitializeAsync(SetupRequest request, CancellationToken ct = default)
    {
        var existing = await _settingsRepo.GetAsync(ct);
        if (existing is not null)
            throw new ValidationException(_localizer.Get<ValidationResource>("Validation_Setup_AlreadyCompleted"), "Settings", nameof(InitializeAsync));

        Validate(request);

        // Tax codes and rate only ever apply while tax is applicable — force them null
        // otherwise, regardless of what the wizard happened to have selected before the
        // user toggled tax applicability off.
        var taxRate = request.IsTaxApplicable ? request.TaxRate : null;
        var annualFeeTaxCode = request.IsTaxApplicable ? request.AnnualFeeTaxCode : null;
        var attendanceFeeTaxCode = request.IsTaxApplicable ? request.AttendanceFeeTaxCode : null;
        var taxEntryMode = request.IsTaxApplicable ? request.TaxEntryMode : TaxEntryMode.Inclusive;

        var settings = new SettingsEntity
        {
            Id = Guid.NewGuid(),
            OrganizationName = request.OrganizationName.Trim(),
            AnnualFee = request.AnnualFee,
            AttendanceFee = request.AttendanceFee,
            MembershipRenewalMonth = request.MembershipRenewalMonth,
            IsTaxApplicable = request.IsTaxApplicable,
            TaxRate = taxRate,
            AnnualFeeTaxCode = annualFeeTaxCode,
            AttendanceFeeTaxCode = attendanceFeeTaxCode,
            TaxEntryMode = taxEntryMode,
            CommitteeRenewalMonth = request.CommitteeRenewalMonth,
            GeneralCommitteeSeatCountTarget = request.GeneralCommitteeSeatCountTarget,
            AuditRetentionYears = request.AuditRetentionYears,
            CurrencyCode = CurrencyCatalog.Get(request.CurrencyCode).Code,
            FinancialYearStartMonth = request.FinancialYearStartMonth,
            FinancialYearStartDay = request.FinancialYearStartDay,
            InceptionDate = request.InceptionDate?.Date,
            MaxAgeRangeYears = 150,
            MinimumMemberAge = 0,
            Theme = request.Theme,
            LanguageCode = request.LanguageCode,
            SchemaVersion = "1.1.0",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _settingsRepo.SaveAsync(settings, ct);

        await SeedDefaultEventTypesAsync(ct);

        // Chart of Accounts entries queued during setup (FR-013) are created here, together
        // with the rest of setup, so Finish stays one submission (FR-008). ClientId->real
        // Account.Id is tracked so a queued account's opening balance (below) can be
        // resolved to a real AccountId even though it didn't exist when it was queued.
        var clientIdToAccountId = new Dictionary<Guid, Guid>();
        if (request.QueuedAccounts is { Count: > 0 })
        {
            foreach (var queuedAccount in request.QueuedAccounts)
            {
                var created = await _accountService.CreateAsync(
                    queuedAccount.Name, queuedAccount.Type, queuedAccount.IsBankAccount, ct);
                clientIdToAccountId[queuedAccount.ClientId] = created.Id;
            }
        }

        // Opening balances queued during setup (FR-018) post together as one journal entry,
        // after any queued accounts above so a queued account's ClientId can already resolve.
        if (request.QueuedOpeningBalances is { Count: > 0 })
        {
            var resolvedEntries = request.QueuedOpeningBalances
                .Select(entry => new OpeningBalanceEntry
                {
                    AccountId = clientIdToAccountId.TryGetValue(entry.AccountId, out var realId)
                        ? realId
                        : entry.AccountId,
                    Amount = entry.Amount
                })
                .ToList();

            await _openingBalanceService.RecordOpeningBalancesAsync(new RecordOpeningBalancesRequest
            {
                AsAtDate = request.OpeningBalanceAsAtDate,
                Entries = resolvedEntries
            }, ct);
        }

        // Committee configuration is optional at every default (FR-021) — coordinators who
        // skip this step get no custom titles, identical to configuring none from Settings.
        if (request.CommitteeOfficeHolderTitles is { Count: > 0 })
        {
            foreach (var title in request.CommitteeOfficeHolderTitles)
                await _officeHolderTypeService.AddAsync(title, ct);
        }

        await _audit.LogAsync(
            entityType: "Settings",
            entityId: settings.Id,
            action: AuditAction.Create,
            newValue: request.OrganizationName,
            ct: ct);
    }

    private async Task SeedDefaultEventTypesAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        foreach (var name in EventTypeService.GetDefaultEventTypeNames())
        {
            var eventType = new EventType
            {
                Id = Guid.NewGuid(),
                Name = name,
                IsSystemDefault = true,
                CreatedAt = now,
                UpdatedAt = now
            };
            await _eventTypeRepo.AddAsync(eventType, ct);
        }
    }

    private void Validate(SetupRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.OrganizationName))
            throw new ValidationException(_localizer.Get<ValidationResource>("Validation_Setup_OrganisationNameRequired"), "Settings", nameof(InitializeAsync));

        if (request.IsTaxApplicable && request.TaxRate is not (> 0))
            throw new ValidationException(_localizer.Get<ValidationResource>("Validation_Settings_TaxRateRequired"), "Settings", nameof(InitializeAsync));

        if (request.AnnualFee < 0)
            throw new ValidationException(_localizer.Get<ValidationResource>("Validation_Setup_AnnualFeeNegative"), "Settings", nameof(InitializeAsync));

        if (request.AttendanceFee < 0)
            throw new ValidationException(_localizer.Get<ValidationResource>("Validation_Setup_AttendanceFeeNegative"), "Settings", nameof(InitializeAsync));

        if (request.MembershipRenewalMonth < 1 || request.MembershipRenewalMonth > 12)
            throw new ValidationException(_localizer.Get<ValidationResource>("Validation_Setup_MembershipRenewalMonthRange"), "Settings", nameof(InitializeAsync));

        if (request.AuditRetentionYears < 1 || request.AuditRetentionYears > 7)
            throw new ValidationException(_localizer.Get<ValidationResource>("Validation_Settings_AuditRetentionRange"), "Settings", nameof(InitializeAsync));

        if (!CurrencyCatalog.TryGet(request.CurrencyCode, out _))
            throw new ValidationException(_localizer.Get<ValidationResource>("Validation_Setup_CurrencyUnknown"), "Settings", nameof(InitializeAsync));

        if (request.FinancialYearStartDay < 1 || request.FinancialYearStartDay > 28)
            throw new ValidationException(_localizer.Get<ValidationResource>("Validation_Setup_FinancialYearStartDayRange"), "Settings", nameof(InitializeAsync));
    }
}
