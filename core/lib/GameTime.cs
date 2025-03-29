using System;
using System.Collections.Generic;
using Godot;

namespace VeilOfAges.Core.Lib
{
    /// <summary>
    /// Utility class for managing and converting game time in the Veil of Ages time system.
    /// Game time is stored in centiseconds (1/100th of a second) as a ulong.
    /// Base-56 calendar structure with 28-day months and 13-month years.
    /// </summary>
    public class GameTime
    {
        // Time constants
        public const ulong CENTISECONDS_PER_SECOND = 100UL;
        public const ulong SECONDS_PER_MINUTE = 56UL;
        public const ulong MINUTES_PER_HOUR = 56UL;
        public const ulong HOURS_PER_DAY = 14UL;
        public const ulong DAYS_PER_MONTH = 28UL;
        public const ulong MONTHS_PER_YEAR = 13UL;

        // Derived constants
        public const ulong CENTISECONDS_PER_MINUTE = CENTISECONDS_PER_SECOND * SECONDS_PER_MINUTE;
        public const ulong CENTISECONDS_PER_HOUR = CENTISECONDS_PER_MINUTE * MINUTES_PER_HOUR;
        public const ulong CENTISECONDS_PER_DAY = CENTISECONDS_PER_HOUR * HOURS_PER_DAY;
        public const ulong CENTISECONDS_PER_MONTH = CENTISECONDS_PER_DAY * DAYS_PER_MONTH;
        public const ulong CENTISECONDS_PER_YEAR = CENTISECONDS_PER_MONTH * MONTHS_PER_YEAR;

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
            "Thawcraft"     // End Winter
        ];

        // Season mapping
        private static readonly string[] Seasons = new string[]
        {
            "Spring", "Spring", "Spring",
            "Summer", "Summer", "Summer",
            "Autumn", "Autumn", "Autumn",
            "Winter", "Winter", "Winter", "Winter"
        };

        // The current game time in centiseconds
        public ulong Value { get; private set; }

        /// <summary>
        /// Creates a new GameTime instance with the specified game time in centiseconds
        /// </summary>
        /// <param name="gameTimeInCentiseconds">The game time in centiseconds</param>
        public GameTime(ulong gameTimeInCentiseconds)
        {
            Value = gameTimeInCentiseconds;
        }

        /// <summary>
        /// Creates a GameTime instance for a specific date
        /// </summary>
        /// <param name="year">Year (1-based)</param>
        /// <param name="month">Month (1-13)</param>
        /// <param name="day">Day (1-28)</param>
        /// <param name="hour">Hour (0-13)</param>
        /// <param name="minute">Minute (0-55)</param>
        /// <param name="second">Second (0-55)</param>
        /// <param name="centisecond">Centisecond (0-99)</param>
        /// <returns>A new GameTime instance</returns>
        public static GameTime FromDate(ulong year, ulong month, ulong day, ulong hour = 0, ulong minute = 0, ulong second = 0, ulong centisecond = 0)
        {
            // Validate inputs
            if (year < 1) throw new ArgumentOutOfRangeException(nameof(year), "Year must be at least 1");
            if (month < 1 || month > MONTHS_PER_YEAR) throw new ArgumentOutOfRangeException(nameof(month), $"Month must be between 1 and {MONTHS_PER_YEAR}");
            if (day < 1 || day > DAYS_PER_MONTH) throw new ArgumentOutOfRangeException(nameof(day), $"Day must be between 1 and {DAYS_PER_MONTH}");
            if (hour < 0 || hour >= HOURS_PER_DAY) throw new ArgumentOutOfRangeException(nameof(hour), $"Hour must be between 0 and {HOURS_PER_DAY - 1}");
            if (minute < 0 || minute >= MINUTES_PER_HOUR) throw new ArgumentOutOfRangeException(nameof(minute), $"Minute must be between 0 and {MINUTES_PER_HOUR - 1}");
            if (second < 0 || second >= SECONDS_PER_MINUTE) throw new ArgumentOutOfRangeException(nameof(second), $"Second must be between 0 and {SECONDS_PER_MINUTE - 1}");
            if (centisecond < 0 || centisecond >= CENTISECONDS_PER_SECOND) throw new ArgumentOutOfRangeException(nameof(centisecond), $"Centisecond must be between 0 and {CENTISECONDS_PER_SECOND - 1}");

            // Calculate total centiseconds
            ulong totalCentiseconds =
                (year - 1) * CENTISECONDS_PER_YEAR +
                (month - 1) * CENTISECONDS_PER_MONTH +
                (day - 1) * CENTISECONDS_PER_DAY +
                hour * CENTISECONDS_PER_HOUR +
                minute * CENTISECONDS_PER_MINUTE +
                second * CENTISECONDS_PER_SECOND +
                centisecond;

            return new GameTime(totalCentiseconds);
        }

