using System.Collections.Generic;
using System.Linq;
using Godot;
using VeilOfAges.Core;
using VeilOfAges.Entities;
using VeilOfAges.Entities.Needs;

namespace VeilOfAges.Core.UI;

/// <summary>
/// Displays the 2-3 most critical needs as compact progress bars.
/// Hides entirely when all needs are satisfied (calm HUD).
/// Uses _Process only for smooth bar lerping.
/// </summary>
public partial class NeedsPanel : PanelContainer
{
    private const int MAXVISIBLENEEDS = 3;
    private const float LERPSPEED = 10f;

    private VBoxContainer? _needsContainer;
    private readonly List<NeedBarEntry> _needBars = new ();
    private readonly Dictionary<string, float> _previousValues = new ();

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;

        _needsContainer = new VBoxContainer();
        _needsContainer.AddThemeConstantOverride("separation", 4);
        AddChild(_needsContainer);
    }

    public override void _EnterTree()
    {
        GameEvents.UITickFired += OnUITick;
    }

    public override void _ExitTree()
    {
        GameEvents.UITickFired -= OnUITick;
    }

    public override void _Process(double delta)
    {
        // Lerp bars toward target values for smooth animation
        foreach (var entry in _needBars)
        {
            if (entry.Bar == null)
            {
                continue;
            }

            float current = (float)entry.Bar.Value;
            if (Mathf.Abs(current - entry.Target) > 0.01f)
            {
                entry.Bar.Value = Mathf.Lerp(current, entry.Target, 1f - Mathf.Exp(-LERPSPEED * (float)delta));
            }
        }
    }

    private void OnUITick()
    {
        if (!Services.TryGet<Player>(out var player) || player == null)
        {
            return;
        }

        if (player.NeedsSystem == null)
        {
            return;
        }

        var allNeeds = player.NeedsSystem.GetAllNeeds().ToList();

        // Sort by value ascending (most critical first)
        allNeeds.Sort((a, b) => a.Value.CompareTo(b.Value));

        // Check if all needs are satisfied
        bool allSatisfied = allNeeds.All(n => n.IsSatisfied());
        Visible = !allSatisfied;

        if (allSatisfied)
        {
            return;
        }

        // Take top N most critical needs
        var visibleNeeds = allNeeds.Take(MAXVISIBLENEEDS).ToList();

        // Ensure we have the right number of bar entries
        EnsureBarCount(visibleNeeds.Count);

        // Update each bar
        for (int i = 0; i < visibleNeeds.Count; i++)
        {
            var need = visibleNeeds[i];
            var entry = _needBars[i];

            // Determine trend arrow
            string trend = "→";
            if (_previousValues.TryGetValue(need.Id, out float prevValue))
            {
                float diff = need.Value - prevValue;
                if (diff > 0.1f)
                {
                    trend = "↑";
                }
                else if (diff < -0.1f)
                {
                    trend = "↓";
                }
            }

            _previousValues[need.Id] = need.Value;

            // Update label
            string labelText = $"{trend} {need.DisplayName}";
            if (entry.Label != null && entry.Label.Text != labelText)
            {
                entry.Label.Text = labelText;
            }

            // Update bar color based on status
            if (entry.Bar != null)
            {
                if (entry.Bar.GetThemeStylebox("fill") is StyleBoxFlat fillStyle)
                {
                    Color barColor;
                    if (need.IsCritical())
                    {
                        barColor = new Color("#cc4444");
                    }
                    else if (need.IsLow())
                    {
                        barColor = new Color("#cc8844");
                    }
                    else
                    {
                        barColor = new Color("#7b5aad");
                    }

                    if (fillStyle.BgColor != barColor)
                    {
                        fillStyle.BgColor = barColor;
                    }
                }
            }

            // Set target for lerping
            entry.Target = need.Value;

            if (entry.Container != null)
            {
                entry.Container.Visible = true;
            }
        }

        // Hide excess bars
        for (int i = visibleNeeds.Count; i < _needBars.Count; i++)
        {
            if (_needBars[i].Container is { } container)
            {
                container.Visible = false;
            }
        }
    }

    private void EnsureBarCount(int count)
    {
        while (_needBars.Count < count)
        {
            var container = new HBoxContainer();
            container.AddThemeConstantOverride("separation", 6);
            _needsContainer?.AddChild(container);

            var label = new Label();
            label.AddThemeFontSizeOverride("font_size", 11);
            label.AddThemeColorOverride("font_color", new Color("#8888aa"));
            label.CustomMinimumSize = new Vector2(80, 0);
            container.AddChild(label);

            var bar = new ProgressBar
            {
                CustomMinimumSize = new Vector2(100, 16),
                MaxValue = 100,
                Value = 0,
                ShowPercentage = false,
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
            };

            // Create unique fill style for each bar so colors don't bleed
            var fillStyle = new StyleBoxFlat
            {
                BgColor = new Color("#7b5aad"),
                CornerRadiusTopLeft = 4,
                CornerRadiusTopRight = 4,
                CornerRadiusBottomLeft = 4,
                CornerRadiusBottomRight = 4,
            };
            bar.AddThemeStyleboxOverride("fill", fillStyle);

            var bgStyle = new StyleBoxFlat
            {
                BgColor = new Color("#1a1a2e"),
                CornerRadiusTopLeft = 4,
                CornerRadiusTopRight = 4,
                CornerRadiusBottomLeft = 4,
                CornerRadiusBottomRight = 4,
            };
            bar.AddThemeStyleboxOverride("background", bgStyle);

            container.AddChild(bar);

            _needBars.Add(new NeedBarEntry
            {
                Container = container,
                Label = label,
                Bar = bar,
                Target = 0f,
            });
        }
    }

    private sealed class NeedBarEntry
    {
        public HBoxContainer? Container;
        public Label? Label;
        public ProgressBar? Bar;
        public float Target;
    }
}
