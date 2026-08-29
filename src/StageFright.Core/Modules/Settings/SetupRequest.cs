using StageFright.Core.Enums;
using StageFright.Core.Modules.Finance;

namespace StageFright.Core.Modules.Settings;

/// <summary>Input data for the first-run setup wizard.</summary>
public record SetupRequest(
    string OrganizationName,
    decimal AnnualFee,
    decimal AttendanceFee,
    int MembershipRenewalMonth,
    bool IsTaxApplicable,
    decimal? TaxRate,
    TaxCode? AnnualFeeTaxCode,
    TaxCode? AttendanceFeeTaxCode,
    Theme Theme,
    int CommitteeRenewalMonth = 1,
    IReadOnlyList<string>? CommitteeOfficeHolderTitles = null,
    int? GeneralCommitteeSeatCountTarget = null,
    int AuditRetentionYears = 1,
    // QueuedAccounts: Chart of Accounts entries queued during setup (spec 017 FR-012/FR-013),
    // created together with the rest of setup at Finish.
    IReadOnlyList<QueuedAccountRequest>? QueuedAccounts = null,
    // QueuedOpeningBalances: opening balance entries queued during setup (FR-017/FR-018). An
    // entry's AccountId is either a real existing account or a QueuedAccountRequest.ClientId
    // that SetupService resolves to the real id it assigns.
    IReadOnlyList<OpeningBalanceEntry>? QueuedOpeningBalances = null,
    // OpeningBalanceAsAtDate: the date queued opening balances are recorded as at. Ignored
    // when QueuedOpeningBalances is empty.
    DateTime OpeningBalanceAsAtDate = default,
    // LanguageCode: chosen display language as a BCP-47 culture id (spec 027, US3 / FR-013).
    // Null keeps Settings.LanguageCode null ("follow the OS display language").
    string? LanguageCode = null,
    // CurrencyCode: ISO 4217 code the organisation keeps its books in (spec 028, FR-001).
    // Defaults to "AUD"; validated against CurrencyCatalog.All and fixed after setup (FR-002).
    string CurrencyCode = "AUD",
    // FinancialYearStartMonth / FinancialYearStartDay: the (month, day) the financial year
    // opens on (spec 028, US7 / FR-019, FR-020). Chosen explicitly during first-run setup;
    // defaults reproduce the Australian FY (1 July). Day is validated to 1..28.
    int FinancialYearStartMonth = 7,
    int FinancialYearStartDay = 1);