        /// <summary>
        /// Gets the year component (0-based) of the game time
        /// </summary>
        public ulong Year => (ulong)(Value / CENTISECONDS_PER_YEAR + 1);

        /// <summary>
        /// Gets the month component (1-13) of the game time
        /// </summary>
        public ulong Month => (ulong)(Value % CENTISECONDS_PER_YEAR / CENTISECONDS_PER_MONTH + 1);

        /// <summary>
        /// Gets the day component (1-28) of the game time
        /// </summary>
        public ulong Day => (ulong)(Value % CENTISECONDS_PER_MONTH / CENTISECONDS_PER_DAY + 1);

        /// <summary>
        /// Gets the hour component (0-13) of the game time
        /// </summary>
        public ulong Hour => (ulong)(Value % CENTISECONDS_PER_DAY / CENTISECONDS_PER_HOUR);

        /// <summary>
        /// Gets the minute component (0-55) of the game time
        /// </summary>
        public ulong Minute => (ulong)(Value % CENTISECONDS_PER_HOUR / CENTISECONDS_PER_MINUTE);

        /// <summary>
        /// Gets the second component (0-55) of the game time
        /// </summary>
        public ulong Second => (ulong)(Value % CENTISECONDS_PER_MINUTE / CENTISECONDS_PER_SECOND);

        /// <summary>
        /// Gets the centisecond component (0-99) of the game time
        /// </summary>
        public ulong Centisecond => (ulong)(Value % CENTISECONDS_PER_SECOND);

        /// <summary>
        /// Gets the name of the current month
        /// </summary>
        public string MonthName => MonthNames[Month - 1];

        /// <summary>
        /// Gets the current season
        /// </summary>
        public string Season => Seasons[Month - 1];

        /// <summary>
        /// Gets the total days elapsed since the start of time
        /// </summary>
        public ulong TotalDays => Value / CENTISECONDS_PER_DAY;

        /// <summary>
        /// Gets the day of the year (1-364)
        /// </summary>
        public ulong DayOfYear => (Month - 1) * (ulong)DAYS_PER_MONTH + Day;

        /// <summary>
        /// Gets the phase of the day (Morning, Afternoon, Evening, Night)
        /// </summary>
        public string DayPhase
        {
            get
            {
                ulong hour = Hour;
                if (hour < 4) return "Morning";
                if (hour < 8) return "Afternoon";
                if (hour < 12) return "Evening";
                return "Night";
            }
        }

        /// <summary>
        /// Advances the game time by one game tick
        /// </summary>
        /// <returns>A new GameTime instance with the advanced time by one game tick</returns>
        public GameTime Advance()
        {
            return new GameTime(Value + GameCentisecondsPerGameTick);
        }

        /// <summary>
        /// Advances the game time by the specified amount of centiseconds
        /// </summary>
        /// <param name="centiseconds">The amount of centiseconds to advance</param>
        /// <returns>A new GameTime instance with the advanced time</returns>
        public GameTime Advance(ulong centiseconds)
        {
            return new GameTime(Value + centiseconds);
        }

        /// <summary>
        /// Advances the game time by the specified amount of game time units
        /// </summary>
        /// <param name="years">Years to advance</param>
        /// <param name="months">Months to advance</param>
        /// <param name="days">Days to advance</param>
        /// <param name="hours">Hours to advance</param>
        /// <param name="minutes">Minutes to advance</param>
        /// <param name="seconds">Seconds to advance</param>
        /// <param name="centiseconds">Centiseconds to advance</param>
        /// <returns>A new GameTime instance with the advanced time</returns>
        public GameTime Advance(int years = 0, int months = 0, int days = 0, int hours = 0, int minutes = 0, int seconds = 0, int centiseconds = 0)
        {
            ulong totalCentiseconds =
                (ulong)years * CENTISECONDS_PER_YEAR +
                (ulong)months * CENTISECONDS_PER_MONTH +
                (ulong)days * CENTISECONDS_PER_DAY +
                (ulong)hours * CENTISECONDS_PER_HOUR +
                (ulong)minutes * CENTISECONDS_PER_MINUTE +
                (ulong)seconds * CENTISECONDS_PER_SECOND +
                (ulong)centiseconds;

            return new GameTime(Value + totalCentiseconds);
        }

