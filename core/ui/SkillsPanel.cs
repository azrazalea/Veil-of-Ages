using System.Collections.Generic;
using System.Linq;
using Godot;
using VeilOfAges.Core;
using VeilOfAges.Entities;

namespace VeilOfAges.Core.UI;

/// <summary>
/// Toggleable panel showing the player's skills with level and XP progress bars.
/// Press K to show/hide. Only displays skills the player currently has.
/// </summary>
public partial class SkillsPanel : PanelContainer
{
    private VBoxContainer? _skillsContainer;
    private readonly List<SkillRowEntry> _skillRows = new ();

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        Visible = false;

        _skillsContainer = new VBoxContainer();
        _skillsContainer.AddThemeConstantOverride("separation", 8);
        AddChild(_skillsContainer);

        // Header label using HeaderLabel theme type variation (gold color)
        var header = new Label
        {
            ThemeTypeVariation = "HeaderLabel",
            Text = "Skills"
        };
        _skillsContainer.AddChild(header);
    }

    public override void _EnterTree()
    {
        GameEvents.UITickFired += OnUITick;
    }

    public override void _ExitTree()
    {
        GameEvents.UITickFired -= OnUITick;
    }

    private void OnUITick()
    {
        if (!Visible)
        {
            return;
        }

        if (!Services.TryGet<Player>(out var player) || player == null)
        {
            return;
        }

        var skillSystem = player.SkillSystem;
        if (skillSystem == null)
        {
            return;
        }

        var skills = skillSystem.GetAllSkills()
            .OrderBy(s => s.Definition.Category)
            .ThenBy(s => s.Definition.LocalizedName)
            .ToList();

        EnsureRowCount(skills.Count);

        for (int i = 0; i < skills.Count; i++)
        {
            var skill = skills[i];
            var entry = _skillRows[i];

            // Update name
            string nameText = skill.Definition.LocalizedName;
            if (entry.NameLabel != null && entry.NameLabel.Text != nameText)
            {
                entry.NameLabel.Text = nameText;
            }

            // Update level
            string levelText = skill.IsMaxLevel ? "MAX" : $"Lv {skill.Level}";
            if (entry.LevelLabel != null && entry.LevelLabel.Text != levelText)
            {
                entry.LevelLabel.Text = levelText;
            }

            // Update progress bar
            if (entry.Bar != null)
            {
                double targetValue = skill.LevelProgress * 100.0;
                if (System.Math.Abs(entry.Bar.Value - targetValue) > 0.01)
                {
                    entry.Bar.Value = targetValue;
                }
            }

            if (entry.Container != null)
            {
                entry.Container.Visible = true;
            }
        }

        // Hide excess rows
        for (int i = skills.Count; i < _skillRows.Count; i++)
        {
            if (_skillRows[i].Container is { } container)
            {
                container.Visible = false;
            }
        }
    }

    private void EnsureRowCount(int count)
    {
        while (_skillRows.Count < count)
        {
            // Outer container for the row
            var rowContainer = new VBoxContainer();
            rowContainer.AddThemeConstantOverride("separation", 2);
            _skillsContainer?.AddChild(rowContainer);

            // Name + Level row
            var headerRow = new HBoxContainer();
            rowContainer.AddChild(headerRow);

            var nameLabel = new Label
            {
                SizeFlagsHorizontal = SizeFlags.ExpandFill
            };
            nameLabel.AddThemeFontSizeOverride("font_size", 11);
            nameLabel.AddThemeColorOverride("font_color", Colors.White);
            headerRow.AddChild(nameLabel);

            var levelLabel = new Label();
            levelLabel.AddThemeFontSizeOverride("font_size", 11);
            levelLabel.AddThemeColorOverride("font_color", Colors.White);
            headerRow.AddChild(levelLabel);

            // Progress bar
            var bar = new ProgressBar
            {
                CustomMinimumSize = new Vector2(0, 12),
                MaxValue = 100,
                Value = 0,
                ShowPercentage = false,
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
            };

            // Create unique fill style (prevent color bleed)
            var fillStyle = new StyleBoxFlat
            {
                BgColor = new Color("#7b5aad"),
                CornerRadiusTopLeft = 3,
                CornerRadiusTopRight = 3,
                CornerRadiusBottomLeft = 3,
                CornerRadiusBottomRight = 3,
            };
            bar.AddThemeStyleboxOverride("fill", fillStyle);

            var bgStyle = new StyleBoxFlat
            {
                BgColor = new Color("#1a1a2e"),
                CornerRadiusTopLeft = 3,
                CornerRadiusTopRight = 3,
                CornerRadiusBottomLeft = 3,
                CornerRadiusBottomRight = 3,
            };
            bar.AddThemeStyleboxOverride("background", bgStyle);

            rowContainer.AddChild(bar);

            _skillRows.Add(new SkillRowEntry
            {
                Container = rowContainer,
                NameLabel = nameLabel,
                LevelLabel = levelLabel,
                Bar = bar,
            });
        }
    }

    private sealed class SkillRowEntry
    {
        public VBoxContainer? Container;
        public Label? NameLabel;
        public Label? LevelLabel;
        public ProgressBar? Bar;
    }
}
