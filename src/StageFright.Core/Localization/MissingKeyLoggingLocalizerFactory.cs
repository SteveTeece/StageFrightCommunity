using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;

namespace StageFright.Core.Localization;

/// <summary>
/// Decorates the default <see cref="IStringLocalizerFactory"/> so every lookup whose
/// <see cref="LocalizedString.ResourceNotFound"/> comes back <c>true</c> is logged as a warning
/// before the neutral (Australian English) value is returned to the caller (FR-008/FR-009).
/// Registered in the composition root (<c>MauiProgram.RegisterCoreServices</c>) in place of the
/// factory <c>AddLocalization()</c> adds, so every <c>IStringLocalizer&lt;T&gt;</c> resolved by
/// the container — and every <see cref="ILocalizer"/> lookup — passes through this decorator.
/// </summary>
public class MissingKeyLoggingLocalizerFactory : IStringLocalizerFactory
{
    private readonly IStringLocalizerFactory _inner;
    private readonly ILogger<MissingKeyLoggingLocalizerFactory> _logger;

    public MissingKeyLoggingLocalizerFactory(IStringLocalizerFactory inner, ILogger<MissingKeyLoggingLocalizerFactory> logger)
    {
        _inner = inner;
        _logger = logger;
    }

    public IStringLocalizer Create(Type resourceSource) =>
        new LoggingStringLocalizer(_inner.Create(resourceSource), _logger, resourceSource.FullName ?? resourceSource.Name);

    public IStringLocalizer Create(string baseName, string location) =>
        new LoggingStringLocalizer(_inner.Create(baseName, location), _logger, baseName);

    /// <summary>
    /// Wraps one resolved <see cref="IStringLocalizer"/> so every indexer lookup that comes back
    /// with <see cref="LocalizedString.ResourceNotFound"/> set logs a warning naming the key, the
    /// owning resource, and the active UI culture before the neutral fallback value is returned.
    /// </summary>
    private sealed class LoggingStringLocalizer : IStringLocalizer
    {
        private readonly IStringLocalizer _inner;
        private readonly ILogger _logger;
        private readonly string _resourceName;

        public LoggingStringLocalizer(IStringLocalizer inner, ILogger logger, string resourceName)
        {
            _inner = inner;
            _logger = logger;
            _resourceName = resourceName;
        }

        public LocalizedString this[string name] => LogIfMissing(_inner[name]);

        public LocalizedString this[string name, params object[] arguments] => LogIfMissing(_inner[name, arguments]);

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => _inner.GetAllStrings(includeParentCultures);

        private LocalizedString LogIfMissing(LocalizedString value)
        {
            if (value.ResourceNotFound)
            {
                _logger.LogWarning(
                    "Missing localization key {Key} in {Resource} for culture {Culture}; fell back to neutral",
                    value.Name, _resourceName, System.Globalization.CultureInfo.CurrentUICulture.Name);
            }

            return value;
        }
    }
}
