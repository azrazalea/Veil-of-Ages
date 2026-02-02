using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Diagnostics.Runtime;

namespace VeilOfAges.Core.Lib;

/// <summary>
/// Tracks running tasks and can kill stuck ones while capturing their stack traces using ClrMD.
/// Stack capture happens on a background thread so the game can continue.
/// </summary>
public sealed class TaskTracker : IDisposable
{
    private readonly ConcurrentDictionary<long, TrackedTaskInfo> _tasks = new ();
    private long _nextId;

    public int TimeoutMs { get; set; } = 500;

    private sealed class TrackedTaskInfo
    {
        public required string Name { get; init; }
        public required Task Task { get; init; }
        public required CancellationTokenSource Cts { get; init; }
        public long StartTicks { get; init; }
        public int ManagedThreadId { get; set; } = -1;

        public double ElapsedMs => (Stopwatch.GetTimestamp() - StartTicks) * 1000.0 / Stopwatch.Frequency;
    }

    private sealed class StuckTaskInfo
    {
        public required string Name { get; init; }
        public required int ManagedThreadId { get; init; }
        public required double ElapsedMs { get; init; }
    }

    /// <summary>
    /// Run an action as a tracked task.
    /// </summary>
    public Task Run(string name, Action work)
    {
        var id = Interlocked.Increment(ref _nextId);
        var startTicks = Stopwatch.GetTimestamp();
        var cts = new CancellationTokenSource();

        var task = Task.Run(
            () =>
        {
            // Update our tracked info with the actual thread ID
            if (_tasks.TryGetValue(id, out var info))
            {
                info.ManagedThreadId = Environment.CurrentManagedThreadId;
            }

            try
            {
                work();
            }
            catch (OperationCanceledException)
            {
                // Expected on cancellation
            }
        }, cts.Token);

        var trackedInfo = new TrackedTaskInfo
        {
            Name = name,
            Task = task,
            Cts = cts,
            StartTicks = startTicks
        };

        _tasks[id] = trackedInfo;

        // Auto-remove when done
        task.ContinueWith(_ => _tasks.TryRemove(id, out TrackedTaskInfo? _), TaskScheduler.Default);

        return task;
    }

    /// <summary>
    /// Check for stuck tasks and kill them. Stack traces are captured on a background thread.
    /// </summary>
    /// <param name="currentTick">Current game tick for identifying when the stuck task occurred.</param>
    /// <returns>Number of stuck tasks found and killed.</returns>
    public int CheckAndKillStuck(ulong currentTick)
    {
        var stuckTasks = _tasks.Values
            .Where(t => !t.Task.IsCompleted && t.ElapsedMs > TimeoutMs && t.ManagedThreadId != -1)
            .ToList();

        if (stuckTasks.Count == 0)
        {
            return 0;
        }

        // Collect info we need before canceling
        var stuckInfo = stuckTasks.Select(t => new StuckTaskInfo
        {
            Name = t.Name,
            ManagedThreadId = t.ManagedThreadId,
            ElapsedMs = t.ElapsedMs
        }).ToList();

        // Cancel all stuck tasks
        foreach (var task in stuckTasks)
        {
            task.Cts.Cancel();
        }

        // Capture stacks on a background thread so game can continue
        var threadIds = stuckInfo.Select(s => s.ManagedThreadId).ToHashSet();
        Task.Run(() => CaptureAndLogStacks(currentTick, stuckInfo, threadIds));

        return stuckTasks.Count;
    }

    private static void CaptureAndLogStacks(
        ulong tick,
        List<StuckTaskInfo> stuckInfo,
        HashSet<int> threadIds)
    {
        Dictionary<int, string> stacks;

        try
        {
            stacks = CaptureStacksForThreads(threadIds);
        }
        catch (Exception ex)
        {
            Log.Error($"[Tick {tick}] Failed to capture stacks: {ex.Message}");
            stacks = threadIds.ToDictionary(id => id, _ => "[Stack capture failed]");
        }

        foreach (var info in stuckInfo)
        {
            var sb = new StringBuilder();
            sb.AppendLine(CultureInfo.InvariantCulture, $"\n=== STUCK TASK KILLED (Tick {tick}) ===");
            sb.AppendLine(CultureInfo.InvariantCulture, $"Task: {info.Name}");
            sb.AppendLine(CultureInfo.InvariantCulture, $"Elapsed: {info.ElapsedMs:F0}ms (timeout exceeded)");
            sb.AppendLine(CultureInfo.InvariantCulture, $"ManagedThreadId: {info.ManagedThreadId}");
            sb.AppendLine("--- STACK TRACE ---");

            if (stacks.TryGetValue(info.ManagedThreadId, out var stack))
            {
                sb.Append(stack);
            }
            else
            {
                sb.AppendLine("[No stack available]");
            }

            sb.AppendLine("=========================");

            Log.Error(sb.ToString());
        }
    }

    private static Dictionary<int, string> CaptureStacksForThreads(HashSet<int> threadIds)
    {
        var result = new Dictionary<int, string>();

        using var target = DataTarget.CreateSnapshotAndAttach(Environment.ProcessId);
        var clrVersion = target.ClrVersions.FirstOrDefault();

        if (clrVersion == null)
        {
            foreach (var id in threadIds)
            {
                result[id] = "[No CLR runtime found]";
            }

            return result;
        }

        using var runtime = clrVersion.CreateRuntime();

        foreach (var thread in runtime.Threads)
        {
            if (!threadIds.Contains(thread.ManagedThreadId))
            {
                continue;
            }

            var sb = new StringBuilder();

            if (!thread.IsAlive)
            {
                sb.AppendLine("  [Thread is dead]");
            }
            else
            {
                var frameCount = 0;
                foreach (var frame in thread.EnumerateStackTrace())
                {
                    // Safety limit - EnumerateStackTrace can loop on corrupt stacks
                    if (++frameCount > 100)
                    {
                        sb.AppendLine("  ... (truncated, >100 frames)");
                        break;
                    }

                    if (frame.Method != null)
                    {
                        var method = frame.Method;
                        var typeName = method.Type?.Name ?? "???";
                        sb.AppendLine(CultureInfo.InvariantCulture, $"  at {typeName}.{method.Name}()");
                    }
                    else
                    {
                        sb.AppendLine(CultureInfo.InvariantCulture, $"  [Native: 0x{frame.InstructionPointer:X}]");
                    }
                }

                if (frameCount == 0)
                {
                    sb.AppendLine("  [No managed frames - thread may be in native code]");
                }
            }

            result[thread.ManagedThreadId] = sb.ToString();
        }

        // Mark any threads we didn't find
        foreach (var id in threadIds)
        {
            if (!result.ContainsKey(id))
            {
                result[id] = "  [Thread not found in snapshot]";
            }
        }

        return result;
    }

    /// <summary>
    /// Get all tracked tasks.
    /// </summary>
    public IEnumerable<Task> GetAllTasks() => _tasks.Values.Select(t => t.Task);

    /// <summary>
    /// Check if any tasks are still running.
    /// </summary>
    public bool HasActiveTasks() => _tasks.Values.Any(t => !t.Task.IsCompleted);

    public void Dispose()
    {
        foreach (var task in _tasks.Values)
        {
            task.Cts.Cancel();
        }

        _tasks.Clear();
    }
}
