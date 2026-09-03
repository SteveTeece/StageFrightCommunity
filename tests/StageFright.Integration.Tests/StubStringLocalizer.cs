using System.Globalization;
using Microsoft.Extensions.Localization;

namespace StageFright.Integration.Tests;

/// <summary>
/// Test double for <see cref="IStringLocalizer{T}"/> that echoes the requested key as its own
/// value. Lets scenario tests construct services whose constructor now takes a localizer
/// (spec 027) without wiring real <c>.resx</c> lookups — those tests assert behaviour, not
/// message wording.
/// </summary>
internal sealed class StubStringLocalizer<T> : IStringLocalizer<T>
{
    public LocalizedString this[string name] => new(name, name, resourceNotFound: false);

    public LocalizedString this[string name, params object[] arguments] =>
        new(name, string.Format(CultureInfo.CurrentCulture, name, arguments), resourceNotFound: false);

    public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
}
