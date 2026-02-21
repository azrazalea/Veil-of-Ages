using Godot;

namespace VeilOfAges.Core.Lib;

/// <summary>
/// Simple static utility for logging memory usage at initialization milestones.
/// Measures process memory (OS-level), managed heap (.NET), Godot object count, and orphan nodes.
/// </summary>
public static class MemoryProfiler
{
    /// <summary>
    /// Log all 4 memory metrics at a labeled checkpoint.
    /// Call this at key initialization milestones to identify memory usage spikes.
    /// Only active in DEBUG builds — compiles to a no-op in release.
    /// </summary>
    [System.Diagnostics.Conditional("DEBUG")]
    public static void Checkpoint(string label)
    {
        var process = System.Diagnostics.Process.GetCurrentProcess();
        var processMemMB = process.WorkingSet64 / 1024.0 / 1024.0;
        var managedMemMB = System.GC.GetTotalMemory(false) / 1024.0 / 1024.0;
        var objectCount = Performance.GetMonitor(Performance.Monitor.ObjectCount);
        var orphanCount = Performance.GetMonitor(Performance.Monitor.ObjectOrphanNodeCount);

        Log.Print($"[MEMORY] {label}: Process={processMemMB:F0}MB, Managed={managedMemMB:F0}MB, Objects={objectCount}, Orphans={orphanCount}");
    }
}
