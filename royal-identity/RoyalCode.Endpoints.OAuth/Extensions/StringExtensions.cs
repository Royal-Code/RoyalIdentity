using Microsoft.AspNetCore.WebUtilities;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Text.Encodings.Web;
using System.Text;
using System.Diagnostics.CodeAnalysis;

namespace RoyalIdentity.Extensions;

internal static class StringExtensions
{
    internal static readonly char[] separator = [' '];

    [DebuggerStepThrough]
    public static string ToSpaceSeparatedString(this IEnumerable<string> list)
    {
        if (list == null)
        {
            return string.Empty;
        }

        var sb = new StringBuilder(100);

        foreach (var element in list)
        {
            sb.Append(element + " ");
        }

        return sb.ToString().Trim();
    }

    [DebuggerStepThrough]
    public static IEnumerable<string> FromSpaceSeparatedString(this string? input)
    {
        if (input is null)
            return [];

        input = input.Trim();
        return [.. input.Split(separator, StringSplitOptions.RemoveEmptyEntries)];
    }

    public static List<string>? ParseScopesString(this string? scopes)
    {
        if (scopes.IsMissing())
        {
            return null;
        }

        scopes = scopes.Trim();
        var parsedScopes = scopes.Split(separator, StringSplitOptions.RemoveEmptyEntries).Distinct().ToList();

        if (parsedScopes.Count is not 0)
        {
            parsedScopes.Sort();
            return parsedScopes;
        }

        return null;
    }

    [DebuggerStepThrough]
    public static bool IsMissing([NotNullWhen(false)] this string? value)
    {
        return string.IsNullOrWhiteSpace(value);
    }

    [DebuggerStepThrough]
    public static bool IsMissingOrTooLong([NotNullWhen(false)] this string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }
        if (value.Length > maxLength)
        {
            return true;
        }

        return false;
    }

    [DebuggerStepThrough]
    public static bool IsPresent([NotNullWhen(true)] this string? value)
    {
        return !string.IsNullOrWhiteSpace(value);
    }

    [DebuggerStepThrough]
    public static string? EnsureLeadingSlash(this string? url)
    {
        if (url is not null && !url.StartsWith('/'))
        {
            return "/" + url;
        }

        return url;
    }

    [DebuggerStepThrough]
    public static string? EnsureTrailingSlash(this string? url)
    {
        if (url is not null && !url.EndsWith('/'))
        {
            return url + "/";
        }

        return url;
    }

    [DebuggerStepThrough]
    public static string? RemoveLeadingSlash(this string? url)
    {
        if (url is not null && url.StartsWith('/'))
        {
            url = url[1..];
        }

        return url;
    }

    [DebuggerStepThrough]
    public static string? RemoveTrailingSlash(this string? url)
    {
        if (url is not null && url.EndsWith('/'))
        {
            url = url[..^1];
        }

        return url;
    }

    [DebuggerStepThrough]
    public static string? CleanUrlPath(this string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) url = "/";

        if (url is not "/" && url.EndsWith('/'))
        {
            url = url[..^1];
        }

        return url;
    }

    [DebuggerStepThrough]
    public static bool IsLocalUrl(this string? url)
    {
        if (string.IsNullOrEmpty(url))
        {
            return false;
        }

        // Allows "/" or "/foo" but not "//" or "/\".
        if (url[0] == '/')
        {
            // url is exactly "/"
            if (url.Length == 1)
            {
                return true;
            }

            // url doesn't start with "//" or "/\"
            if (url[1] is not '/' and not '\\')
            {
                return true;
            }

            return false;
        }

        // Allows "~/" or "~/foo" but not "~//" or "~/\".
        if (url[0] == '~' && url.Length > 1 && url[1] == '/')
        {
            // url is exactly "~/"
            if (url.Length == 2)
            {
                return true;
            }

            // url doesn't start with "~//" or "~/\"
            if (url[2] is not '/' and not '\\')
            {
                return true;
            }

            return false;
        }

        return false;
    }

    [DebuggerStepThrough]
    public static string AddQueryString(this string url, string query)
    {
        if (!url.Contains('?'))
        {
            url += '?';
        }
        else if (!url.EndsWith('&'))
        {
            url += '&';
        }

        return url + query;
    }

    [DebuggerStepThrough]
    public static string AddQueryString(this string url, string name, string value)
    {
        return url.AddQueryString(name + "=" + UrlEncoder.Default.Encode(value));
    }

    [DebuggerStepThrough]
    public static string AddHashFragment(this string url, string query)
    {
        if (!url.Contains('#'))
        {
            url += '#';
        }

        return url + query;
    }

    [DebuggerStepThrough]
    public static NameValueCollection ReadQueryStringAsNameValueCollection(this string? url)
    {
        if (url is not null)
        {
            var idx = url.IndexOf('?');
            if (idx >= 0)
            {
                url = url[(idx + 1)..];
            }
            var query = QueryHelpers.ParseNullableQuery(url);
            if (query is not null)
            {
                return query.AsNameValueCollection();
            }
        }

        return [];
    }

    public static string? GetOrigin(this string? url)
    {
        if (url is not null)
        {
            Uri uri;
            try
            {
                uri = new Uri(url);
            }
            catch (Exception)
            {
                return null;
            }

            if (uri.Scheme == "http" || uri.Scheme == "https")
            {
                return $"{uri.Scheme}://{uri.Authority}";
            }
        }

        return null;
    }

    public static string Obfuscate(this string value)
    {
        var last4Chars = "****";
        if (value.IsPresent() && value.Length > 4)
        {
            last4Chars = value[^4..];
        }

        return "****" + last4Chars;
    }
}
