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
    /// <summary>Chart of Accounts entries queued during setup (spec 017 FR-012/FR-013),
    /// created together with the rest of setup at Finish.</summary>
    IReadOnlyList<QueuedAccountRequest>? QueuedAccounts = null,
    /// <summary>Opening balance entries queued during setup (FR-017/FR-018). An entry's
    /// <c>AccountId</c> is either a real existing account or a <see cref="QueuedAccountRequest.ClientId"/>
    /// that <see cref="SetupService"/> resolves to the real id it assigns.</summary>
    IReadOnlyList<OpeningBalanceEntry>? QueuedOpeningBalances = null,
    /// <summary>The date queued opening balances are recorded as at. Ignored when
    /// <see cref="QueuedOpeningBalances"/> is empty.</summary>
    DateTime OpeningBalanceAsAtDate = default);
