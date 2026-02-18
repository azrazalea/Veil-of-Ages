using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using Godot;

namespace VeilOfAges.Core.Lib;

public static class L
{
    private static readonly ConcurrentDictionary<string, string> _cache = new ();
    private static readonly ConcurrentDictionary<string, CompositeFormat> _fmtCache = new ();

    public static string Tr(string key)
    {
        return _cache.GetOrAdd(key, static k =>
        {
            using var snKey = new StringName(k);
            return TranslationServer.Translate(snKey).ToString();
        });
    }

    public static string Tr(string key, StringName context)
    {
        string cacheKey = $"{key}\0{context}";
        return _cache.GetOrAdd(cacheKey, _ =>
        {
            using var snKey = new StringName(key);
            return TranslationServer.Translate(snKey, context).ToString();
        });
    }

    public static string TrN(string singularKey, string pluralKey, int n)
    {
        string cacheKey = $"{singularKey}\0{pluralKey}\0{n}";
        return _cache.GetOrAdd(cacheKey, _ =>
        {
            using var snSingular = new StringName(singularKey);
            using var snPlural = new StringName(pluralKey);
            return TranslationServer.TranslatePlural(snSingular, snPlural, n).ToString();
        });
    }

    public static string Fmt(string translatedFormat, params object[] args)
    {
        var cf = _fmtCache.GetOrAdd(translatedFormat, static fmt => CompositeFormat.Parse(fmt));
        return string.Format(CultureInfo.InvariantCulture, cf, args);
    }

    public static string TrFmt(string key, params object[] args) => Fmt(Tr(key), args);

    public static void ClearCache()
    {
        _cache.Clear();
        _fmtCache.Clear();
    }
}
