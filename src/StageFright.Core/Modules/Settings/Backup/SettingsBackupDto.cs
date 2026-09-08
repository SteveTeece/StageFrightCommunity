using System.ComponentModel;
using ProtoBuf;
using StageFright.Core.Enums;

namespace StageFright.Core.Modules.Settings.Backup;

/// <summary>Backup DTO mirroring the Settings singleton entity 1:1.</summary>
[ProtoContract]
public class SettingsBackupDto
{
    [ProtoMember(1)] public Guid Id { get; set; }
    [ProtoMember(2)] public string OrganizationName { get; set; } = string.Empty;
    [ProtoMember(3)] public decimal AnnualFee { get; set; }
    [ProtoMember(4)] public decimal AttendanceFee { get; set; }
    [ProtoMember(5)] public int MembershipRenewalMonth { get; set; }
    [ProtoMember(6)] public int CommitteeRenewalMonth { get; set; }
    [ProtoMember(7)] public int MaxAgeRangeYears { get; set; }
    [ProtoMember(8)] public int MinimumMemberAge { get; set; }
    [ProtoMember(9)] public Theme Theme { get; set; }
    [ProtoMember(10)] public int FinancialYearStartMonth { get; set; } = 7;
    [ProtoMember(11)] public string SchemaVersion { get; set; } = "1.0.0";
    [ProtoMember(12)] public bool IsDeleted { get; set; }
    [ProtoMember(13)] public DateTime? DeletedAt { get; set; }
    [ProtoMember(14)] public string? DeletedBy { get; set; }
    [ProtoMember(15)] public DateTime CreatedAt { get; set; }
    [ProtoMember(16)] public DateTime UpdatedAt { get; set; }
    [ProtoMember(17)] public int? GeneralCommitteeSeatCountTarget { get; set; }
    [ProtoMember(18)] public int AuditRetentionYears { get; set; } = 1;
    [ProtoMember(19)] public int FinancialYearStartDay { get; set; } = 1;
    [ProtoMember(20)] public string CurrencyCode { get; set; } = "AUD";
    [ProtoMember(21)] public DateTime? ClosedThroughDate { get; set; }
    [ProtoMember(22)] public DateTime? InceptionDate { get; set; }
    [ProtoMember(23)] public bool IsTaxApplicable { get; set; }
    [ProtoMember(24)] public decimal? TaxRate { get; set; }
    [ProtoMember(25)] public TaxCode? AnnualFeeTaxCode { get; set; }
    [ProtoMember(26)] public TaxCode? AttendanceFeeTaxCode { get; set; }
    [ProtoMember(27)] public TaxEntryMode TaxEntryMode { get; set; }
    [ProtoMember(28)] public string? LanguageCode { get; set; }
    [ProtoMember(29)][DefaultValue(true)] public bool ShowParticipationGraphs { get; set; } = true;
}
