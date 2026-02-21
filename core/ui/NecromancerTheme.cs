using Godot;

namespace VeilOfAges.Core.UI;

/// <summary>
/// Programmatic Godot Theme builder for the necromancer UI aesthetic.
/// Call Build() to get a configured Theme instance to apply to UI containers.
/// </summary>
public static class NecromancerTheme
{
    // Dark necromancer palette
    private static readonly Color _colorBackground = new ("#2d2d42");
    private static readonly Color _colorBorder = new ("#7b5aad");
    private static readonly Color _colorText = new ("#ffffff");
    private static readonly Color _colorGold = new ("#ffd700");
    private static readonly Color _colorDim = new ("#8888aa");

    // Public palette colors for use by panel code (e.g., NeedsPanel bar coloring)
    public static readonly Color ColorCritical = new ("#cc4444");
    public static readonly Color ColorGood = new ("#44cc44");
    private static readonly Color _colorBarBackground = new ("#1a1a2e");
    private static readonly Color _colorBarFill = new ("#7b5aad");

    /// <summary>
    /// Creates and returns a fully configured necromancer-themed Godot Theme.
    /// </summary>
    public static Theme Build()
    {
        var theme = new Theme();

        RegisterTypeVariations(theme);
        StyleLabel(theme);
        StyleHeaderLabel(theme);
        StyleValueLabel(theme);
        StyleDimLabel(theme);
        StylePanelContainer(theme);
        StyleProgressBar(theme);
        StyleButton(theme);

        return theme;
    }

    private static void RegisterTypeVariations(Theme theme)
    {
        theme.SetTypeVariation("HeaderLabel", "Label");
        theme.SetTypeVariation("ValueLabel", "Label");
        theme.SetTypeVariation("DimLabel", "Label");
    }

    private static void StyleLabel(Theme theme)
    {
        theme.SetColor("font_color", "Label", _colorText);
        theme.SetFontSize("font_size", "Label", 14);
    }

    private static void StyleHeaderLabel(Theme theme)
    {
        theme.SetColor("font_color", "HeaderLabel", _colorGold);
        theme.SetFontSize("font_size", "HeaderLabel", 18);
    }

    private static void StyleValueLabel(Theme theme)
    {
        theme.SetColor("font_color", "ValueLabel", _colorText);
        theme.SetFontSize("font_size", "ValueLabel", 14);
    }

    private static void StyleDimLabel(Theme theme)
    {
        theme.SetColor("font_color", "DimLabel", _colorDim);
        theme.SetFontSize("font_size", "DimLabel", 11);
    }

    private static void StylePanelContainer(Theme theme)
    {
        var panel = new StyleBoxFlat
        {
            BgColor = _colorBackground,
            BorderColor = _colorBorder,
            BorderWidthLeft = 4,
            BorderWidthTop = 4,
            BorderWidthRight = 4,
            BorderWidthBottom = 4,
            CornerRadiusTopLeft = 8,
            CornerRadiusTopRight = 8,
            CornerRadiusBottomLeft = 8,
            CornerRadiusBottomRight = 8,
            ContentMarginLeft = 8,
            ContentMarginTop = 8,
            ContentMarginRight = 8,
            ContentMarginBottom = 8,
        };

        theme.SetStylebox("panel", "PanelContainer", panel);
    }

    private static void StyleProgressBar(Theme theme)
    {
        var background = new StyleBoxFlat
        {
            BgColor = _colorBarBackground,
            CornerRadiusTopLeft = 4,
            CornerRadiusTopRight = 4,
            CornerRadiusBottomLeft = 4,
            CornerRadiusBottomRight = 4,
        };

        var fill = new StyleBoxFlat
        {
            BgColor = _colorBarFill,
            CornerRadiusTopLeft = 4,
            CornerRadiusTopRight = 4,
            CornerRadiusBottomLeft = 4,
            CornerRadiusBottomRight = 4,
        };

        theme.SetStylebox("background", "ProgressBar", background);
        theme.SetStylebox("fill", "ProgressBar", fill);
        theme.SetFontSize("font_size", "ProgressBar", 9);
    }

    private static void StyleButton(Theme theme)
    {
        var normal = new StyleBoxFlat
        {
            BgColor = _colorBackground,
            BorderColor = _colorBackground,
            BorderWidthLeft = 2,
            BorderWidthTop = 2,
            BorderWidthRight = 2,
            BorderWidthBottom = 2,
            CornerRadiusTopLeft = 4,
            CornerRadiusTopRight = 4,
            CornerRadiusBottomLeft = 4,
            CornerRadiusBottomRight = 4,
        };

        var hover = new StyleBoxFlat
        {
            BgColor = _colorBackground,
            BorderColor = _colorBorder,
            BorderWidthLeft = 2,
            BorderWidthTop = 2,
            BorderWidthRight = 2,
            BorderWidthBottom = 2,
            CornerRadiusTopLeft = 4,
            CornerRadiusTopRight = 4,
            CornerRadiusBottomLeft = 4,
            CornerRadiusBottomRight = 4,
        };

        var pressed = new StyleBoxFlat
        {
            BgColor = _colorBorder,
            BorderColor = _colorBorder,
            BorderWidthLeft = 2,
            BorderWidthTop = 2,
            BorderWidthRight = 2,
            BorderWidthBottom = 2,
            CornerRadiusTopLeft = 4,
            CornerRadiusTopRight = 4,
            CornerRadiusBottomLeft = 4,
            CornerRadiusBottomRight = 4,
        };

        theme.SetStylebox("normal", "Button", normal);
        theme.SetStylebox("hover", "Button", hover);
        theme.SetStylebox("pressed", "Button", pressed);
        theme.SetColor("font_color", "Button", _colorText);
        theme.SetFontSize("font_size", "Button", 14);
    }
}
