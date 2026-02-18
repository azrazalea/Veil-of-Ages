using System;
using System.Collections.Generic;
using Godot;

namespace VeilOfAges.Core.Lib;

/// <summary>
/// The four phases of a day in the game world.
/// </summary>
public enum DayPhaseType
{
    Dawn,
    Day,
    Dusk,
    Night
}

public enum SeasonType
{
    Spring,
    Summer,
    Autumn,
    Winter
}

/// <summary>
/// Utility class for managing and converting game time in the Veil of Ages time system.
/// Game time is stored in centiseconds (1/100th of a second) as a ulong.
/// Base-56 calendar structure with 28-day months and 13-month years.
///
/// Day/Night Cycle:
/// - 14 hours per day total
/// - 1 hour dawn (always hour 0)
/// - 1 hour dusk (position varies by season)
/// - Remaining 12 hours split between day and night based on season
/// - Base split favors night (necromancy theme): 5 day / 7 night
/// - Summer: 6 day / 6 night (living advantage)
/// - Winter: 4 day / 8 night (undead advantage).
/// </summary>
public class GameTime
{
    // Time constants
    public const ulong CENTISECONDSPERSECOND = 100UL;
    public const ulong SECONDSPERMINUTE = 56UL;
    public const ulong MINUTESPERHOUR = 56UL;
    public const ulong HOURSPERDAY = 14UL;
    public const ulong DAYSPERMONTH = 28UL;
    public const ulong MONTHSPERYEAR = 13UL;

    // Derived constants
    public const ulong CENTISECONDSPERMINUTE = CENTISECONDSPERSECOND * SECONDSPERMINUTE;
    public const ulong CENTISECONDSPERHOUR = CENTISECONDSPERMINUTE * MINUTESPERHOUR;
    public const ulong CENTISECONDSPERDAY = CENTISECONDSPERHOUR * HOURSPERDAY;
    public const ulong CENTISECONDSPERMONTH = CENTISECONDSPERDAY * DAYSPERMONTH;
    public const ulong CENTISECONDSPERYEAR = CENTISECONDSPERMONTH * MONTHSPERYEAR;

    // Realtime
    public const ulong SimulationTickRate = 8; // Ticks per a real second at 1.0 time scale
    public const ulong GameCentisecondsPerRealSecond = 3680;
    public const ulong GameCentisecondsPerGameTick = GameCentisecondsPerRealSecond / SimulationTickRate;

    // Month names
    private static readonly string[] MonthNames =
    [
        "Seedweave",    // Early Spring
        "Marketbloom",  // Mid Spring
        "Tradewind",    // Late Spring
        "Goldtide",     // Early Summer
        "Growthsong",   // Mid Summer
        "Marketfire",   // Late Summer
        "Tradebounty",  // Early Autumn
        "Goldfall",     // Mid Autumn
        "Mistmarket",   // Late Autumn
        "Frostfair",    // Early Winter
        "Deepmarket",   // Mid Winter
        "Starbarter",   // Late Winter
        "Thawcraft" // End Winter
    ];

    // Season mapping
    private static readonly SeasonType[] Seasons =
    [
        SeasonType.Spring, SeasonType.Spring, SeasonType.Spring,
        SeasonType.Summer, SeasonType.Summer, SeasonType.Summer,
        SeasonType.Autumn, SeasonType.Autumn, SeasonType.Autumn,
        SeasonType.Winter, SeasonType.Winter, SeasonType.Winter, SeasonType.Winter
    ];

