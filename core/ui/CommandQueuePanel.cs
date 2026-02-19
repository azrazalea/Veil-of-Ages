using System.Collections.Generic;
using Godot;
using VeilOfAges.Core;
using VeilOfAges.Core.Lib;
using VeilOfAges.Entities;
using VeilOfAges.UI;

namespace VeilOfAges.Core.UI;

/// <summary>
/// Horizontal command queue strip showing current command and queued commands.
/// Uses node pooling instead of destroy/recreate pattern.
/// </summary>
public partial class CommandQueuePanel : PanelContainer
{
    private HBoxContainer? _queueContainer;
    private Label? _emptyLabel;
    private readonly List<Label> _commandLabels = new ();

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;

        _queueContainer = new HBoxContainer();
        _queueContainer.AddThemeConstantOverride("separation", 8);
        AddChild(_queueContainer);

        _emptyLabel = new Label { Text = L.Tr("ui.hud.NO_COMMANDS") };
        _emptyLabel.AddThemeFontSizeOverride("font_size", 11);
        _emptyLabel.AddThemeColorOverride("font_color", new Color("#8888aa"));
        _queueContainer.AddChild(_emptyLabel);
    }

    public override void _EnterTree()
    {
        GameEvents.CommandQueueChanged += OnCommandQueueChanged;
        GameEvents.UITickFired += OnUITick;
    }

    public override void _ExitTree()
    {
        GameEvents.CommandQueueChanged -= OnCommandQueueChanged;
        GameEvents.UITickFired -= OnUITick;
    }

    private void OnCommandQueueChanged()
    {
        RefreshQueue();
    }

    private void OnUITick()
    {
        RefreshQueue();
    }

    private void RefreshQueue()
    {
        if (!Services.TryGet<Player>(out var player) || player == null)
        {
            return;
        }

        var queue = player.GetCommandQueue();
        var currentCommand = player.GetAssignedCommand();

        // Collect all items to display
        var items = new List<string>();

        if (currentCommand != null)
        {
            items.Add($"▶ {currentCommand.DisplayName}");
        }

        var node = queue.First();
        while (node != null)
        {
            items.Add(node.Value.DisplayName ?? string.Empty);
            node = node.Next;
        }

        // Show/hide empty label
        bool isEmpty = items.Count == 0;
        if (_emptyLabel != null)
        {
            _emptyLabel.Visible = isEmpty;
        }

        // Ensure pool has enough labels
        EnsureLabelPool(items.Count);

        // Update labels
        for (int i = 0; i < items.Count; i++)
        {
            var label = _commandLabels[i];
            label.Visible = true;
            if (label.Text != items[i])
            {
                label.Text = items[i];
            }

            // First item (current command) gets highlight color
            if (i == 0 && currentCommand != null)
            {
                label.AddThemeColorOverride("font_color", new Color("#ffd700"));
            }
            else
            {
                label.AddThemeColorOverride("font_color", new Color("#ffffff"));
            }
        }

        // Hide excess labels
        for (int i = items.Count; i < _commandLabels.Count; i++)
        {
            _commandLabels[i].Visible = false;
        }
    }

    private void EnsureLabelPool(int count)
    {
        while (_commandLabels.Count < count)
        {
            var label = new Label();
            label.AddThemeFontSizeOverride("font_size", 11);
            _queueContainer?.AddChild(label);
            _commandLabels.Add(label);
        }
    }
}
