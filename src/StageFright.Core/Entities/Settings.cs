using StageFright.Core.Enums;

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

    /// <summary>Month (1–12) when committee positions are reviewed. Default: 1 (January).</summary>
    public int CommitteeRenewalMonth { get; set; } = 1;

    /// <summary>Maximum member age accepted by the system (years). Default: 150.</summary>
    public int MaxAgeRangeYears { get; set; } = 150;

    /// <summary>Minimum member age accepted by the system (years). Default: 0 (no minimum).</summary>
    public int MinimumMemberAge { get; set; } = 0;

    /// <summary>Current UI colour theme preference. Default: Light.</summary>
    public Theme Theme { get; set; } = Theme.Light;

    /// <summary>
    /// When true, the Rehearsals and Events dashboard tiles display a year-to-date
    /// participation bar chart in addition to the most-recent doughnut chart.
    /// Default: true.
    /// </summary>
    public bool ShowParticipationGraphs { get; set; } = true;

    /// <summary>
    /// The calendar year for which the annual committee reset was last performed.
    /// Null until the first reset. Used to drive the AGM reminder banner (FR-031).
    /// </summary>
    public int? LastCommitteeResetYear { get; set; }

    /// <summary>Semver schema version recorded by migrations and backup manifests (NFR-002).</summary>
    public string SchemaVersion { get; set; } = "1.0.0";

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
