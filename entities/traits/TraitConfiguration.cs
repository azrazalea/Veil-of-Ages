using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using VeilOfAges.Core.Lib;

namespace VeilOfAges.Entities.Traits;

/// <summary>
/// Configuration object that passes parameters to traits during configuration.
/// Merges JSON static parameters with runtime parameters.
/// </summary>
public class TraitConfiguration
{
    /// <summary>
    /// Gets stores all parameters for trait configuration.
    /// </summary>
    public Dictionary<string, object?> Parameters { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="TraitConfiguration"/> class.
    /// Creates a new TraitConfiguration with optional initial parameters.
    /// </summary>
    /// <param name="parameters">Initial parameters dictionary, or null for empty configuration.</param>
    public TraitConfiguration(Dictionary<string, object?>? parameters = null)
    {
        Parameters = parameters ?? new Dictionary<string, object?>();
    }

    /// <summary>
    /// Gets a typed value from the configuration.
    /// </summary>
    /// <typeparam name="T">The expected type of the value.</typeparam>
    /// <param name="key">The parameter key.</param>
    /// <returns>The value cast to T, or default if not found or wrong type.</returns>
    public T? Get<T>(string key)
    {
        if (!Parameters.TryGetValue(key, out var value) || value == null)
        {
            return default;
        }

        if (value is T typedValue)
        {
            return typedValue;
        }

        // Handle JsonElement conversion
        if (value is JsonElement jsonElement)
        {
            try
            {
                return jsonElement.Deserialize<T>();
            }
            catch (JsonException ex)
            {
                Log.Warn($"TraitConfiguration: Failed to deserialize JsonElement for key '{key}' to type {typeof(T).Name}: {ex.Message}");
                return default;
            }
        }

        // Try direct cast as last resort
        try
        {
            return (T)Convert.ChangeType(value, typeof(T), CultureInfo.InvariantCulture);
        }
        catch
        {
            Log.Warn($"TraitConfiguration: Value for key '{key}' is not of type {typeof(T).Name}");
            return default;
        }
    }

    /// <summary>
    /// Gets a required typed value from the configuration.
    /// </summary>
    /// <typeparam name="T">The expected type of the value.</typeparam>
    /// <param name="key">The parameter key.</param>
    /// <returns>The value cast to T.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the key is missing or value cannot be converted.</exception>
    public T GetRequired<T>(string key)
    {
        if (!Parameters.TryGetValue(key, out var value))
        {
            throw new InvalidOperationException($"Required parameter '{key}' is missing from trait configuration.");
        }

        if (value == null)
        {
            throw new InvalidOperationException($"Required parameter '{key}' is null in trait configuration.");
        }

        if (value is T typedValue)
        {
            return typedValue;
        }

        // Handle JsonElement conversion
        if (value is JsonElement jsonElement)
        {
            try
            {
                var result = jsonElement.Deserialize<T>() ?? throw new InvalidOperationException($"Required parameter '{key}' deserialized to null.");
                return result;
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException($"Required parameter '{key}' could not be deserialized to type {typeof(T).Name}: {ex.Message}", ex);
            }
        }

        // Try direct cast as last resort
        try
        {
            var converted = Convert.ChangeType(value, typeof(T), CultureInfo.InvariantCulture);
            if (converted is T result)
            {
                return result;
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Required parameter '{key}' could not be converted to type {typeof(T).Name}: {ex.Message}", ex);
        }

        throw new InvalidOperationException($"Required parameter '{key}' is not of type {typeof(T).Name}.");
    }

    /// <summary>
    /// Convenience method to get a string value.
    /// </summary>
    /// <param name="key">The parameter key.</param>
    /// <returns>The string value, or null if not found.</returns>
    public string? GetString(string key)
    {
        if (!Parameters.TryGetValue(key, out var value) || value == null)
        {
            return null;
        }

        if (value is string stringValue)
        {
            return stringValue;
        }

        if (value is JsonElement jsonElement && jsonElement.ValueKind == JsonValueKind.String)
        {
            return jsonElement.GetString();
        }

        return value.ToString();
    }

    /// <summary>
    /// Convenience method to get an int value.
    /// Handles JsonElement conversion automatically.
    /// </summary>
    /// <param name="key">The parameter key.</param>
    /// <returns>The int value, or null if not found or not convertible.</returns>
    public int? GetInt(string key)
    {
        if (!Parameters.TryGetValue(key, out var value) || value == null)
        {
            return null;
        }

        if (value is int intValue)
        {
            return intValue;
        }

        if (value is JsonElement jsonElement)
        {
            if (jsonElement.ValueKind == JsonValueKind.Number)
            {
                try
                {
                    return jsonElement.GetInt32();
                }
                catch (FormatException)
                {
                    Log.Warn($"TraitConfiguration: JsonElement for key '{key}' is not a valid int.");
                    return null;
                }
            }

            return null;
        }

        // Try conversion from other numeric types
        try
        {
            return Convert.ToInt32(value, CultureInfo.InvariantCulture);
        }
        catch
        {
            Log.Warn($"TraitConfiguration: Value for key '{key}' could not be converted to int.");
            return null;
        }
    }

    /// <summary>
    /// Convenience method to get a float value.
    /// Handles JsonElement conversion automatically.
    /// </summary>
    /// <param name="key">The parameter key.</param>
    /// <returns>The float value, or null if not found or not convertible.</returns>
    public float? GetFloat(string key)
    {
        if (!Parameters.TryGetValue(key, out var value) || value == null)
        {
            return null;
        }

        if (value is float floatValue)
        {
            return floatValue;
        }

        if (value is double doubleValue)
        {
            return (float)doubleValue;
        }

        if (value is JsonElement jsonElement)
        {
            if (jsonElement.ValueKind == JsonValueKind.Number)
            {
                try
                {
                    return jsonElement.GetSingle();
                }
                catch (FormatException)
                {
                    Log.Warn($"TraitConfiguration: JsonElement for key '{key}' is not a valid float.");
                    return null;
                }
            }

            return null;
        }

        // Try conversion from other numeric types
        try
        {
            return Convert.ToSingle(value, CultureInfo.InvariantCulture);
        }
        catch
        {
            Log.Warn($"TraitConfiguration: Value for key '{key}' could not be converted to float.");
            return null;
        }
    }

    /// <summary>
    /// Convenience method to get a bool value.
    /// Handles JsonElement conversion automatically.
    /// </summary>
    /// <param name="key">The parameter key.</param>
    /// <returns>The bool value, or null if not found or not convertible.</returns>
    public bool? GetBool(string key)
    {
        if (!Parameters.TryGetValue(key, out var value) || value == null)
        {
            return null;
        }

        if (value is bool boolValue)
        {
            return boolValue;
        }

        if (value is JsonElement jsonElement)
        {
            if (jsonElement.ValueKind == JsonValueKind.True)
            {
                return true;
            }

            if (jsonElement.ValueKind == JsonValueKind.False)
            {
                return false;
            }

            return null;
        }

        // Try conversion from string
        if (value is string stringValue)
        {
            if (bool.TryParse(stringValue, out var parsed))
            {
                return parsed;
            }
        }

        return null;
    }

    /// <summary>
    /// Checks if a key exists in the configuration.
    /// </summary>
    /// <param name="key">The parameter key.</param>
    /// <returns>True if the key exists, false otherwise.</returns>
    public bool Has(string key)
    {
        return Parameters.ContainsKey(key);
    }

    /// <summary>
    /// Gets a Room reference from the configuration.
    /// The value should be stored directly as a Room object in runtime parameters.
    /// </summary>
    /// <param name="key">The parameter key.</param>
    /// <returns>The Room, or null if not found or wrong type.</returns>
    public Entities.Room? GetRoom(string key)
    {
        return Get<Entities.Room>(key);
    }

    /// <summary>
    /// Creates a new TraitConfiguration by merging JSON parameters with runtime parameters.
    /// Runtime parameters override JSON parameters when keys conflict.
    /// </summary>
    /// <param name="jsonParams">Parameters from JSON definition.</param>
    /// <param name="runtimeParams">Runtime parameters that override JSON values.</param>
    /// <returns>A new TraitConfiguration with merged parameters.</returns>
    public static TraitConfiguration Merge(Dictionary<string, object?> jsonParams, Dictionary<string, object?>? runtimeParams)
    {
        var merged = new Dictionary<string, object?>(jsonParams);

        if (runtimeParams != null)
        {
            foreach (var kvp in runtimeParams)
            {
                merged[kvp.Key] = kvp.Value;
            }
        }

        return new TraitConfiguration(merged);
    }
}
