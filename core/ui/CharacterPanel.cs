using Godot;
using VeilOfAges.Core;
using VeilOfAges.Core.Lib;
using VeilOfAges.Entities;
using VeilOfAges.Entities.Traits;

namespace VeilOfAges.Core.UI;

/// <summary>
/// Bottom-left character cluster: portrait placeholder, name, activity, automation indicator.
/// </summary>
public partial class CharacterPanel : PanelContainer
{
    private Label? _nameLabel;
    private Label? _activityLabel;
    private Label? _automationLabel;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;

        var hbox = new HBoxContainer();
        hbox.AddThemeConstantOverride("separation", 10);
        AddChild(hbox);

        // Portrait placeholder
        var portrait = new TextureRect
        {
            CustomMinimumSize = new Vector2(48, 48),
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = MouseFilterEnum.Ignore
        };
        hbox.AddChild(portrait);

        // Info column
        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 2);
        hbox.AddChild(vbox);

        _nameLabel = new Label { Text = "Lilith" };
        _nameLabel.AddThemeFontSizeOverride("font_size", 16);
        _nameLabel.AddThemeColorOverride("font_color", new Color("#ffd700"));
        vbox.AddChild(_nameLabel);

        _activityLabel = new Label { Text = L.Tr("ui.hud.IDLE") };
        _activityLabel.AddThemeFontSizeOverride("font_size", 12);
        _activityLabel.AddThemeColorOverride("font_color", new Color("#8888aa"));
        _activityLabel.ClipText = true;
        _activityLabel.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        _activityLabel.CustomMinimumSize = new Vector2(120, 0);
        vbox.AddChild(_activityLabel);

        _automationLabel = new Label { Text = L.Tr("ui.hud.MANUAL") };
        _automationLabel.AddThemeFontSizeOverride("font_size", 10);
        _automationLabel.AddThemeColorOverride("font_color", new Color("#cc4444"));
        vbox.AddChild(_automationLabel);
    }

    public override void _EnterTree()
    {
        GameEvents.UITickFired += OnUITick;
        GameEvents.AutomationToggled += OnAutomationToggled;
    }

    public override void _ExitTree()
    {
        GameEvents.UITickFired -= OnUITick;
        GameEvents.AutomationToggled -= OnAutomationToggled;
    }

    private void OnUITick()
    {
        if (!Services.TryGet<Player>(out var player) || player == null)
        {
            return;
        }

        // Name
        if (_nameLabel != null && _nameLabel.Text != player.Name)
        {
            _nameLabel.Text = player.Name;
        }

        // Activity
        if (_activityLabel != null)
        {
            var activity = player.GetCurrentActivity();
            string activityText;
            if (activity != null)
            {
                activityText = activity.DisplayName;
            }
            else
            {
                var command = player.GetAssignedCommand();
                activityText = command?.DisplayName ?? L.Tr("ui.hud.IDLE");
            }

            if (_activityLabel.Text != activityText)
            {
                _activityLabel.Text = activityText;
            }
        }

        // Automation (periodic sync — instant updates come from event)
        UpdateAutomationLabel(player);
    }

    private void OnAutomationToggled(bool isAutomated)
    {
        if (Services.TryGet<Player>(out var player) && player != null)
        {
            UpdateAutomationLabel(player);
        }
    }

    private void UpdateAutomationLabel(Player player)
    {
        if (_automationLabel == null)
        {
            return;
        }

        var automationTrait = player.SelfAsEntity().GetTrait<AutomationTrait>();
        bool isAuto = automationTrait?.IsAutomated ?? true;

        string text = isAuto ? L.Tr("ui.hud.AUTO") : L.Tr("ui.hud.MANUAL");
        if (_automationLabel.Text != text)
        {
            _automationLabel.Text = text;
        }

        var color = isAuto ? new Color("#44cc44") : new Color("#cc4444");
        _automationLabel.AddThemeColorOverride("font_color", color);
    }
}
