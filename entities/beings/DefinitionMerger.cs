using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using VeilOfAges.Core.Lib;

namespace VeilOfAges.Entities.Beings;

/// <summary>
/// Generic reflection-based merger for definition inheritance.
/// Automatically handles merging any definition type without explicit field handling.
/// </summary>
public static class DefinitionMerger
{
    /// <summary>
    /// Properties that should not be inherited (identity fields and non-inheritable flags).
    /// </summary>
    private static readonly HashSet<string> _skipProperties = new (StringComparer.OrdinalIgnoreCase)
    {
        "Id",
        "ParentId",
        "Abstract" // Abstract status should never be inherited - defaults to false
    };

    /// <summary>
    /// Properties that require special smart merging instead of simple additive.
    /// </summary>
    private static readonly HashSet<string> _smartMergeProperties = new (StringComparer.OrdinalIgnoreCase)
    {
        "Needs",  // Merge by Id - child overrides parent's needs with same Id
        "Traits", // Merge by TraitType - child overrides parent's traits with same TraitType
        "SpriteLayers" // Merge by Name - child overrides parent's sprite layers with same Name
    };

    /// <summary>
    /// Merges a child definition with a parent definition.
    /// Creates a new instance with merged values.
    /// </summary>
    /// <typeparam name="T">The definition type.</typeparam>
    /// <param name="parent">The parent definition.</param>
    /// <param name="child">The child definition (overrides parent).</param>
    /// <returns>A new merged definition.</returns>
    public static T Merge<T>(T parent, T child)
        where T : class, new()
    {
        if (parent == null)
        {
            return child;
        }

        if (child == null)
        {
            return parent;
        }

        var merged = new T();
        var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.CanWrite);

        foreach (var prop in properties)
        {
            var childValue = prop.GetValue(child);
            var parentValue = prop.GetValue(parent);

            object? mergedValue;

            if (_skipProperties.Contains(prop.Name))
            {
                // Identity fields always use child's value
                mergedValue = childValue;
            }
            else
            {
                mergedValue = MergeValue(prop.PropertyType, parentValue, childValue, prop.Name);
            }

            prop.SetValue(merged, mergedValue);
        }