    // The current game time in centiseconds
    public ulong Value { get; private set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="GameTime"/> class.
    /// Creates a new GameTime instance with the specified game time in centiseconds.
    /// </summary>
    /// <param name="gameTimeInCentiseconds">The game time in centiseconds.</param>
    public GameTime(ulong gameTimeInCentiseconds)
    {
        Value = gameTimeInCentiseconds;
    }

    /// <summary>
    /// Creates a GameTime instance for a specific date.
    /// </summary>
    /// <param name="year">Year (1-based).</param>
    /// <param name="month">Month (1-13).</param>
    /// <param name="day">Day (1-28).</param>
    /// <param name="hour">Hour (0-13).</param>
    /// <param name="minute">Minute (0-55).</param>
    /// <param name="second">Second (0-55).</param>
    /// <param name="centisecond">Centisecond (0-99).</param>
    /// <returns>A new GameTime instance.</returns>
    public static GameTime FromDate(ulong year, ulong month, ulong day, ulong hour = 0, ulong minute = 0, ulong second = 0, ulong centisecond = 0)
    {
        // Validate inputs
        if (year < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(year), "Year must be at least 1");
        }

        if (month is < 1 or > MONTHSPERYEAR)
        {
            throw new ArgumentOutOfRangeException(nameof(month), $"Month must be between 1 and {MONTHSPERYEAR}");
        }

        if (day is < 1 or > DAYSPERMONTH)
        {
            throw new ArgumentOutOfRangeException(nameof(day), $"Day must be between 1 and {DAYSPERMONTH}");
        }

        if (false || hour >= HOURSPERDAY)
        {
            throw new ArgumentOutOfRangeException(nameof(hour), $"Hour must be between 0 and {HOURSPERDAY - 1}");
        }

        if (false || minute >= MINUTESPERHOUR)
        {
            throw new ArgumentOutOfRangeException(nameof(minute), $"Minute must be between 0 and {MINUTESPERHOUR - 1}");
        }

        if (false || second >= SECONDSPERMINUTE)
        {
            throw new ArgumentOutOfRangeException(nameof(second), $"Second must be between 0 and {SECONDSPERMINUTE - 1}");
        }

        if (false || centisecond >= CENTISECONDSPERSECOND)
        {
            throw new ArgumentOutOfRangeException(nameof(centisecond), $"Centisecond must be between 0 and {CENTISECONDSPERSECOND - 1}");
        }

        // Calculate total centiseconds
        ulong totalCentiseconds =
            ((year - 1) * CENTISECONDSPERYEAR) +
            ((month - 1) * CENTISECONDSPERMONTH) +
            ((day - 1) * CENTISECONDSPERDAY) +
            (hour * CENTISECONDSPERHOUR) +
            (minute * CENTISECONDSPERMINUTE) +
            (second * CENTISECONDSPERSECOND) +
            centisecond;

        return new GameTime(totalCentiseconds);
    }

    /// <summary>
    /// Creates a GameTime from game ticks.
    /// </summary>
    /// <param name="ticks">Number of game ticks since simulation start.</param>
    /// <returns>A new GameTime instance.</returns>
    public static GameTime FromTicks(ulong ticks)
    {
        return new GameTime(ticks * GameCentisecondsPerGameTick);
    }

    /// <summary>
    /// Gets the year component (0-based) of the game time.
    /// </summary>
    public ulong Year => (Value / CENTISECONDSPERYEAR) + 1;

    /// <summary>
    /// Gets the month component (1-13) of the game time.
    /// </summary>
    public ulong Month => ((Value % CENTISECONDSPERYEAR) / CENTISECONDSPERMONTH) + 1;

    /// <summary>
    /// Gets the day component (1-28) of the game time.
    /// </summary>
    public ulong Day => ((Value % CENTISECONDSPERMONTH) / CENTISECONDSPERDAY) + 1;

    /// <summary>
    /// Gets the hour component (0-13) of the game time.
    /// </summary>
    public ulong Hour => (Value % CENTISECONDSPERDAY) / CENTISECONDSPERHOUR;

    /// <summary>
    /// Gets the minute component (0-55) of the game time.
    /// </summary>
    public ulong Minute => (Value % CENTISECONDSPERHOUR) / CENTISECONDSPERMINUTE;

