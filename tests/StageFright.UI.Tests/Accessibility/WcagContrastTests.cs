namespace StageFright.UI.Tests.Accessibility;

/// <summary>
/// WCAG AA contrast ratio tests for all app-defined colour tokens.
/// Verifies that each colour passes the 4.5:1 minimum when paired with
/// the text colour used against it in the UI.
/// </summary>
public class WcagContrastTests
{
    private const double WcagAaMinimum = 4.5;

    // Finance balance colours (used as backgrounds with dark text)
    [Fact]
    public void FinanceGreen_HSL120_35_70_WithDarkText_PassesWcagAA()
    {
        var bg = HslToRgb(120, 35, 70);
        var text = (R: 26, G: 26, B: 26); // #1a1a1a — dark text on badge
        var ratio = ContrastRatio(bg, text);
        Assert.True(ratio >= WcagAaMinimum,
            $"Finance green HSL(120,35%,70%) with dark text contrast {ratio:F2} < {WcagAaMinimum}");
    }

    [Fact]
    public void FinanceRed_HSL0_35_70_WithDarkText_PassesWcagAA()
    {
        var bg = HslToRgb(0, 35, 70);
        var text = (R: 26, G: 26, B: 26);
        var ratio = ContrastRatio(bg, text);
        Assert.True(ratio >= WcagAaMinimum,
            $"Finance red HSL(0,35%,70%) with dark text contrast {ratio:F2} < {WcagAaMinimum}");
    }

    // Committee badge — light theme (background HSL(120,40%,70%) with dark text)
    [Fact]
    public void CommitteeBadgeLight_HSL120_40_70_WithDarkText_PassesWcagAA()
    {
        var bg = HslToRgb(120, 40, 70);
        var text = (R: 26, G: 26, B: 26);
        var ratio = ContrastRatio(bg, text);
        Assert.True(ratio >= WcagAaMinimum,
            $"Committee badge light HSL(120,40%,70%) contrast {ratio:F2} < {WcagAaMinimum}");
    }

    // Committee badge — dark theme (background HSL(120,35%,55%) with dark text)
    [Fact]
    public void CommitteeBadgeDark_HSL120_35_55_WithDarkText_PassesWcagAA()
    {
        var bg = HslToRgb(120, 35, 55);
        var text = (R: 26, G: 26, B: 26);
        var ratio = ContrastRatio(bg, text);
        Assert.True(ratio >= WcagAaMinimum,
            $"Committee badge dark HSL(120,35%,55%) contrast {ratio:F2} < {WcagAaMinimum}");
    }

    // Finance green on white (light-mode background check)
    [Fact]
    public void FinanceGreen_HSL120_35_70_BlackText_ContrastAbove4_5()
    {
        var bg = HslToRgb(120, 35, 70);
        var black = (R: 0, G: 0, B: 0);
        var ratio = ContrastRatio(bg, black);
        Assert.True(ratio >= WcagAaMinimum,
            $"HSL(120,35%,70%) black text contrast {ratio:F2} < {WcagAaMinimum}");
    }

    // Finance red on white (light-mode background check)
    [Fact]
    public void FinanceRed_HSL0_35_70_BlackText_ContrastAbove4_5()
    {
        var bg = HslToRgb(0, 35, 70);
        var black = (R: 0, G: 0, B: 0);
        var ratio = ContrastRatio(bg, black);
        Assert.True(ratio >= WcagAaMinimum,
            $"HSL(0,35%,70%) black text contrast {ratio:F2} < {WcagAaMinimum}");
    }

    // --- WCAG calculation helpers ---

    private static (int R, int G, int B) HslToRgb(double h, double s, double l)
    {
        s /= 100.0;
        l /= 100.0;

        double c = (1.0 - Math.Abs(2.0 * l - 1.0)) * s;
        double x = c * (1.0 - Math.Abs(h / 60.0 % 2.0 - 1.0));
        double m = l - c / 2.0;

        double r1, g1, b1;
        if (h < 60)       { r1 = c; g1 = x; b1 = 0; }
        else if (h < 120) { r1 = x; g1 = c; b1 = 0; }
        else if (h < 180) { r1 = 0; g1 = c; b1 = x; }
        else if (h < 240) { r1 = 0; g1 = x; b1 = c; }
        else if (h < 300) { r1 = x; g1 = 0; b1 = c; }
        else              { r1 = c; g1 = 0; b1 = x; }

        return (
            (int)Math.Round((r1 + m) * 255.0),
            (int)Math.Round((g1 + m) * 255.0),
            (int)Math.Round((b1 + m) * 255.0)
        );
    }

    private static double RelativeLuminance((int R, int G, int B) color)
    {
        static double Linearize(int component)
        {
            double c = component / 255.0;
            return c <= 0.04045
                ? c / 12.92
                : Math.Pow((c + 0.055) / 1.055, 2.4);
        }

        return 0.2126 * Linearize(color.R)
             + 0.7152 * Linearize(color.G)
             + 0.0722 * Linearize(color.B);
    }

    private static double ContrastRatio(
        (int R, int G, int B) c1,
        (int R, int G, int B) c2)
    {
        double l1 = RelativeLuminance(c1);
        double l2 = RelativeLuminance(c2);
        double lighter = Math.Max(l1, l2);
        double darker  = Math.Min(l1, l2);
        return (lighter + 0.05) / (darker + 0.05);
    }
}
