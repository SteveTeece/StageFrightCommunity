namespace StageFright.Core.Modules.Localization.Resources;

/// <summary>
/// Marker type that locates <c>SystemAccountsResource.resx</c> via
/// <see cref="Microsoft.Extensions.Localization.IStringLocalizer{SystemAccountsResource}"/>.
/// Contains no members — the neutral (en-AU) <c>.resx</c> beside this file holds the display name
/// of every built-in system account (Petty Cash, Undeposited Funds, Member Receivable, …), keyed
/// <c>SystemAccount_Name_&lt;Account&gt;</c> (issue #394). The names are written to the accounts by
/// <see cref="StageFright.Core.Modules.Finance.SystemAccountNameService"/> whenever a language is
/// applied, so every screen, report and export shows them in the language in use.
/// </summary>
public class SystemAccountsResource
{
}
