namespace StageFright.Core.Modules.Localization.Resources;

/// <summary>
/// Marker type that locates <c>EnumsResource.resx</c> via <see cref="Microsoft.Extensions.Localization.IStringLocalizer{EnumsResource}"/>.
/// Contains no members — the neutral (en-AU) <c>.resx</c> beside this file holds every
/// user-facing enum member's display text, keyed <c>Enum_&lt;EnumTypeName&gt;_&lt;MemberName&gt;</c>
/// (FR-024). Shared across <c>StageFright.UI</c> and <c>StageFright.Reports</c> so a status reads
/// identically on screen and in a printed report. Resolved through <see cref="StageFright.Core.Localization.EnumLocalizationExtensions.LocalizeEnum"/>,
/// never <c>enum.ToString()</c>, at any display site.
/// </summary>
public class EnumsResource
{
}