        /// <summary>
        /// Calculates the real-time duration in seconds for a given game time duration at normal speed
        /// </summary>
        /// <param name="gameCentiseconds">Game time duration in centiseconds</param>
        /// <param name="timeScale">Current time scale (1.0 = normal)</param>
        /// <returns>Real time in seconds</returns>
        public static double GameToRealTime(ulong gameCentiseconds, float timeScale = 1.0f)
        {
            // At normal speed, 8 ticks per real second with 4.6 game seconds per tick
            // This means 36.8 game seconds per real second

            double gameSeconds = gameCentiseconds / (float)CENTISECONDS_PER_SECOND;
            return gameSeconds / (GameCentisecondsPerRealSecond * timeScale);
        }

        /// <summary>
        /// Calculates the game time duration in centiseconds for a given real time duration at normal speed
        /// </summary>
        /// <param name="realSeconds">Real time duration in seconds</param>
        /// <param name="timeScale">Current time scale (1.0 = normal)</param>
        /// <returns>Game time in centiseconds</returns>
        public static ulong RealToGameTime(float realSeconds, float timeScale = 1.0f)
        {
            // At normal speed, 8 ticks per real second with 4.6 game seconds per tick
            // This means 36.8 game seconds per real second
            const float GAME_SECONDS_PER_REAL_SECOND = 36.8f;

            float gameSeconds = realSeconds * GAME_SECONDS_PER_REAL_SECOND * timeScale;
            return (ulong)(gameSeconds * CENTISECONDS_PER_SECOND);
        }

        /// <summary>
        /// Returns a TimeSpan representing the game time duration at normal speed
        /// </summary>
        /// <param name="gameCentiseconds">Game time duration in centiseconds</param>
        /// <returns>TimeSpan representing the game time duration</returns>
        public static TimeSpan GameDurationToTimeSpan(ulong gameCentiseconds)
        {
            // This converts game duration to real-world time units for display purposes
            // It is NOT a direct conversion of the base-56 system to base-60
            ulong totalSeconds = gameCentiseconds / CENTISECONDS_PER_SECOND;

            ulong days = totalSeconds / (24 * 60 * 60);
            totalSeconds %= 24 * 60 * 60;

            ulong hours = totalSeconds / (60 * 60);
            totalSeconds %= 60 * 60;

            ulong minutes = totalSeconds / 60;
            ulong seconds = totalSeconds % 60;

            ulong milliseconds = gameCentiseconds % CENTISECONDS_PER_SECOND * 10;

            return new TimeSpan((int)days, (int)hours, (int)minutes, (int)seconds, (int)milliseconds);
        }

        /// <summary>
        /// Returns a human-readable date string
        /// </summary>
        /// <param name="includeTime">Whether to include the time in the string</param>
        /// <returns>A formatted date string</returns>
        public string ToDateString(bool includeTime = false)
        {
            string date = $"{Day} {MonthName}, Year {Year}";

            if (includeTime)
            {
                string time = $"{Hour:D2}:{Minute:D2}:{Second:D2}";
                return $"{date} {time}";
            }

            return date;
        }

        /// <summary>
        /// Returns a human-readable time string
        /// </summary>
        /// <returns>A formatted time string</returns>
        public string ToTimeString()
        {
            return $"{Hour:D2}:{Minute:D2}:{Second:D2}";
        }

