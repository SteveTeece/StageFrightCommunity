using System.Linq.Expressions;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using StageFright.UI.Shared;

namespace StageFright.UI.Tests.Shared;

/// <summary>
/// bUnit tests for AbnInput: display grouping, plain-digit binding, paste handling,
/// truncation beyond 11 digits, and EditContext wiring (inherited from InputText).
/// </summary>
public class AbnInputTests : BunitContext
{
    private sealed class Model
    {
        public string? Abn { get; set; }
    }

    private IRenderedComponent<AbnInput> RenderInput(Model model, string? value, EventCallback<string?> valueChanged)
    {
        var editContext = new EditContext(model);
        return Render<AbnInput>(p => p
            .AddCascadingValue(editContext)
            .Add(x => x.Value, value)
            .Add(x => x.ValueChanged, valueChanged)
            .Add(x => x.ValueExpression, (Expression<Func<string?>>)(() => model.Abn)));
    }

    [Fact]
    public void Value_RendersGroupedAsXxXxxXxxXxx()
    {
        var model = new Model();
        var cut = RenderInput(model, "51824753556", EventCallback<string?>.Empty);

        var input = cut.Find("input");
        Assert.Equal("51 824 753 556", input.GetAttribute("value"));
    }

    [Fact]
    public void ChangeEvent_BindsPlainDigitsOnly_WhenTypedWithSpaces()
    {
        string? bound = null;
        var model = new Model();
        var cut = RenderInput(model, null, EventCallback.Factory.Create<string?>(this, v => bound = v));

        cut.Find("input").Change("51 824 753 556");

        Assert.Equal("51824753556", bound);
    }

    [Fact]
    public void PastedFormattedValue_ParsesToPlainDigits()
    {
        string? bound = null;
        var model = new Model();
        var cut = RenderInput(model, null, EventCallback.Factory.Create<string?>(this, v => bound = v));

        cut.Find("input").Change("51-824-753-556");

        Assert.Equal("51824753556", bound);
    }

    [Fact]
    public void InputBeyond11Digits_IsTruncated()
    {
        string? bound = null;
        var model = new Model();
        var cut = RenderInput(model, null, EventCallback.Factory.Create<string?>(this, v => bound = v));

        cut.Find("input").Change("518247535567890");

        Assert.Equal("51824753556", bound);
    }

    [Fact]
    public void ChangeEvent_MarksFieldAsModified_InEditContext()
    {
        var model = new Model();
        var editContext = new EditContext(model);
        var cut = Render<AbnInput>(p => p
            .AddCascadingValue(editContext)
            .Add(x => x.Value, (string?)null)
            .Add(x => x.ValueChanged, EventCallback.Factory.Create<string?>(this, v => model.Abn = v))
            .Add(x => x.ValueExpression, (Expression<Func<string?>>)(() => model.Abn)));

        cut.Find("input").Change("51824753556");

        Assert.True(editContext.IsModified(FieldIdentifier.Create(() => model.Abn)));
    }
}
