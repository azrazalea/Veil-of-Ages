# UI Components — Implementation Detail

## Table of contents

- Scene structure and CanvasLayer stacking
- Theming
- Animated transitions
- Toast notification system
- Custom tooltips
- Drag-and-drop
- Context menus
- Input handling
- Four-state fog of war

---

## Scene structure and CanvasLayer stacking

Build every UI panel as a separate `.tscn` with its own C# controller. CanvasLayer `Layer` values define absolute draw order:

```
Root
├── GameWorld (Node2D)
├── UILayer          (CanvasLayer, Layer = 10)
│   └── UIRoot       (Control, full-rect anchors)
│       ├── HUD      (MarginContainer)   ← always visible
│       ├── Panels   (Control)           ← collapsible management panels
│       └── Sidebar  (Control)
├── NotifyLayer      (CanvasLayer, Layer = 20)
├── ModalLayer       (CanvasLayer, Layer = 30)
└── TooltipLayer     (CanvasLayer, Layer = 100)
```

Tooltips always above modals, modals above notifications — guaranteed regardless of node ZIndex.

Organize exports with groups on complex controllers:

```csharp
[ExportGroup("Needs Display")]
[Export] public TextureProgressBar HungerBar { get; set; }
[Export] public TextureProgressBar ThirstBar { get; set; }

[ExportGroup("Queue Panel")]
[Export] public VBoxContainer QueueContainer { get; set; }
[Export] public PackedScene ActivityCardScene { get; set; }
```

---

## Theming

Apply one `Theme` to `UIRoot`. All children inherit. Build programmatically for version-control:

```csharp
public static Theme BuildNecromancerTheme()
{
    var theme = new Theme();

    var panelStyle = new StyleBoxFlat();
    panelStyle.BgColor = new Color(0.08f, 0.08f, 0.12f, 0.95f);
    panelStyle.SetBorderWidthAll(1);
    panelStyle.BorderColor = new Color("3a3a5a");
    panelStyle.SetCornerRadiusAll(4);
    theme.SetStylebox("panel", "PanelContainer", panelStyle);

    // Type variations for reusable named styles
    theme.SetTypeVariation("HeaderLabel", "Label");
    theme.SetFontSize("font_size", "HeaderLabel", 24);
    theme.SetColor("font_color", "HeaderLabel", new Color("ffcc00"));

    theme.SetTypeVariation("CriticalLabel", "Label");
    theme.SetColor("font_color", "CriticalLabel", Colors.Red);

    return theme;
}
```

Apply a variation: `myLabel.ThemeTypeVariation = "HeaderLabel";`

**Always `Duplicate()` StyleBoxes before modifying** — they are reference types shared across nodes.

---

## Animated transitions

Prefer Tween over AnimationPlayer for runtime UI. Always kill before restarting:

```csharp
private Tween _tween;

public void SlideIn()
{
    _tween?.Kill();
    _tween = CreateTween().SetParallel(true);
    _tween.TweenProperty(this, "position:x", 0f, 0.35f)
          .SetTrans(Tween.TransitionType.Back)
          .SetEase(Tween.EaseType.Out);
    _tween.TweenProperty(this, "modulate:a", 1f, 0.2f);
}
```

`TransitionType.Back` with `EaseType.Out` for slide-in (slight overshoot feels responsive).

---

## Toast notification system

CanvasLayer manager instancing PackedScene toasts into VBoxContainer with Tween auto-dismiss:

```csharp
public void ShowNotification(string message, Priority priority, float duration = 3f)
{
    var toast = _toastScene.Instantiate<NotificationToast>();
    _container.AddChild(toast);
    toast.Setup(message, priority);

    if (priority != Priority.Persistent)
    {
        var tween = CreateTween();
        tween.TweenInterval(duration);
        tween.TweenProperty(toast, "modulate:a", 0f, 0.3f);
        tween.TweenCallback(Callable.From(() => {
            toast.QueueFree();
            ProcessQueue();
        }));
    }
}
```

Cap visible at 4–5, queue the rest.

---

## Custom tooltips

Override `_MakeCustomTooltip` for rich tooltips. Set `TooltipText = " "` (space) to trigger when no plain text:

```csharp
public override Control _MakeCustomTooltip(string forText)
{
    var panel = new PanelContainer();
    var vbox = new VBoxContainer();
    panel.AddChild(vbox);
    vbox.AddChild(new Label { Text = _statName, ThemeTypeVariation = "HeaderLabel" });

    foreach (var mod in _modifiers)
    {
        var row = new HBoxContainer();
        // TooltipText on sub-labels enables another nesting level
        row.AddChild(new Label { Text = mod.Source, TooltipText = mod.Description });
        row.AddChild(new Label { Text = mod.Value.ToString("+0;-0") });
        vbox.AddChild(row);
    }

    panel.CustomMinimumSize = new Vector2(220, 0);
    return panel;
}
```

