# Architecture Patterns

## Table of contents

- Hybrid update model
- Simulation interfaces
- Service locator
- Discrete event bus
- Throttled UI reads
- Node pooling patterns

---

## Hybrid update model

UI panels get data through two channels:

| Channel | What it covers | Mechanism | Frequency |
|---|---|---|---|
| Throttled reads | Needs, health, activity progress, resource counts, entity status | Panel reads interface properties via `_Process` timer | 2–4 fps |
| Discrete events | Notifications, mode changes, queue mutations, familiar returns, combat start/end | C# `event Action<T>` on a static event bus | Immediate |

**Why not per-property events for everything?** The simulation ticks at 8/sec. Reading a handful of floats and strings at 2–4 fps costs nothing. Per-property events (`PopulationChanged`, `GoldChanged`, etc.) create massive interface proliferation and add thread-safety complexity (entity Think() runs on background threads) for zero practical benefit.

**Why not pure polling for everything?** Notifications must trigger toasts, audio, and possibly force-pause. Queue mutations must trigger slide animations. Mode switches need instant visual feedback. Polling current state can't tell you *what happened* or *why* — only what's there now.

---

## Simulation interfaces

Simulation systems expose C# interfaces with readable properties. No per-property change events — UI reads these on a throttle:

```csharp
public interface IPlayerState
{
    string CharacterName { get; }
    float Health { get; }
    IReadOnlyDictionary<string, float> Needs { get; }
    IReadOnlyList<ActivityData> ActivityQueue { get; }
    string CurrentActivityDescription { get; }
}

public interface IKingdomState
{
    int Population { get; }
    int Gold { get; }
    IReadOnlyList<string> ActiveCommands { get; }
}
```

UI never sees concrete simulation types — only interfaces.

---

## Service locator

All UI panels resolve simulation interfaces through a static service locator — never through `GetNode` paths:

```csharp
public static class Services
{
    private static readonly Dictionary<Type, object> _services = new();
    public static void Register<T>(T service) => _services[typeof(T)] = service;
    public static T Get<T>() => (T)_services[typeof(T)];
}

// Simulation registers itself:
Services.Register<IPlayerState>(this);
Services.Register<IKingdomState>(this);

// Any UI panel resolves what it needs:
var player = Services.Get<IPlayerState>();
```

For more sophisticated DI, **Chickensoft AutoInject** provides reflection-free, source-generated DI scoped to scene subtrees. Start with the service locator; consider AutoInject if you need per-scene DI scoping or test isolation.

---

## Discrete event bus

For things that need instant UI reaction. Keep events **coarse and meaningful** — one per happening, not one per property:

```csharp
public static class GameEvents
{
    // Trigger toasts, audio, possible forced pause
    public static event Action<string, NotificationPriority> NotificationRaised;

    // Trigger panel transformation, animation, audio cue
    public static event Action<GameMode> ModeChanged;

    // Trigger slide/reorder animations in the queue display
    public static event Action<QueueMutation> QueueMutated;

    // Trigger report display, order lifecycle updates
    public static event Action<FamiliarReport> FamiliarReturned;

    // Trigger minimap and fog overlay refresh
    public static event Action<Vector2I, int> FogUpdated;

    public static void RaiseNotification(string msg, NotificationPriority p)
        => NotificationRaised?.Invoke(msg, p);
    public static void ChangeMode(GameMode mode) => ModeChanged?.Invoke(mode);
}
```

Subscribe in `_EnterTree`, unsubscribe in `_ExitTree`:

```csharp
public override void _EnterTree()
{
    GameEvents.ModeChanged += OnModeChanged;
    GameEvents.NotificationRaised += OnNotification;
}

public override void _ExitTree()
{
    GameEvents.ModeChanged -= OnModeChanged;
    GameEvents.NotificationRaised -= OnNotification;
}
```

Use C# `event Action<T>`, not Godot signals — no Variant marshalling, compile-time type safety.

---

## Throttled UI reads

For continuous state (needs, health, progress, resources), read on a timer in `_Process`:

```csharp
public partial class NeedsPanel : PanelContainer
{
    private IPlayerState _player;
    private float _timer;
    private const float ReadInterval = 0.25f; // 4 fps

    public override void _EnterTree()
    {
        _player = Services.Get<IPlayerState>();
    }

    public override void _Process(double delta)
    {
        _timer += (float)delta;
        if (_timer < ReadInterval) return;
        _timer = 0f;

        foreach (var (needId, value) in _player.Needs)
            UpdateNeedBar(needId, value);

        _targetHealth = _player.Health;
    }
}
```

For **visual interpolation** (smooth health bar), lerp every frame toward the target that updates at the throttled interval:

```csharp
if (Mathf.Abs((float)_healthBar.Value - _targetHealth) > 0.01f)
    _healthBar.Value = Mathf.Lerp(
        (float)_healthBar.Value, _targetHealth,
        1.0f - Mathf.Exp(-10f * (float)delta));
```

---

## Node pooling patterns

### Simple text lists — use ItemList

Single node managing items internally, no scene tree nodes per entry:

```csharp
private List<string> _lastItems = new();

public void UpdateList(IReadOnlyList<string> items)
{
    if (items.SequenceEqual(_lastItems)) return;
    Clear();
    foreach (var item in items) AddItem(item);
    _lastItems = items.ToList();
}
```

### Custom per-item layouts — pool with incremental diffing

```csharp
public partial class ActivityQueueUI : VBoxContainer
{
    [Export] public PackedScene CardScene { get; set; }
    private readonly List<ActivityCard> _active = new();
    private readonly Queue<ActivityCard> _pool = new();

    public void Refresh(IReadOnlyList<ActivityData> activities)
    {
        int reuse = Math.Min(_active.Count, activities.Count);

        // Update existing cards in-place
        for (int i = 0; i < reuse; i++)
            _active[i].Bind(activities[i]);

        // Spawn new from pool
        for (int i = reuse; i < activities.Count; i++)
        {
            var card = _pool.TryDequeue(out var c) ? c : CardScene.Instantiate<ActivityCard>();
            if (!card.IsInsideTree()) AddChild(card);
            card.Visible = true;
            card.Bind(activities[i]);
            _active.Add(card);
        }

        // Return excess to pool
        for (int i = activities.Count; i < _active.Count; i++)
        {
            _active[i].Visible = false;
            _pool.Enqueue(_active[i]);
        }
        if (activities.Count < _active.Count)
            _active.RemoveRange(activities.Count, _active.Count - activities.Count);
    }
}
```

**Performance context:**
- Godot 4 C# node instantiation is ~60x slower than Godot 3.5 Mono for custom nodes
- `QueueFree` triggers deferred cleanup, GC pressure, tree notifications, and container relayout
- Deleting many children has O(n²) behavior due to index shifting
- `Visible = false` + pool is fastest for lists under ~20 items
