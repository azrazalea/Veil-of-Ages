using Godot;
using VeilOfAges.Core.Lib;

namespace VeilOfAges.Core;

/// <summary>
/// Manages the visual day/night cycle by modulating the world's colors
/// based on the current game time. Uses GameTime as the source of truth
/// for day phase and daylight levels.
/// </summary>
public partial class DayNightCycle : CanvasModulate
{
    /// <summary>
    /// Gets or sets color for full daylight (no tint).
    /// </summary>
    [Export]
    public Color DayColor { get; set; } = new Color(1.0f, 1.0f, 1.0f);

    /// <summary>
    /// Gets or sets color for dawn (warm sunrise tint).
    /// </summary>
    [Export]
    public Color DawnColor { get; set; } = new Color(1.0f, 0.85f, 0.7f);

    /// <summary>
    /// Gets or sets color for dusk (warm sunset tint).
    /// </summary>
    [Export]
    public Color DuskColor { get; set; } = new Color(1.0f, 0.7f, 0.5f);

    /// <summary>
    /// Gets or sets color for night (cool dark tint).
    /// </summary>
    [Export]
    public Color NightColor { get; set; } = new Color(0.15f, 0.15f, 0.3f);

    /// <summary>
    /// Gets or sets how quickly colors transition (higher = faster).
    /// </summary>
    [Export]
    public float TransitionSpeed { get; set; } = 2.0f;

    private GameController? _gameController;
    private Color _targetColor;
    private Color _currentColor;

    public override void _Ready()
    {
        _gameController = GetNode<GameController>("/root/World/GameController");
        if (_gameController == null)
        {
            Log.Error("DayNightCycle: Could not find GameController!");
            return;
        }

        // Initialize to current state immediately
        _targetColor = CalculateTargetColor();
        _currentColor = _targetColor;
        Color = _currentColor;
    }

    public override void _Process(double delta)
    {
        if (_gameController == null)
        {
            return;
        }

        // Calculate target color based on current game time
        _targetColor = CalculateTargetColor();

        // Smoothly interpolate to target
        _currentColor = _currentColor.Lerp(_targetColor, (float)delta * TransitionSpeed);
        Color = _currentColor;
    }

    /// <summary>
    /// Calculates the target modulation color based on current game time.
    /// </summary>
    private Color CalculateTargetColor()
    {
        if (_gameController == null)
        {
            return DayColor;
        }

        GameTime time = _gameController.CurrentGameTime;
        DayPhaseType phase = time.CurrentDayPhase;
        float daylightLevel = time.DaylightLevel;

        return phase switch
        {
            // Dawn: blend from night to dawn color, then towards day
            DayPhaseType.Dawn => BlendDawnColor(daylightLevel),

            // Day: full brightness, no tint
            DayPhaseType.Day => DayColor,

            // Dusk: blend from day to dusk color, then towards night
            DayPhaseType.Dusk => BlendDuskColor(daylightLevel),

            // Night: dark blue tint
            DayPhaseType.Night => NightColor,

            _ => DayColor
        };
    }

    /// <summary>
    /// Blends dawn colors based on progress through the dawn hour.
    /// Early dawn (low daylight): between night and dawn color
    /// Late dawn (high daylight): between dawn and day color.
    /// </summary>
    private Color BlendDawnColor(float daylightLevel)
    {
        // daylightLevel goes from 0.1 to 1.0 during dawn
        // Normalize to 0-1 range for blending
        float progress = (daylightLevel - 0.1f) / 0.9f;

        if (progress < 0.5f)
        {
            // First half: night -> dawn
            float t = progress * 2.0f;
            return NightColor.Lerp(DawnColor, t);
        }
        else
        {
            // Second half: dawn -> day
            float t = (progress - 0.5f) * 2.0f;
            return DawnColor.Lerp(DayColor, t);
        }
    }

    /// <summary>
    /// Blends dusk colors based on progress through the dusk hour.
    /// Early dusk (high daylight): between day and dusk color
    /// Late dusk (low daylight): between dusk and night color.
    /// </summary>
    private Color BlendDuskColor(float daylightLevel)
    {
        // daylightLevel goes from 1.0 to 0.1 during dusk
        // Normalize to 0-1 range for blending (inverted)
        float progress = (1.0f - daylightLevel) / 0.9f;

        if (progress < 0.5f)
        {
            // First half: day -> dusk
            float t = progress * 2.0f;
            return DayColor.Lerp(DuskColor, t);
        }
        else
        {
            // Second half: dusk -> night
            float t = (progress - 0.5f) * 2.0f;
            return DuskColor.Lerp(NightColor, t);
        }
    }
}
