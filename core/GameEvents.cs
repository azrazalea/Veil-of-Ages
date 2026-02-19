using System;

namespace VeilOfAges.Core;

/// <summary>
/// Static event bus for broadcasting game-wide events to decoupled listeners.
/// </summary>
public static class GameEvents
{
    public static event Action? UITickFired;
    public static event Action<bool>? SimulationPauseChanged;  // bool paused
    public static event Action<float>? TimeScaleChanged;       // float scale
    public static event Action? CommandQueueChanged;
    public static event Action<bool>? AutomationToggled;       // bool isAutomated
    public static event Action<bool>? DialogueStateChanged;    // bool open

    internal static void FireUITick() => UITickFired?.Invoke();
    internal static void FireSimulationPauseChanged(bool paused) => SimulationPauseChanged?.Invoke(paused);
    internal static void FireTimeScaleChanged(float scale) => TimeScaleChanged?.Invoke(scale);
    internal static void FireCommandQueueChanged() => CommandQueueChanged?.Invoke();
    internal static void FireAutomationToggled(bool isAutomated) => AutomationToggled?.Invoke(isAutomated);
    internal static void FireDialogueStateChanged(bool open) => DialogueStateChanged?.Invoke(open);
}
