using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace RoyalIdentity.Extensions;

public static class WildcardExtensions
{
    private static readonly WildcardDefinition[] wildcards =
    [
        new WildcardDefinition("://*.", "://wildcard.", "://(?:[a-zA-Z0-9-]+\\.)+"),
        new WildcardDefinition("/**", "/wildcard/wildcard", "/.*"),
        new WildcardDefinition("/*", "/wildcard/", "/[a-zA-Z0-9-]*"),
        new WildcardDefinition(":*", ":5000", ":[0-9]+"),
        new WildcardDefinition("*:/", "scheme:", "(?:[a-zA-Z][a-zA-Z0-9+.-]*):/"),
    ];

    private static readonly ConcurrentDictionary<string, string> regexPatternsCache = new();
    private static readonly Func<string, string> createPattern = CreatePattern;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool HasWildcard(this string value)
    {
        for (var i = 0; i < wildcards.Length; i++)
        {
            if (value.Contains(wildcards[i].Key))
                return true;
        }

        return false;
    }

    public static string ReplaceWildcard(this string value)
    {
        foreach (var kvp in wildcards)
        {
            value = value.Replace(kvp.Key, kvp.Replacement);
        }

        return value;
    }

    public static bool MatchWildcard(this string value, string toMatch, StringComparison comparison)
    {
        var pattern = regexPatternsCache.GetOrAdd(value, createPattern);
        var options = comparison switch
        {
            StringComparison.Ordinal => RegexOptions.CultureInvariant,
            StringComparison.OrdinalIgnoreCase => RegexOptions.CultureInvariant | RegexOptions.IgnoreCase,
            _ => throw new ArgumentOutOfRangeException(nameof(comparison), comparison, null),
        };
        var match = Regex.Match(toMatch, pattern, options);

        return match.Success;
    }

    public static string CreatePattern(string value)
    {
        var pattern = Regex.Escape(value);

        foreach (var wc in wildcards)
        {
            pattern = pattern.Replace(
                Regex.Escape(wc.Key),
                wc.RegexPattern,
                StringComparison.Ordinal);
        }

        return $"^{pattern}$";
    }

    private sealed class WildcardDefinition
    {
        public WildcardDefinition(string key, string replacement, string regexPattern)
        {
            Key = key;
            Replacement = replacement;
            RegexPattern = regexPattern;
        }

        public string Key { get; }

        public string Replacement { get; }

        public string RegexPattern { get; }
    }
}