    /// <summary>
    /// Gets the second component (0-55) of the game time.
    /// </summary>
    public ulong Second => (Value % CENTISECONDSPERMINUTE) / CENTISECONDSPERSECOND;

    /// <summary>
    /// Gets the centisecond component (0-99) of the game time.
    /// </summary>
    public ulong Centisecond => Value % CENTISECONDSPERSECOND;

    /// <summary>
    /// Gets the internal name of the current month (for logging/data).
    /// </summary>
    public string MonthName => MonthNames[Month - 1];

    /// <summary>
    /// Gets the localized name of the current month (for display).
    /// </summary>
    public string LocalizedMonthName => L.Tr($"time.month.{MonthName.ToUpperInvariant()}");

    /// <summary>
    /// Gets the current season.
    /// </summary>
    public SeasonType Season => Seasons[Month - 1];

    /// <summary>
    /// Gets the localized season name (for display).
    /// </summary>
    public string LocalizedSeason => L.Tr($"time.season.{Season.ToString().ToUpperInvariant()}");

    /// <summary>
    /// Gets the total days elapsed since the start of time.
    /// </summary>
    public ulong TotalDays => Value / CENTISECONDSPERDAY;

    /// <summary>
    /// Gets the day of the year (1-364).
    /// </summary>
    public ulong DayOfYear => ((Month - 1) * DAYSPERMONTH) + Day;

    /// <summary>
    /// Gets the number of daylight hours for the current season (excluding dawn/dusk).
    /// Base: 5 hours (Spring/Autumn), Summer: 6 hours, Winter: 4 hours.
    /// </summary>
    public ulong DayHours => Season switch
    {
        SeasonType.Summer => 6,
        SeasonType.Winter => 4,
        _ => 5 // Spring, Autumn
    };

    /// <summary>
    /// Gets the number of night hours for the current season.
    /// Base: 7 hours (Spring/Autumn), Summer: 6 hours, Winter: 8 hours.
    /// </summary>
    public ulong NightHours => Season switch
    {
        SeasonType.Summer => 6,
        SeasonType.Winter => 8,
        _ => 7 // Spring, Autumn
    };

    /// <summary>
    /// Gets the hour when dawn starts (always 0).
    /// </summary>
    public static ulong DawnStartHour => 0;

    /// <summary>
    /// Gets the hour when full daylight begins (always 1, after dawn).
    /// </summary>
    public static ulong DayStartHour => 1;

    /// <summary>
    /// Gets the hour when dusk begins (varies by season).
    /// </summary>
    public ulong DuskStartHour => DayStartHour + DayHours;

    /// <summary>
    /// Gets the hour when night begins (one hour after dusk starts).
    /// </summary>
    public ulong NightStartHour => DuskStartHour + 1;

    /// <summary>
    /// Gets the current phase of the day based on hour and season.
    /// </summary>
    public DayPhaseType CurrentDayPhase
    {
        get
        {
            ulong hour = Hour;
            if (hour < DayStartHour)
            {
                return DayPhaseType.Dawn;
            }

            if (hour < DuskStartHour)
            {
                return DayPhaseType.Day;
            }

            if (hour < NightStartHour)
            {
                return DayPhaseType.Dusk;
            }

            return DayPhaseType.Night;
        }
    }

    /// <summary>
    /// Gets the phase of the day as a string (for backwards compatibility and display).
    /// </summary>
    public string DayPhase => CurrentDayPhase.ToString();

    /// <summary>
    /// Gets a value indicating whether returns true if it is currently full daylight (not dawn, dusk, or night).
    /// </summary>
    public bool IsDaytime => CurrentDayPhase == DayPhaseType.Day;

    /// <summary>
    /// Gets a value indicating whether returns true if it is currently night (not dawn, dusk, or day).
    /// </summary>
    public bool IsNighttime => CurrentDayPhase == DayPhaseType.Night;

