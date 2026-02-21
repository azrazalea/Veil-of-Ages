using Godot;

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
            Text = "Veil of Ages: Whispers of\nKalixoria",
            ThemeTypeVariation = "HeaderLabel",
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        vbox.AddChild(title);

        // Intro paragraph
        var intro = new Label
        {
            Text = "You are the village scholar \u2014 and secretly, a necromancer. " +
                   "Your character acts on their own: studying, eating, and sleeping. " +
                   "Give commands to take direct control.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        vbox.AddChild(intro);

        // Separator + Controls header
        vbox.AddChild(new HSeparator());

        var controlsHeader = new Label
        {
            Text = "Controls",
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

        AddControlRow(grid, "Left-click", "Move / Talk (adjacent)");
        AddControlRow(grid, "Right-click", "Context menu");
        AddControlRow(grid, "Escape", "Cancel action / Close panel");
        AddControlRow(grid, "K", "Toggle skills panel");
        AddControlRow(grid, "Ctrl+T", "Toggle AUTO / MANUAL mode");
        AddControlRow(grid, "Ctrl+Space", "Pause / Resume simulation");
        AddControlRow(grid, "+  /  \u2212", "Speed up / Slow down");

        // Got it! button
        var buttonContainer = new HBoxContainer
        {
            Alignment = BoxContainer.AlignmentMode.Center,
        };
        vbox.AddChild(buttonContainer);

        var button = new Button
        {
            Text = "Got it!",
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
