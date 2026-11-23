using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using StageFright.UI.Pages.Setup;
using StageFright.UI.Pages.Setup.Tabs;

namespace StageFright.UI.Tests.Pages.Setup.Tabs;

/// <summary>bUnit tests for AccountingTab (spec 031, US2) — every money-related setup field on
/// one tab, with the hosted SalesTaxTab's detail fields hidden until sales tax is switched on.
/// Rendered inside an EditForm (built manually — EditForm.ChildContent is generic) so the
/// Input* components get a cascaded EditContext.</summary>
public class AccountingTabTests : LocalizedTestContext
{
    private EditContext? _editContext;

    private IRenderedComponent<EditForm> RenderInForm(SetupFormModel model) =>
        Render<EditForm>(p => p
            .Add(f => f.Model, model)
            .Add(f => f.ChildContent, (RenderFragment<EditContext>)(ctx => builder =>
            {
                _editContext = ctx;
                builder.OpenComponent<DataAnnotationsValidator>(0);
                builder.CloseComponent();
                builder.OpenComponent<AccountingTab>(1);
                builder.AddComponentParameter(2, nameof(AccountingTab.Model), model);
                builder.CloseComponent();
            })));

    [Theory]
    [InlineData("#setup-currency")]
    [InlineData("#setup-fy-start-month")]
    [InlineData("#setup-fy-start-day")]
    [InlineData("#setup-inception-date")]
    [InlineData("#annualFee")]
    [InlineData("#attendanceFee")]
    [InlineData("#taxApplicable")]
    public void Should_RenderAccountingField_When_TabRendered(string selector)
    {
        var cut = RenderInForm(new SetupFormModel());

        Assert.Single(cut.FindAll(selector));
    }

    [Theory]
    [InlineData("#orgName")]
    [InlineData("#themeSelect")]
    [InlineData("#renewalMonth")]
    [InlineData("#auditRetentionYears")]
    public void Should_NotRenderOrganisationField_When_TabRendered(string selector)
    {
        var cut = RenderInForm(new SetupFormModel());

        Assert.Empty(cut.FindAll(selector));
    }

    [Fact]
    public void Should_HideTaxDetailFields_When_SalesTaxIsOff()
    {
        var cut = RenderInForm(new SetupFormModel());

        Assert.Empty(cut.FindAll("#taxRate"));
        Assert.Empty(cut.FindAll("#taxEntryMode"));
        Assert.Empty(cut.FindAll("#annualFeeTaxCode"));
        Assert.Empty(cut.FindAll("#attendanceFeeTaxCode"));
    }

    [Fact]
    public void Should_ShowTaxDetailFields_When_SalesTaxSwitchedOn()
    {
        var cut = RenderInForm(new SetupFormModel());

        cut.Find("#taxApplicable").Change(true);

        Assert.Single(cut.FindAll("#taxRate"));
        Assert.Single(cut.FindAll("#taxEntryMode"));
        Assert.Single(cut.FindAll("#annualFeeTaxCode"));
        Assert.Single(cut.FindAll("#attendanceFeeTaxCode"));
    }

    [Fact]
    public void Should_BindFeesAndCurrency_When_Changed()
    {
        var model = new SetupFormModel();
        var cut = RenderInForm(model);

        cut.Find("#setup-currency").Change("NZD");
        cut.Find("#annualFee").Change("80");
        cut.Find("#attendanceFee").Change("6");

        Assert.Equal("NZD", model.CurrencyCode);
        Assert.Equal(80m, model.AnnualFee);
        Assert.Equal(6m, model.AttendanceFee);
    }

    [Fact]
    public async Task Should_FailValidation_When_SalesTaxOnAndRateBlank()
    {
        var model = new SetupFormModel { OrganizationName = "My Choir" };
        var cut = RenderInForm(model);

        cut.Find("#taxApplicable").Change(true);

        var valid = await cut.InvokeAsync(() => _editContext!.Validate());

        Assert.False(valid);
        Assert.Contains(_editContext!.GetValidationMessages(), m => m.Contains("Tax rate"));
    }

    [Fact]
    public async Task Should_PassValidation_When_SalesTaxOff()
    {
        var model = new SetupFormModel { OrganizationName = "My Choir" };
        var cut = RenderInForm(model);

        var valid = await cut.InvokeAsync(() => _editContext!.Validate());

        Assert.True(valid);
    }
}