        /// <summary>
        /// Returns a human-readable duration string for a game time duration
        /// </summary>
        /// <param name="gameCentiseconds">Game time duration in centiseconds</param>
        /// <returns>A formatted duration string</returns>
        public static string FormatDuration(ulong gameCentiseconds)
        {
            ulong years = gameCentiseconds / CENTISECONDS_PER_YEAR;
            ulong remaining = gameCentiseconds % CENTISECONDS_PER_YEAR;

            ulong months = remaining / CENTISECONDS_PER_MONTH;
            remaining %= CENTISECONDS_PER_MONTH;

            ulong days = remaining / CENTISECONDS_PER_DAY;
            remaining %= CENTISECONDS_PER_DAY;

            ulong hours = remaining / CENTISECONDS_PER_HOUR;
            remaining %= CENTISECONDS_PER_HOUR;

            ulong minutes = remaining / CENTISECONDS_PER_MINUTE;
            remaining %= CENTISECONDS_PER_MINUTE;

            ulong seconds = remaining / CENTISECONDS_PER_SECOND;

            List<string> parts = [];

            if (years > 0) parts.Add($"{years} year{(years != 1 ? "s" : "")}");
            if (months > 0) parts.Add($"{months} month{(months != 1 ? "s" : "")}");
            if (days > 0) parts.Add($"{days} day{(days != 1 ? "s" : "")}");
            if (hours > 0) parts.Add($"{hours} hour{(hours != 1 ? "s" : "")}");
            if (minutes > 0) parts.Add($"{minutes} minute{(minutes != 1 ? "s" : "")}");
            if (seconds > 0 || parts.Count == 0) parts.Add($"{seconds} second{(seconds != 1 ? "s" : "")}");

            if (parts.Count == 1)
                return parts[0];
            else if (parts.Count == 2)
                return $"{parts[0]} and {parts[1]}";
            else
            {
                string lastPart = parts[^1];
                parts.RemoveAt(parts.Count - 1);
                return $"{string.Join(", ", parts)} and {lastPart}";
            }
        }

        /// <summary>
        /// Calculates the real time duration for this game time interval
        /// </summary>
        /// <param name="timeScale">Current time scale (1.0 = normal)</param>
        /// <returns>Real time in seconds</returns>
        public double ToRealTimeDuration(float timeScale = 1.0f)
        {
            return GameToRealTime(Value, timeScale);
        }

        /// <summary>
        /// Returns a human-readable description of the relative real-time duration
        /// </summary>
        /// <param name="gameCentiseconds">Game time duration in centiseconds</param>
        /// <param name="timeScale">Current time scale (1.0 = normal)</param>
        /// <returns>A description of how long the duration would take in real time</returns>
        public static string GetRealTimeDescription(ulong gameCentiseconds, float timeScale = 1.0f)
        {
            double seconds = GameToRealTime(gameCentiseconds, timeScale);
            TimeSpan span = TimeSpan.FromSeconds(seconds);

            if (span.TotalDays > 1)
                return $"about {span.Days} day{(span.Days != 1 ? "s" : "")} and {span.Hours} hour{(span.Hours != 1 ? "s" : "")} in real time";
            else if (span.TotalHours > 1)
                return $"about {span.Hours} hour{(span.Hours != 1 ? "s" : "")} and {span.Minutes} minute{(span.Minutes != 1 ? "s" : "")} in real time";
            else if (span.TotalMinutes > 1)
                return $"about {span.Minutes} minute{(span.Minutes != 1 ? "s" : "")} and {span.Seconds} second{(span.Seconds != 1 ? "s" : "")} in real time";
            else
                return $"about {span.Seconds} second{(span.Seconds != 1 ? "s" : "")} in real time";
        }

        /// <summary>
        /// Generates a description of what phase of day/year it is
        /// </summary>
        /// <returns>A descriptive string about the current time</returns>
        public string GetTimeDescription()
        {
            string timeOfDay = Hour switch
            {
                0 => "dawn",
                1 => "early morning",
                2 => "mid-morning",
                3 => "late morning",
                4 => "noon",
                5 => "early afternoon",
                6 => "mid-afternoon",
                7 => "late afternoon",
                8 => "dusk",
                9 => "early evening",
                10 => "evening",
                11 => "night",
                12 => "midnight",
                13 => "late night",
                _ => "unknown time"
            };

            string dayDescription = Day switch
            {
                1 => "the first day of",
                <= 7 => "the early days of",
                <= 14 => "the middle of",
                <= 21 => "the later part of",
                _ => "the final days of"
            };

            return $"It is {timeOfDay} on {dayDescription} {MonthName}, Year {Year}";
        }

        /// <summary>
        /// Returns a string representation of the game time
        /// </summary>
        /// <returns>A string representation of the game time</returns>
        public override string ToString()
        {
            return ToDateString(true);
        }
    }
}