For full CK3-style nested tooltips, use the **Nested Tooltips addon (Asset Library #4260)** — C#-native, handles positioning and nesting lifecycle.

---

## Drag-and-drop (activity queue)

```csharp
public partial class ActivityCard : PanelContainer
{
    [Signal] public delegate void ReorderRequestedEventHandler(int fromIndex, int toIndex);
    public int QueueIndex { get; set; }

    public override Variant _GetDragData(Vector2 atPosition)
    {
        var preview = Duplicate() as Control;
        preview!.Modulate = new Color(1, 1, 1, 0.6f);
        SetDragPreview(preview);
        return new Godot.Collections.Dictionary {
            ["type"] = "activity_card",
            ["index"] = QueueIndex
        };
    }

    public override bool _CanDropData(Vector2 at, Variant data)
    {
        if (data.VariantType != Variant.Type.Dictionary) return false;
        return data.AsGodotDictionary().TryGetValue("type", out var t)
               && t.AsString() == "activity_card";
    }

    public override void _DropData(Vector2 at, Variant data)
    {
        int from = data.AsGodotDictionary()["index"].AsInt32();
        int to = at.Y > Size.Y / 2 ? QueueIndex + 1 : QueueIndex;
        EmitSignal(SignalName.ReorderRequested, from, to);
    }
}
```

---

## Context menus

Use `PopupMenu` with dynamic content. Define `IContextMenuProvider` so clickable entities declare their own items:

```csharp
public interface IContextMenuProvider
{
    IEnumerable<ContextMenuItem> GetContextMenuItems();
}
```

Trigger from `_GuiInput` for UI elements, `_UnhandledInput` for game-world objects. Use `AddSubmenuNodeItem` for nested submenus. PopupMenu auto-closes on outside click.

---

## Input handling

All gameplay input in `_UnhandledInput` — UI Controls consume events before they get there:

```csharp
public override void _UnhandledInput(InputEvent @event)
{
    if (@event is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } mb)
    {
        HandleWorldClick(mb.GlobalPosition);
        GetViewport().SetInputAsHandled();
    }
}
```

**`MouseFilter = MouseFilterEnum.Ignore`** on every non-interactive Control. Default `Stop` silently blocks clicks.

**Modal input blocking** via stack:

```csharp
private readonly Stack<IInputMode> _modeStack = new();

public override void _UnhandledInput(InputEvent @event)
{
    if (_modeStack.Count > 0 && _modeStack.Peek().HandleInput(@event))
        GetViewport().SetInputAsHandled();
}
```

**Cache StringName** for hot-path actions:

```csharp
private static readonly StringName ActionPause = "toggle_simulation_pause";
if (@event.IsActionPressed(ActionPause)) TogglePause();
```

---

## Four-state fog of war

One C# array → one Image → one ImageTexture → one fullscreen shader. No per-tile overhead:

```csharp
public enum FogState : byte { Unknown = 0, StaleOld = 64, StaleRecent = 128, Observed = 255 }

private FogState[,] _fog;
private Image _fogImage;
private ImageTexture _fogTexture;

public void RevealArea(Vector2I center, int radius)
{
    // Demote current Observed → StaleRecent
    for (int x = 0; x < _fog.GetLength(0); x++)
        for (int y = 0; y < _fog.GetLength(1); y++)
            if (_fog[x, y] == FogState.Observed)
                _fog[x, y] = FogState.StaleRecent;

    // Mark visible tiles
    for (int dx = -radius; dx <= radius; dx++)
        for (int dy = -radius; dy <= radius; dy++)
        {
            if (dx*dx + dy*dy > radius*radius) continue;
            int tx = center.X+dx, ty = center.Y+dy;
            if (tx >= 0 && ty >= 0 && tx < _fog.GetLength(0) && ty < _fog.GetLength(1))
                _fog[tx, ty] = FogState.Observed;
        }

    UploadFogTexture();
}

private void UploadFogTexture()
{
    for (int x = 0; x < _fog.GetLength(0); x++)
        for (int y = 0; y < _fog.GetLength(1); y++)
            _fogImage.SetPixel(x, y, new Color((byte)_fog[x,y] / 255f, 0, 0));
    _fogTexture.Update(_fogImage); // efficient — no GPU reallocation
}
```

Shader reads red channel: 0=black (Unknown), 0.25=heavy desaturation (StaleOld), 0.5=light desaturation (StaleRecent), 1=clear (Observed). Use `filter_nearest` for sharp tile edges.

Minimap reuses the same `_fogTexture` — updates are free. Screen-edge vignette uses a fullscreen ColorRect on a high CanvasLayer with a shader controlled by a `vignette_intensity` uniform tweened from C#.
