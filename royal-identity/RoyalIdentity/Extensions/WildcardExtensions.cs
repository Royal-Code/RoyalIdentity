using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace RoyalIdentity.Extensions;

public static class WildcardExtensions
{
    private static readonly WildcardDefinition[] wildcards =
    [
        new WildcardDefinition("://*.", "://wildcard.", DomainRegexPattern),
        new WildcardDefinition("/**", "/wildcard/wildcard", AnyRegexPattern),
        new WildcardDefinition("/*", "/wildcard/", PathRegexPattern),
        new WildcardDefinition(":*", ":5000", PortRegexPattern),
        new WildcardDefinition("*:/", "scheme:", SchemeRegexPattern),
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

    public static bool MatchWildcard(this string value, string toMatch)
    {
        var pattern = regexPatternsCache.GetOrAdd(value, createPattern);
        var match = Regex.Match(toMatch, pattern);

        return match.Success;
    }

    public static string CreatePattern(string value)
    {
        var pattern = value.PrepareRegexPattern();

        foreach (var wc in wildcards)
        {
            var rwc = wc.RegexWildcard;
            if (pattern.Contains(rwc))
            {
                pattern = string.Join(wc.RegexPattern(rwc), pattern.Split(rwc));
            }
        }

        return $"^{pattern}$";
    }

    private static string PrepareRegexPattern(this string value)
    {
        var result = value.Replace("/", "\\/").Replace("?", "\\?").Replace(".", "\\.");
        return result;
    }

    private static string AnyRegexPattern(this string wildcard)
    {
        return $"({wildcard.Replace("**", ".*")})";
    }

    private static string DomainRegexPattern(this string wildcard)
    {
        return $"({wildcard.Replace("*", "[a-zA-Z0-9\\-\\.]*")})";
    }

    private static string PortRegexPattern(this string wildcard)
    {
        return $"({wildcard.Replace("*", "[0-9]*")})";
    }

    private static string PathRegexPattern(this string wildcard)
    {
        return $"({wildcard.Replace("*", "[a-zA-Z0-9\\-]*")})";
    }

    private static string SchemeRegexPattern(this string wildcard)
    {
        return $"({wildcard.Replace("*", "[a-zA-Z0-9\\-\\.]*")})";
    }

    private sealed class WildcardDefinition
    {
        public WildcardDefinition(string key, string replacement, Func<string, string> apply)
        {
            Key = key;
            Replacement = replacement;
            RegexPattern = apply;
            RegexWildcard = key.PrepareRegexPattern();
        }

        public string Key { get; }

        public string Replacement { get; }

        public string RegexWildcard { get; }

        public Func<string, string> RegexPattern { get; }
    }
}

