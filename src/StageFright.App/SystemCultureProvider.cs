using System.Globalization;
using StageFright.Core.Contracts;

namespace StageFright.App;

/// <summary>
/// Reads the operating-system / device display (UI) culture for <see cref="ILanguageProvider"/>'s
/// FR-023 resolution ladder. Uses <see cref="CultureInfo.InstalledUICulture"/> — the OS UI
/// language MAUI itself derives the device culture from — and never throws: any failure yields
/// <see cref="CultureInfo.InvariantCulture"/>, which <see cref="ILanguageProvider"/> then treats
/// as "no OS match" and falls back to <c>en-AU</c>.
/// </summary>
public sealed class SystemCultureProvider : ISystemCultureProvider
{
    public CultureInfo GetUiCulture()
    {
        try
        {
            return CultureInfo.InstalledUICulture ?? CultureInfo.InvariantCulture;
        }
        catch
        {
            return CultureInfo.InvariantCulture;
        }
    }
}
