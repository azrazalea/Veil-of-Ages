using Godot;
using VeilOfAges.Core;
using VeilOfAges.Core.Lib;
using VeilOfAges.Entities;

namespace VeilOfAges.Core.UI;

/// <summary>
/// Top bar displaying location name, date, time of day, and speed controls.
/// </summary>
public partial class TopBarPanel : PanelContainer
{
    private Label? _locationLabel;
    private Label? _dateLabel;
    private Label? _timeLabel;
    private HBoxContainer? _speedContainer;
    private Button? _pauseButton;
    private Button? _speed1Button;
    private Button? _speed2Button;
    private Button? _speed4Button;

    public override void _Ready()
    {
        // Build UI programmatically
        MouseFilter = MouseFilterEnum.Ignore;

        var hbox = new HBoxContainer();
        hbox.AddThemeConstantOverride("separation", 16);
        AddChild(hbox);

        _locationLabel = new Label { Text = string.Empty };
        _locationLabel.AddThemeFontSizeOverride("font_size", 18);
        _locationLabel.AddThemeColorOverride("font_color", new Color("#ffd700"));
        hbox.AddChild(_locationLabel);

        // Spacer
        var spacer = new Control
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Ignore
        };
        hbox.AddChild(spacer);

        _dateLabel = new Label { Text = string.Empty };
        _dateLabel.AddThemeColorOverride("font_color", new Color(0.8f, 0.8f, 1f, 0.8f));
        hbox.AddChild(_dateLabel);

        _timeLabel = new Label { Text = string.Empty };
        _timeLabel.AddThemeColorOverride("font_color", new Color(1f, 0.9f, 0.7f, 0.9f));
        hbox.AddChild(_timeLabel);

        // Speed controls
        _speedContainer = new HBoxContainer();
        _speedContainer.AddThemeConstantOverride("separation", 4);
        hbox.AddChild(_speedContainer);

        _pauseButton = CreateSpeedButton("⏸");
        _pauseButton.Pressed += OnPausePressed;
        _speedContainer.AddChild(_pauseButton);

        _speed1Button = CreateSpeedButton("1x");
        _speed1Button.Pressed += () => OnSpeedPressed(1f);
        _speedContainer.AddChild(_speed1Button);

        _speed2Button = CreateSpeedButton("2x");
        _speed2Button.Pressed += () => OnSpeedPressed(2f);
        _speedContainer.AddChild(_speed2Button);

        _speed4Button = CreateSpeedButton("4x");
        _speed4Button.Pressed += () => OnSpeedPressed(4f);
        _speedContainer.AddChild(_speed4Button);
    }

    private static Button CreateSpeedButton(string text)
    {
        return new Button
        {
            Text = text,
            ToggleMode = true,
            CustomMinimumSize = new Vector2(36, 28),
            FocusMode = FocusModeEnum.None,
        };
    }

    public override void _EnterTree()
    {
        GameEvents.UITickFired += OnUITick;
        GameEvents.SimulationPauseChanged += OnPauseChanged;
        GameEvents.TimeScaleChanged += OnTimeScaleChanged;
    }

    public override void _ExitTree()
    {
        GameEvents.UITickFired -= OnUITick;
        GameEvents.SimulationPauseChanged -= OnPauseChanged;
        GameEvents.TimeScaleChanged -= OnTimeScaleChanged;
    }

    private void OnUITick()
    {
        if (!Services.TryGet<GameController>(out var gc) || gc == null)
        {
            return;
        }

        // Area name: read from player's current grid area
        if (_locationLabel != null && Services.TryGet<Player>(out var player) && player != null)
        {
            var areaName = player.GetGridArea()?.AreaDisplayName ?? string.Empty;
            if (_locationLabel.Text != areaName)
            {
                _locationLabel.Text = areaName;
            }
        }

        var gameTime = gc.CurrentGameTime;

        var dateText = L.TrFmt("ui.hud.DAY_DATE", gameTime.Day, gameTime.LocalizedMonthName);
        if (_dateLabel != null && _dateLabel.Text != dateText)
        {
            _dateLabel.Text = dateText;
        }

        var timeText = CapitalizeFirst(gameTime.GetTimeOfDayDescription());
        if (_timeLabel != null && _timeLabel.Text != timeText)
        {
            _timeLabel.Text = timeText;
        }

        UpdateSpeedHighlight(gc);
    }

    private void OnPauseChanged(bool paused)
    {
        if (!Services.TryGet<GameController>(out var gc) || gc == null)
        {
            return;
        }

        UpdateSpeedHighlight(gc);
    }

    private void OnTimeScaleChanged(float scale)
    {
        if (!Services.TryGet<GameController>(out var gc) || gc == null)
        {
            return;
        }

        UpdateSpeedHighlight(gc);
    }

    private void UpdateSpeedHighlight(GameController gc)
    {
        bool paused = gc.SimulationPaused();
        float scale = gc.TimeScale;

        if (_pauseButton != null)
        {
            _pauseButton.ButtonPressed = paused;
        }

        if (_speed1Button != null)
        {
            _speed1Button.ButtonPressed = !paused && Mathf.IsEqualApprox(scale, 1f);
        }

        if (_speed2Button != null)
        {
            _speed2Button.ButtonPressed = !paused && Mathf.IsEqualApprox(scale, 2f);
        }

        if (_speed4Button != null)
        {
            _speed4Button.ButtonPressed = !paused && Mathf.IsEqualApprox(scale, 4f);
        }
    }

    private void OnPausePressed()
    {
        if (Services.TryGet<GameController>(out var gc) && gc != null)
        {
            gc.ToggleSimulationPause();
        }
    }

    private static void OnSpeedPressed(float scale)
    {
        if (!Services.TryGet<GameController>(out var gc) || gc == null)
        {
            return;
        }

        gc.SetTimeScale(scale);
        if (gc.SimulationPaused())
        {
            gc.ToggleSimulationPause();
        }
    }

    private static string CapitalizeFirst(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return input;
        }

        return char.ToUpperInvariant(input[0]) + input[1..];
    }
}