        return merged;
    }

    /// <summary>
    /// Merges two values based on their type.
    /// </summary>
    private static object? MergeValue(Type type, object? parentValue, object? childValue, string propertyName)
    {
        // Handle null cases
        if (parentValue == null)
        {
            return childValue;
        }

        if (childValue == null)
        {
            return parentValue;
        }

        // String: child wins if non-empty
        if (type == typeof(string))
        {
            var childStr = childValue as string;
            var parentStr = parentValue as string;
            return !string.IsNullOrEmpty(childStr) ? childStr : parentStr;
        }

        // Nullable value types: child wins if has value
        if (IsNullableValueType(type))
        {
            return childValue ?? parentValue;
        }

        // List types: check for smart merge or additive
        if (IsListType(type))
        {
            if (_smartMergeProperties.Contains(propertyName))
            {
                return MergeListsSmart(type, parentValue, childValue, propertyName);
            }

            return MergeLists(type, parentValue, childValue);
        }

        // Dictionary types: merge (child overrides keys)
        if (IsDictionaryType(type))
        {
            return MergeDictionaries(type, parentValue, childValue);
        }

        // Complex objects with properties: recursive merge
        if (type.IsClass && type != typeof(string))
        {
            return MergeObjects(type, parentValue, childValue);
        }

        // Value types (int, float, etc.): child wins if not default
        if (type.IsValueType)
        {
            var defaultValue = Activator.CreateInstance(type);
            return !Equals(childValue, defaultValue) ? childValue : parentValue;
        }

        // Fallback: child wins
        return childValue;
    }

    /// <summary>
    /// Checks if type is a nullable value type.
    /// </summary>
    private static bool IsNullableValueType(Type type)
    {
        return Nullable.GetUnderlyingType(type) != null;
    }

    /// <summary>
    /// Checks if type is a List or derives from IList.
    /// </summary>
    private static bool IsListType(Type type)
    {
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
        {
            return true;
        }

        return type.GetInterfaces().Any(i =>
            i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IList<>));
    }

    /// <summary>
    /// Checks if type is a Dictionary.
    /// </summary>
    private static bool IsDictionaryType(Type type)
    {
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Dictionary<,>))
        {
            return true;
        }

        return type.GetInterfaces().Any(i =>
            i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IDictionary<,>));
    }

    /// <summary>
    /// Merges two lists additively (parent items + child items).
    /// </summary>
    private static object MergeLists(Type listType, object parentValue, object childValue)
    {
        var parentList = (IList)parentValue;
        var childList = (IList)childValue;

        // Get the element type
        _ = listType.IsGenericType
            ? listType.GetGenericArguments()[0]
            : typeof(object);

        // Create new list of the same type
        var mergedList = (IList)Activator.CreateInstance(listType) !;

        // Add parent items first
        foreach (var item in parentList)
        {
            mergedList.Add(item);
        }

        // Add child items (no deduplication - additive)
        foreach (var item in childList)
        {
            mergedList.Add(item);
        }

        return mergedList;
    }

    /// <summary>
    /// Merges two lists with smart key-based deduplication.
    /// Child items with the same key override parent items.
    /// For Needs: key is "Id" property
    /// For Traits: key is "TraitType" property.
    /// </summary>
    private static object MergeListsSmart(Type listType, object parentValue, object childValue, string propertyName)
    {
        var parentList = (IList)parentValue;
        var childList = (IList)childValue;

        // Determine the key property name based on list type
        string keyPropertyName = propertyName switch
        {
            "Needs" => "Id",
            "Traits" => "TraitType",
            "SpriteLayers" => "Name",
            _ => "Id" // Default fallback
        };

        // Get element type
        var elementType = listType.IsGenericType
            ? listType.GetGenericArguments()[0]
            : typeof(object);

        // Get the key property
        var keyProperty = elementType.GetProperty(keyPropertyName);
        if (keyProperty == null)
        {
            // Fall back to additive merge if no key property found
            Log.Warn($"DefinitionMerger: Could not find key property '{keyPropertyName}' for smart merge of '{propertyName}'");
            return MergeLists(listType, parentValue, childValue);
        }

        // Build dictionary of child items by key for fast lookup
        var childKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in childList)
        {
            var key = keyProperty.GetValue(item) as string;
            if (!string.IsNullOrEmpty(key))
            {
                childKeys.Add(key);
            }
        }

        // Create merged list
        var mergedList = (IList)Activator.CreateInstance(listType) !;

        // Add parent items that are NOT overridden by child
        foreach (var item in parentList)
        {
            var key = keyProperty.GetValue(item) as string;
            if (string.IsNullOrEmpty(key) || !childKeys.Contains(key))
            {
                mergedList.Add(item);
            }
        }

        // Add all child items (they override or are new)
        foreach (var item in childList)
        {
            mergedList.Add(item);
        }

        return mergedList;
    }

    /// <summary>
    /// Merges two dictionaries (parent first, child overrides same keys).
    /// </summary>
    private static object MergeDictionaries(Type dictType, object parentValue, object childValue)
    {
        var parentDict = (IDictionary)parentValue;
        var childDict = (IDictionary)childValue;

        // Create new dictionary of the same type
        var mergedDict = (IDictionary)Activator.CreateInstance(dictType) !;

        // Add parent entries first
        foreach (DictionaryEntry entry in parentDict)
        {
            mergedDict[entry.Key] = entry.Value;
        }

        // Child entries override
        foreach (DictionaryEntry entry in childDict)
        {
            mergedDict[entry.Key] = entry.Value;
        }

        return mergedDict;
    }

    /// <summary>
    /// Recursively merges two complex objects.
    /// </summary>
    private static object? MergeObjects(Type type, object parentValue, object childValue)
    {
        // Use reflection to call generic Merge<T> method
        var mergeMethod = typeof(DefinitionMerger)
            .GetMethod(nameof(Merge), BindingFlags.Public | BindingFlags.Static) !
            .MakeGenericMethod(type);

        try
        {
            return mergeMethod.Invoke(null, new[] { parentValue, childValue });
        }
        catch (Exception ex)
        {
            Log.Warn($"DefinitionMerger: Failed to merge type {type.Name}: {ex.Message}");
            return childValue; // Fallback to child on error
        }
    }
}