    /// <summary>
    /// Gets a value indicating whether returns true if it is currently a twilight period (dawn or dusk).
    /// </summary>
    public bool IsTwilight => CurrentDayPhase is DayPhaseType.Dawn or DayPhaseType.Dusk;

    /// <summary>
    /// Gets a value indicating whether returns true if it is dark enough for light-sensitive undead to be comfortable.
    /// This includes night and dusk (sun is setting/set).
    /// </summary>
    public bool IsDark => CurrentDayPhase is DayPhaseType.Night or DayPhaseType.Dusk;

    /// <summary>
    /// Gets a value indicating whether returns true if sunlight is present (dangerous for vampires, etc.).
    /// This includes day and dawn (sun is rising/up).
    /// </summary>
    public bool HasSunlight => CurrentDayPhase is DayPhaseType.Day or DayPhaseType.Dawn;

    /// <summary>
    /// Gets the current daylight level as a value from 0.0 (full dark) to 1.0 (full bright).
    /// Includes smooth transitions within dawn and dusk hours based on minutes.
    /// </summary>
    public float DaylightLevel
    {
        get
        {
            _ = Hour;
            float minuteProgress = Minute / (float)MINUTESPERHOUR;

            return CurrentDayPhase switch
            {
                // Dawn: transition from 0.1 to 1.0 over the hour
                DayPhaseType.Dawn => 0.1f + (0.9f * minuteProgress),

                // Day: full brightness
                DayPhaseType.Day => 1.0f,

                // Dusk: transition from 1.0 to 0.1 over the hour
                DayPhaseType.Dusk => 1.0f - (0.9f * minuteProgress),

                // Night: dark (but not pitch black for gameplay)
                DayPhaseType.Night => 0.1f,
                _ => 1.0f
            };
        }
    }

    /// <summary>
    /// Advances the game time by one game tick.
    /// </summary>
    /// <returns>A new GameTime instance with the advanced time by one game tick.</returns>
    public GameTime Advance()
    {
        return new GameTime(Value + GameCentisecondsPerGameTick);
    }

    /// <summary>
    /// Advances the game time by the specified amount of centiseconds.
    /// </summary>
    /// <param name="centiseconds">The amount of centiseconds to advance.</param>
    /// <returns>A new GameTime instance with the advanced time.</returns>
    public GameTime Advance(ulong centiseconds)
    {
        return new GameTime(Value + centiseconds);
    }

    /// <summary>
    /// Advances the game time by the specified amount of game time units.
    /// </summary>
    /// <param name="years">Years to advance.</param>
    /// <param name="months">Months to advance.</param>
    /// <param name="days">Days to advance.</param>
    /// <param name="hours">Hours to advance.</param>
    /// <param name="minutes">Minutes to advance.</param>
    /// <param name="seconds">Seconds to advance.</param>
    /// <param name="centiseconds">Centiseconds to advance.</param>
    /// <returns>A new GameTime instance with the advanced time.</returns>
    public GameTime Advance(int years = 0, int months = 0, int days = 0, int hours = 0, int minutes = 0, int seconds = 0, int centiseconds = 0)
    {
        ulong totalCentiseconds =
            ((ulong)years * CENTISECONDSPERYEAR) +
            ((ulong)months * CENTISECONDSPERMONTH) +
            ((ulong)days * CENTISECONDSPERDAY) +
            ((ulong)hours * CENTISECONDSPERHOUR) +
            ((ulong)minutes * CENTISECONDSPERMINUTE) +
            ((ulong)seconds * CENTISECONDSPERSECOND) +
            (ulong)centiseconds;

        return new GameTime(Value + totalCentiseconds);
    }

