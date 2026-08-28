using System.Globalization;

namespace StageFright.Core.Contracts;

/// <summary>
/// Reads the operating-system / device display (UI) culture. A one-line App-layer seam so
/// <see cref="ILanguageProvider"/>'s FR-023 resolution ladder is unit-testable without MAUI.
/// Returns <see cref="CultureInfo.InvariantCulture"/> when the platform culture cannot be
/// determined — <see cref="ILanguageProvider"/> then falls back to <c>en-AU</c>.
/// </summary>
public interface ISystemCultureProvider
{
    /// <summary>The OS/device display language, or <see cref="CultureInfo.InvariantCulture"/> if unknown.</summary>
    CultureInfo GetUiCulture();
}
