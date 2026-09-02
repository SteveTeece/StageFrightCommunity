using Microsoft.Extensions.Localization;
using StageFright.Core.Localization;

namespace StageFright.Core.Tests.Localization;

/// <summary>
/// Tests for <see cref="Localizer"/>'s named-placeholder substitution (spec 027, FR-010). Each
/// distinct <c>{Token}</c> binds to an arg by the order the token first appears in the resolved
/// text; every occurrence of that token then renders the bound value, so a token repeated in the
/// text renders its value each time instead of leaking the literal <c>{Token}</c>.
/// </summary>
public sealed class LocalizerTests
{
    private static string Format(string template, params object[] args) =>
        new Localizer(new TemplateFactory(template)).Get<object>("AnyKey", args);

    [Fact]
    public void Should_BindEachDistinctToken_ByFirstAppearanceOrder()
    {
        Assert.Equal("Bad 5 for Alice", Format("Bad {Amount} for {Member}", 5, "Alice"));
    }

    [Fact]
    public void Should_RenderARepeatedToken_WithItsBoundValue_NotTheLiteral()
    {
        // Regression: the earlier positional walk emitted the literal "{Name}" for the 2nd
        // occurrence once its single arg had been consumed.
        Assert.Equal("Hi Sam, bye Sam", Format("Hi {Name}, bye {Name}", "Sam"));
    }

    [Fact]
    public void Should_ReturnTemplateUnchanged_When_NoArgsSupplied()
    {
        Assert.Equal("Bad {Amount}", Format("Bad {Amount}"));
    }

    [Fact]
    public void Should_LeaveFurtherDistinctTokensLiteral_When_ArgsAreExhausted()
    {
        Assert.Equal("5 then {Second}", Format("{First} then {Second}", 5));
    }

    private sealed class TemplateLocalizer : IStringLocalizer
    {
        private readonly string _template;

        public TemplateLocalizer(string template) => _template = template;

        public LocalizedString this[string name] => new(name, _template, resourceNotFound: false);

        public LocalizedString this[string name, params object[] arguments] =>
            new(name, _template, resourceNotFound: false);

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }

    private sealed class TemplateFactory : IStringLocalizerFactory
    {
        private readonly string _template;

        public TemplateFactory(string template) => _template = template;

        public IStringLocalizer Create(Type resourceSource) => new TemplateLocalizer(_template);

        public IStringLocalizer Create(string baseName, string location) => new TemplateLocalizer(_template);
    }
}