    /// <summary>
    /// Calculates the real-time duration in seconds for a given game time duration at normal speed.
    /// </summary>
    /// <param name="gameCentiseconds">Game time duration in centiseconds.</param>
    /// <param name="timeScale">Current time scale (1.0 = normal).</param>
    /// <returns>Real time in seconds.</returns>
    public static double GameToRealTime(ulong gameCentiseconds, float timeScale = 1.0f)
    {
        // At normal speed, 8 ticks per real second with 4.6 game seconds per tick
        // This means 36.8 game seconds per real second
        double gameSeconds = gameCentiseconds / (float)CENTISECONDSPERSECOND;
        return gameSeconds / (GameCentisecondsPerRealSecond * timeScale);
    }

    /// <summary>
    /// Calculates the game time duration in centiseconds for a given real time duration at normal speed.
    /// </summary>
    /// <param name="realSeconds">Real time duration in seconds.</param>
    /// <param name="timeScale">Current time scale (1.0 = normal).</param>
    /// <returns>Game time in centiseconds.</returns>
    public static ulong RealToGameTime(float realSeconds, float timeScale = 1.0f)
    {
        // At normal speed, 8 ticks per real second with 4.6 game seconds per tick
        // This means 36.8 game seconds per real second
        const float GAME_SECONDS_PER_REAL_SECOND = 36.8f;

        float gameSeconds = realSeconds * GAME_SECONDS_PER_REAL_SECOND * timeScale;
        return (ulong)(gameSeconds * CENTISECONDSPERSECOND);
    }

    /// <summary>
    /// Returns a TimeSpan representing the game time duration at normal speed.
    /// </summary>
    /// <param name="gameCentiseconds">Game time duration in centiseconds.</param>
    /// <returns>TimeSpan representing the game time duration.</returns>
    public static TimeSpan GameDurationToTimeSpan(ulong gameCentiseconds)
    {
        // This converts game duration to real-world time units for display purposes
        // It is NOT a direct conversion of the base-56 system to base-60
        ulong totalSeconds = gameCentiseconds / CENTISECONDSPERSECOND;

        ulong days = totalSeconds / (24 * 60 * 60);
        totalSeconds %= 24 * 60 * 60;

        ulong hours = totalSeconds / (60 * 60);
        totalSeconds %= 60 * 60;

        ulong minutes = totalSeconds / 60;
        ulong seconds = totalSeconds % 60;

        ulong milliseconds = (gameCentiseconds % CENTISECONDSPERSECOND) * 10;

        return new TimeSpan((int)days, (int)hours, (int)minutes, (int)seconds, (int)milliseconds);
    }

    /// <summary>
    /// Returns a human-readable date string.
    /// </summary>
    /// <param name="includeTime">Whether to include the time in the string.</param>
    /// <returns>A formatted date string.</returns>
    public string ToDateString(bool includeTime = false)
    {
        string date = L.TrFmt("time.format.DATE", Day, LocalizedMonthName, Year);

        if (includeTime)
        {
            string time = $"{Hour:D2}:{Minute:D2}:{Second:D2}";
            return $"{date} {time}";
        }

        return date;
    }

    /// <summary>
    /// Returns a human-readable time string.
    /// </summary>
    /// <returns>A formatted time string.</returns>
    public string ToTimeString()
    {
        return $"{Hour:D2}:{Minute:D2}:{Second:D2}";
    }

