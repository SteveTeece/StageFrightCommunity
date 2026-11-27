using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Reflection;
using StageFright.UI.Pages.Setup;
using StageFright.UI.Shared;

namespace StageFright.UI.Tests.Shared;

/// <summary>
/// The form models' DataAnnotations messages come from the resources, in the current UI culture,
/// instead of English literals.
/// </summary>
public class FormValidationMessagesTests
{
    private static List<string> Validate(object model, string culture)
    {
        var previous = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
        try
        {
            var results = new List<ValidationResult>();
            Validator.TryValidateObject(model, new ValidationContext(model), results, validateAllProperties: true);
            return results.Select(r => r.ErrorMessage ?? string.Empty).ToList();
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }

    [Fact]
    public void Should_ShowEnglishMessages_When_SetupModelInvalidUnderAustralianEnglish()
    {
        var errors = Validate(new SetupFormModel { OrganizationName = null, AnnualFee = -1m }, "en-AU");

        Assert.Contains("Organisation name is required.", errors);
        Assert.Contains("Annual fee must be zero or greater.", errors);
    }

    // DataAnnotations runs the model's own Validate only once every field passes, so the tax-rate rule is tested alone.
    [Theory]
    [InlineData("en-AU", "Tax rate must be greater than zero.")]
    [InlineData("de-DE", "Der Steuersatz muss größer als null sein.")]
    public void Should_ShowTranslatedTaxRateMessage_When_TaxAppliesWithZeroRate(string culture, string expected)
    {
        var errors = Validate(new SetupFormModel { OrganizationName = "Choir", IsTaxApplicable = true, TaxRate = 0m }, culture);

        Assert.Equal([expected], errors);
    }

    [Fact]
    public void Should_ShowGermanMessages_When_SetupModelInvalidUnderGerman()
    {
        var errors = Validate(new SetupFormModel { OrganizationName = null, AnnualFee = -1m }, "de-DE");

        Assert.Contains("Der Organisationsname ist erforderlich.", errors);
        Assert.Contains("Der Jahresbeitrag muss null oder größer sein.", errors);
        Assert.DoesNotContain(errors, e => e.Contains("required", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Should_ShowJapaneseMessage_When_AccountNameMissingUnderJapanese()
    {
        var errors = Validate(new NewAccountModel { Name = null }, "ja-JP");

        Assert.Equal(["勘定科目名を入力してください。"], errors);
    }

    [Theory]
    [InlineData("en-AU")]
    [InlineData("de-DE")]
    [InlineData("ja-JP")]
    public void Should_ResolveEveryMessageFromResources_When_PropertiesRead(string culture)
    {
        var previous = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
        try
        {
            foreach (var property in typeof(FormValidationMessages).GetProperties(BindingFlags.Public | BindingFlags.Static))
            {
                var value = (string)property.GetValue(null)!;
                Assert.False(string.IsNullOrWhiteSpace(value), property.Name);
                Assert.NotEqual(property.Name, value);
            }
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }
}
