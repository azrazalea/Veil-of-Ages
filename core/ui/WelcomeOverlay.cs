using Godot;
using VeilOfAges.Core.Lib;

namespace VeilOfAges.Core.UI;

/// <summary>
/// Semi-transparent welcome overlay shown on game start.
/// Displays controls and basic game introduction. Does NOT pause the game.
/// Dismissed by pressing Escape or clicking "Got it!" button.
/// </summary>
public partial class WelcomeOverlay : PanelContainer
{
    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        Theme = NecromancerTheme.Build();

        // Override panel style with slightly more transparent background
        var panelStyle = new StyleBoxFlat
        {
            BgColor = new Color(0.176f, 0.176f, 0.259f, 0.95f),
            BorderColor = new Color("#7b5aad"),
            BorderWidthLeft = 4,
            BorderWidthTop = 4,
            BorderWidthRight = 4,
            BorderWidthBottom = 4,
            CornerRadiusTopLeft = 8,
            CornerRadiusTopRight = 8,
            CornerRadiusBottomLeft = 8,
            CornerRadiusBottomRight = 8,
            ContentMarginLeft = 24,
            ContentMarginTop = 20,
            ContentMarginRight = 24,
            ContentMarginBottom = 20,
        };
        AddThemeStyleboxOverride("panel", panelStyle);

        CustomMinimumSize = new Vector2(500, 0);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 12);
        AddChild(vbox);

        // Title
        var title = new Label
        {
            Text = L.Tr("ui.welcome.TITLE"),
            ThemeTypeVariation = "HeaderLabel",
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        vbox.AddChild(title);

        // Intro paragraph
        var intro = new Label
        {
            Text = L.Tr("ui.welcome.INTRO"),
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        vbox.AddChild(intro);

        // Separator + Controls header
        vbox.AddChild(new HSeparator());

        var controlsHeader = new Label
        {
            Text = L.Tr("ui.welcome.CONTROLS_HEADER"),
            ThemeTypeVariation = "HeaderLabel",
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        vbox.AddChild(controlsHeader);

        // Controls grid
        var grid = new GridContainer
        {
            Columns = 2,
        };
        grid.AddThemeConstantOverride("h_separation", 16);
        grid.AddThemeConstantOverride("v_separation", 6);
        vbox.AddChild(grid);

        AddControlRow(grid, L.Tr("ui.welcome.KEY_LEFT_CLICK"), L.Tr("ui.welcome.DESC_LEFT_CLICK"));
        AddControlRow(grid, L.Tr("ui.welcome.KEY_RIGHT_CLICK"), L.Tr("ui.welcome.DESC_RIGHT_CLICK"));
        AddControlRow(grid, L.Tr("ui.welcome.KEY_ESCAPE"), L.Tr("ui.welcome.DESC_ESCAPE"));
        AddControlRow(grid, L.Tr("ui.welcome.KEY_J"), L.Tr("ui.welcome.DESC_J"));
        AddControlRow(grid, L.Tr("ui.welcome.KEY_K"), L.Tr("ui.welcome.DESC_K"));
        AddControlRow(grid, L.Tr("ui.welcome.KEY_CTRL_T"), L.Tr("ui.welcome.DESC_CTRL_T"));
        AddControlRow(grid, L.Tr("ui.welcome.KEY_CTRL_SPACE"), L.Tr("ui.welcome.DESC_CTRL_SPACE"));
        AddControlRow(grid, L.Tr("ui.welcome.KEY_SPEED"), L.Tr("ui.welcome.DESC_SPEED"));

        // Got it! button
        var buttonContainer = new HBoxContainer
        {
            Alignment = BoxContainer.AlignmentMode.Center,
        };
        vbox.AddChild(buttonContainer);

        var button = new Button
        {
            Text = L.Tr("ui.welcome.GOT_IT"),
            CustomMinimumSize = new Vector2(120, 36),
        };
        button.Pressed += Dismiss;
        buttonContainer.AddChild(button);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!Visible)
        {
            return;
        }

        if (@event.IsActionPressed("exit"))
        {
            Dismiss();
            GetViewport().SetInputAsHandled();
        }
    }

    private void Dismiss()
    {
        Visible = false;
        QueueFree();
    }

    private static void AddControlRow(GridContainer grid, string key, string description)
    {
        var keyLabel = new Label
        {
            Text = key,
            ThemeTypeVariation = "DimLabel",
            HorizontalAlignment = HorizontalAlignment.Right,
            CustomMinimumSize = new Vector2(100, 0),
        };
        grid.AddChild(keyLabel);

        var descLabel = new Label
        {
            Text = description,
        };
        grid.AddChild(descLabel);
    }
}