    /// <summary>
    /// Returns a human-readable duration string for a game time duration.
    /// </summary>
    /// <param name="gameCentiseconds">Game time duration in centiseconds.</param>
    /// <returns>A formatted duration string.</returns>
    public static string FormatDuration(ulong gameCentiseconds)
    {
        ulong years = gameCentiseconds / CENTISECONDSPERYEAR;
        ulong remaining = gameCentiseconds % CENTISECONDSPERYEAR;

        ulong months = remaining / CENTISECONDSPERMONTH;
        remaining %= CENTISECONDSPERMONTH;

        ulong days = remaining / CENTISECONDSPERDAY;
        remaining %= CENTISECONDSPERDAY;

        ulong hours = remaining / CENTISECONDSPERHOUR;
        remaining %= CENTISECONDSPERHOUR;

        ulong minutes = remaining / CENTISECONDSPERMINUTE;
        remaining %= CENTISECONDSPERMINUTE;

        ulong seconds = remaining / CENTISECONDSPERSECOND;

        List<string> parts = [];

        if (years > 0)
        {
            parts.Add(L.Fmt(L.TrN("time.duration.YEAR_S", "time.duration.YEAR_P", (int)years), years));
        }

        if (months > 0)
        {
            parts.Add(L.Fmt(L.TrN("time.duration.MONTH_S", "time.duration.MONTH_P", (int)months), months));
        }

        if (days > 0)
        {
            parts.Add(L.Fmt(L.TrN("time.duration.DAY_S", "time.duration.DAY_P", (int)days), days));
        }

        if (hours > 0)
        {
            parts.Add(L.Fmt(L.TrN("time.duration.HOUR_S", "time.duration.HOUR_P", (int)hours), hours));
        }

        if (minutes > 0)
        {
            parts.Add(L.Fmt(L.TrN("time.duration.MINUTE_S", "time.duration.MINUTE_P", (int)minutes), minutes));
        }

        if (seconds > 0 || parts.Count == 0)
        {
            parts.Add(L.Fmt(L.TrN("time.duration.SECOND_S", "time.duration.SECOND_P", (int)seconds), seconds));
        }

        if (parts.Count == 1)
        {
            return parts[0];
        }
        else if (parts.Count == 2)
        {
            return L.TrFmt("time.duration.JOIN_TWO", parts[0], parts[1]);
        }
        else
        {
            string lastPart = parts[^1];
            parts.RemoveAt(parts.Count - 1);
            return L.TrFmt("time.duration.JOIN_MANY", string.Join(", ", parts), lastPart);
        }
    }

    /// <summary>
    /// Calculates the real time duration for this game time interval.
    /// </summary>
    /// <param name="timeScale">Current time scale (1.0 = normal).</param>
    /// <returns>Real time in seconds.</returns>
    public double ToRealTimeDuration(float timeScale = 1.0f)
    {
        return GameToRealTime(Value, timeScale);
    }

    /// <summary>
    /// Returns a human-readable description of the relative real-time duration.
    /// </summary>
    /// <param name="gameCentiseconds">Game time duration in centiseconds.</param>
    /// <param name="timeScale">Current time scale (1.0 = normal).</param>
    /// <returns>A description of how long the duration would take in real time.</returns>
    public static string GetRealTimeDescription(ulong gameCentiseconds, float timeScale = 1.0f)
    {
        double seconds = GameToRealTime(gameCentiseconds, timeScale);
        var span = TimeSpan.FromSeconds(seconds);

        string durationPart;
        if (span.TotalDays > 1)
        {
            string daysPart = L.Fmt(L.TrN("time.duration.DAY_S", "time.duration.DAY_P", span.Days), span.Days);
            string hoursPart = L.Fmt(L.TrN("time.duration.HOUR_S", "time.duration.HOUR_P", span.Hours), span.Hours);
            durationPart = L.TrFmt("time.duration.JOIN_TWO", daysPart, hoursPart);
        }
        else if (span.TotalHours > 1)
        {
            string hoursPart = L.Fmt(L.TrN("time.duration.HOUR_S", "time.duration.HOUR_P", span.Hours), span.Hours);
            string minutesPart = L.Fmt(L.TrN("time.duration.MINUTE_S", "time.duration.MINUTE_P", span.Minutes), span.Minutes);
            durationPart = L.TrFmt("time.duration.JOIN_TWO", hoursPart, minutesPart);
        }
        else if (span.TotalMinutes > 1)
        {
            string minutesPart = L.Fmt(L.TrN("time.duration.MINUTE_S", "time.duration.MINUTE_P", span.Minutes), span.Minutes);
            string secondsPart = L.Fmt(L.TrN("time.duration.SECOND_S", "time.duration.SECOND_P", span.Seconds), span.Seconds);
            durationPart = L.TrFmt("time.duration.JOIN_TWO", minutesPart, secondsPart);
        }
        else
        {
            durationPart = L.Fmt(L.TrN("time.duration.SECOND_S", "time.duration.SECOND_P", span.Seconds), span.Seconds);
        }

        return L.TrFmt("time.realtime.ABOUT_IN_REAL_TIME", durationPart);
    }

