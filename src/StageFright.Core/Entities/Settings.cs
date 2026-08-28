using StageFright.Core.Enums;
using StageFright.Core.Modules.Settings;

namespace StageFright.Core.Entities;

/// <summary>
/// Singleton application configuration record. Exactly one row exists after first-run setup.
/// GetAsync returns null before the setup wizard is completed; null triggers the wizard redirect.
/// </summary>
public class Settings
{
    /// <summary>Primary key (GUID). Single row; enforced by ISettingsRepository.</summary>
    public Guid Id { get; set; }

    /// <summary>Display name of the performing arts group. Required (setup wizard).</summary>
    public string OrganizationName { get; set; } = string.Empty;

    /// <summary>Annual membership fee amount. Required. Precision: decimal(18,2). Must be ≥ 0.</summary>
    public decimal AnnualFee { get; set; }

    /// <summary>Per-rehearsal attendance fee amount. Required. Precision: decimal(18,2). Must be ≥ 0.</summary>
    public decimal AttendanceFee { get; set; }

    /// <summary>Month (1–12) when membership renewals are due. Required.</summary>
    public int MembershipRenewalMonth { get; set; }

    /// <summary>
    /// Month (1–12) the AGM is normally held. Single source of truth for both committee
    /// term boundaries and (formerly) the reset reminder timing. Default: 1 (January).
    /// </summary>
    public int CommitteeRenewalMonth { get; set; } = 1;

    /// <summary>
    /// Coordinator-configured target number of general committee member seats (FR-014).
    /// Null when unset. Snapshotted onto each AnnualGeneralMeeting at save time.
    /// </summary>
    public int? GeneralCommitteeSeatCountTarget { get; set; }

    /// <summary>
    /// First month (1–12) of the financial year used by reports and FY presets.
    /// Default: 7 (Australian financial year, 1 July – 30 June).
    /// </summary>
    public int FinancialYearStartMonth { get; set; } = 7;

    /// <summary>
    /// True when sales tax applies to the organisation. When false all tax UI is
    /// hidden, postings are 2-line, and tax codes/rate stay null. Default: false.
    /// </summary>
    public bool IsTaxApplicable { get; set; }

    /// <summary>
    /// The organisation's sales tax rate, in percentage points (e.g. 10 means 10%).
    /// Present only while <see cref="IsTaxApplicable"/> is true; null otherwise.
    /// User-configurable — unlike the retired hardcoded GST rate, a rate change is a
    /// Settings change, not a code change.
    /// </summary>
    public decimal? TaxRate { get; set; }

    /// <summary>
    /// Tax treatment applied to annual fee accruals while tax applies.
    /// Null means tax-exempt (the default for NFP membership fees).
    /// </summary>
    public TaxCode? AnnualFeeTaxCode { get; set; }

    /// <summary>
    /// Tax treatment applied to attendance fee accruals while tax applies.
    /// Null means tax-exempt.
    /// </summary>
    public TaxCode? AttendanceFeeTaxCode { get; set; }

    /// <summary>Maximum member age accepted by the system (years). Default: 150.</summary>
    public int MaxAgeRangeYears { get; set; } = 150;

    /// <summary>Minimum member age accepted by the system (years). Default: 0 (no minimum).</summary>
    public int MinimumMemberAge { get; set; } = 0;

    /// <summary>Current UI colour theme preference. Default: Dark.</summary>
    public Theme Theme { get; set; } = Theme.Dark;

    /// <summary>
    /// Selected display language as a BCP-47 culture id (e.g. "en-AU", "en-US"). Null until the
    /// user explicitly chooses one; null resolves at startup to the operating-system display
    /// language when a matching resource set ships, otherwise Australian English (FR-023).
    /// Presentation only — changing it never alters any other stored value or GL balance (FR-016).
    /// </summary>
    public string? LanguageCode { get; set; }

    /// <summary>
    /// When true, the Rehearsals and Events dashboard tiles display a year-to-date
    /// participation bar chart in addition to the most-recent doughnut chart.
    /// Default: true.
    /// </summary>
    public bool ShowParticipationGraphs { get; set; } = true;

    /// <summary>Semver schema version recorded by migrations and backup manifests (NFR-002).</summary>
    public string SchemaVersion { get; set; } = "1.1.0";

    /// <summary>
    /// Number of years audit trail entries are retained before the startup purge hard-deletes
    /// them. Range: 1–7. Default: 1 year.
    /// </summary>
    public int AuditRetentionYears { get; set; } = 1;

    // --- Soft-delete fields (never set; singleton row) ---

    /// <summary>Reserved; always false for the Settings singleton.</summary>
    public bool IsDeleted { get; set; }

    /// <summary>Reserved; always null for the Settings singleton.</summary>
    public DateTime? DeletedAt { get; set; }

    /// <summary>Reserved; always null for the Settings singleton.</summary>
    public string? DeletedBy { get; set; }

    // --- Audit fields ---

    /// <summary>UTC timestamp when the record was created (first-run setup).</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>UTC timestamp of the most recent update.</summary>
    public DateTime UpdatedAt { get; set; }
}
