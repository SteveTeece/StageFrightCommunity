using Microsoft.Extensions.Localization;
using StageFright.Core.Modules.Localization.Resources;

namespace StageFright.Core.Localization;

/// <summary>
/// Renders a user-facing enum value through the shared <c>EnumsResource</c> localizer
/// (FR-024) — the only sanctioned alternative to <c>enum.ToString()</c> at a display site. The
/// enum's name/number stays the culture-invariant identity used for storage, GL, backups,
/// sorting, comparison and <c>&lt;option value&gt;</c> / report-filter tokens; only the
/// rendered label goes through this extension.
/// </summary>
/// <remarks>
/// Resolves the active <see cref="IStringLocalizerFactory"/> from a holder set once at
/// composition-root startup (<c>MauiProgram.RunStartupSequence</c>) or by test setup (see
/// <c>LocalizedTestContext</c> in <c>StageFright.UI.Tests</c>), so a call site can render an
/// enum without injecting a localizer purely for that purpose. The factory is a process-wide
/// singleton set once before first use — consistent with this feature's process-wide culture
/// model (plan.md Decision 5) rather than swapped per request.
/// </remarks>
public static class EnumLocalizationExtensions
{
    private static IStringLocalizerFactory? _factory;

    /// <summary>Sets the factory used to resolve <c>EnumsResource</c> lookups. Call once during startup.</summary>
    public static void UseFactory(IStringLocalizerFactory factory) => _factory = factory;

    /// <summary>
    /// The <c>EnumsResource</c> key for <paramref name="value"/>:
    /// <c>Enum_&lt;TypeName&gt;_&lt;MemberName&gt;</c>. The one place this format is built — both
    /// this extension and <see cref="Localizer.Enum"/> resolve through it.
    /// </summary>
    internal static string EnumResourceKey(System.Enum value) => $"Enum_{value.GetType().Name}_{value}";

    /// <summary>
    /// Looks up <c>Enum_&lt;TypeName&gt;_&lt;MemberName&gt;</c> in <c>EnumsResource</c> through
    /// the missing-key logging decorator (FR-008/FR-009). Falls back to the raw enum name when
    /// <see cref="UseFactory"/> was never called (e.g. a test that renders an enum without
    /// wiring localization) so a forgotten wiring never throws.
    /// </summary>
    public static string LocalizeEnum(this System.Enum value)
    {
        if (_factory is null)
            return value.ToString();

        return _factory.Create(typeof(EnumsResource))[EnumResourceKey(value)];
    }
}
