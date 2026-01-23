using System;

namespace VeilOfAges.Entities.Activities;

/// <summary>
/// Static utility class providing duration variance for activities.
/// Creates more natural, human-like timing in entity behaviors.
/// </summary>
public static class ActivityTiming
{
    private static readonly Random _rng = new ();

    /// <summary>
    /// Returns a varied duration based on the base duration with random variance.
    /// </summary>
    /// <param name="baseDuration">The base duration in ticks.</param>
    /// <param name="variancePercent">Variance as percentage (0.2 = ±20%). Default 0.2.</param>
    /// <returns>Varied duration (at least 1 tick).</returns>
    public static uint GetVariedDuration(uint baseDuration, float variancePercent = 0.2f)
    {
        // Calculate variance range
        float variance = baseDuration * variancePercent;
        float minDuration = baseDuration - variance;
        float maxDuration = baseDuration + variance;

        // Get random value in range
        float varied = minDuration + ((float)_rng.NextDouble() * (maxDuration - minDuration));

        // Return at least 1 tick
        return (uint)Math.Max(1, Math.Round(varied));
    }

    /// <summary>
    /// Returns a random break duration within the specified range.
    /// </summary>
    /// <param name="minTicks">Minimum break duration.</param>
    /// <param name="maxTicks">Maximum break duration.</param>
    /// <returns>Random duration between min and max (inclusive).</returns>
    public static uint GetBreakDuration(uint minTicks, uint maxTicks)
    {
        if (minTicks >= maxTicks)
        {
            return minTicks;
        }

        return (uint)_rng.Next((int)minTicks, (int)maxTicks + 1);
    }

    /// <summary>
    /// Randomly determines if an entity should take a break.
    /// </summary>
    /// <param name="probability">Probability of taking a break (0.0 to 1.0). Default 0.15.</param>
    /// <returns>True if break should be taken.</returns>
    public static bool ShouldTakeBreak(float probability = 0.15f)
    {
        return _rng.NextDouble() < probability;
    }
}
