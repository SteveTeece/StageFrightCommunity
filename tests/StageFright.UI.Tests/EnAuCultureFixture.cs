using System.Globalization;
using StageFright.UI.Tests;

[assembly: AssemblyFixture(typeof(EnAuCultureFixture))]

namespace StageFright.UI.Tests;

/// <summary>
/// Runs every UI test in en-AU (the neutral resources) whatever the machine's language, so assertions on the
/// English text hold everywhere. It sets only the process-wide defaults, as the app does (spec 029): the other
/// test projects use xunit.runner.json's "culture", but that pins a per-context CurrentCulture override, which
/// would shadow the globals and defeat CultureProviderTests' check that a language switch is never pinned.
/// </summary>
public sealed class EnAuCultureFixture
{
    public EnAuCultureFixture()
    {
        var enAu = CultureInfo.GetCultureInfo("en-AU");
        CultureInfo.DefaultThreadCurrentCulture = enAu;
        CultureInfo.DefaultThreadCurrentUICulture = enAu;
    }
}
