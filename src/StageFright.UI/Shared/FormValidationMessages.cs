using System.Resources;
using StageFright.Core.Modules.Localization.Resources;
using StageFright.UI.Resources.Strings;

namespace StageFright.UI.Shared;

/// <summary>
/// Translated DataAnnotations messages for the UI's form models. An attribute names a property here through
/// <c>ErrorMessageResourceType</c> / <c>ErrorMessageResourceName</c>, and DataAnnotations reads it when it
/// validates, so the text follows the current UI culture. Each property name is its resource key.
/// </summary>
public static class FormValidationMessages
{
    private static readonly ResourceManager Setup = new(typeof(SetupResource));
    private static readonly ResourceManager Shared = new(typeof(SharedResource));
    private static readonly ResourceManager Settings = new(typeof(SettingsResource));
    private static readonly ResourceManager Finance = new(typeof(FinanceResource));
    private static readonly ResourceManager Validation = new(typeof(ValidationResource));

    public static string Setup_Validation_OrgNameRequired => Get(Setup, nameof(Setup_Validation_OrgNameRequired));
    public static string Setup_Validation_OrgNameTooLong => Get(Setup, nameof(Setup_Validation_OrgNameTooLong));
    public static string Setup_Validation_AnnualFeeNotNegative => Get(Setup, nameof(Setup_Validation_AnnualFeeNotNegative));
    public static string Setup_Validation_AttendanceFeeNotNegative => Get(Setup, nameof(Setup_Validation_AttendanceFeeNotNegative));
    public static string Setup_Validation_RenewalMonthRange => Get(Setup, nameof(Setup_Validation_RenewalMonthRange));
    public static string Setup_Validation_CurrencyRequired => Get(Setup, nameof(Setup_Validation_CurrencyRequired));
    public static string Setup_Validation_FinancialYearStartMonthRange => Get(Setup, nameof(Setup_Validation_FinancialYearStartMonthRange));
    public static string Setup_Validation_FinancialYearStartDayRange => Get(Setup, nameof(Setup_Validation_FinancialYearStartDayRange));
    public static string Setup_Validation_AgmMonthRange => Get(Setup, nameof(Setup_Validation_AgmMonthRange));
    public static string Setup_Validation_TaxRatePositive => Get(Setup, nameof(Setup_Validation_TaxRatePositive));
    public static string Shared_Validation_AccountNameTooLong => Get(Shared, nameof(Shared_Validation_AccountNameTooLong));
    public static string Settings_Validation_EventTypeNameTooLong => Get(Settings, nameof(Settings_Validation_EventTypeNameTooLong));
    public static string Finance_Validation_ReconciliationNotesTooLong => Get(Finance, nameof(Finance_Validation_ReconciliationNotesTooLong));
    public static string Validation_Settings_AuditRetentionRange => Get(Validation, nameof(Validation_Settings_AuditRetentionRange));
    public static string Validation_Account_NameRequired => Get(Validation, nameof(Validation_Account_NameRequired));
    public static string Validation_EventType_NameRequired => Get(Validation, nameof(Validation_EventType_NameRequired));

    // A missing key falls back to the key itself, never a blank message.
    private static string Get(ResourceManager resources, string key) => resources.GetString(key) ?? key;
}