    /// <summary>
    /// Generates a description of what phase of day/year it is.
    /// Descriptions adapt to the seasonal day/night schedule.
    /// </summary>
    /// <returns>A descriptive string about the current time.</returns>
    public string GetTimeDescription()
    {
        string timeOfDay = GetTimeOfDayDescription();

        string dayPeriodKey = Day switch
        {
            1 => "time.day_period.FIRST_DAY",
            <= 7 => "time.day_period.EARLY_DAYS",
            <= 14 => "time.day_period.MIDDLE",
            <= 21 => "time.day_period.LATER_PART",
            _ => "time.day_period.FINAL_DAYS"
        };

        return L.TrFmt("time.desc.FULL_TIME", timeOfDay, L.Tr(dayPeriodKey), LocalizedMonthName, Year);
    }

    /// <summary>
    /// Gets a descriptive string for the current time of day, accounting for seasonal variation.
    /// </summary>
    /// <returns>A string like "dawn", "mid-morning", "dusk", "midnight", etc.</returns>
    public string GetTimeOfDayDescription()
    {
        ulong hour = Hour;

        // Dawn is always hour 0
        if (hour == DawnStartHour)
        {
            return L.Tr("time.tod.DAWN");
        }

        // Dusk hour varies by season
        if (hour == DuskStartHour)
        {
            return L.Tr("time.tod.DUSK");
        }

        // Day phase descriptions (hours 1 to DuskStartHour-1)
        if (hour >= DayStartHour && hour < DuskStartHour)
        {
            ulong dayProgress = hour - DayStartHour;
            ulong totalDayHours = DayHours;

            // Map progress through day to descriptions
            float progressRatio = dayProgress / (float)totalDayHours;

            if (progressRatio < 0.25f)
            {
                return L.Tr("time.tod.EARLY_MORNING");
            }

            if (progressRatio < 0.4f)
            {
                return L.Tr("time.tod.MID_MORNING");
            }

            if (progressRatio < 0.5f)
            {
                return L.Tr("time.tod.LATE_MORNING");
            }

            if (progressRatio < 0.6f)
            {
                return L.Tr("time.tod.MIDDAY");
            }

            if (progressRatio < 0.75f)
            {
                return L.Tr("time.tod.AFTERNOON");
            }

            return L.Tr("time.tod.LATE_AFTERNOON");
        }

        // Night phase descriptions (hours NightStartHour to 13)
        if (hour >= NightStartHour)
        {
            ulong nightProgress = hour - NightStartHour;
            ulong totalNightHours = NightHours;

            // Map progress through night to descriptions
            float progressRatio = nightProgress / (float)totalNightHours;

            if (progressRatio < 0.25f)
            {
                return L.Tr("time.tod.EARLY_NIGHT");
            }

            if (progressRatio < 0.5f)
            {
                return L.Tr("time.tod.NIGHT");
            }

            if (progressRatio < 0.75f)
            {
                return L.Tr("time.tod.MIDNIGHT");
            }

            return L.Tr("time.tod.LATE_NIGHT");
        }

        return L.Tr("time.tod.UNKNOWN");
    }

    /// <summary>
    /// Returns a string representation of the game time.
    /// </summary>
    /// <returns>A string representation of the game time.</returns>
    public override string ToString()
    {
        return ToDateString(true);
    }
}
