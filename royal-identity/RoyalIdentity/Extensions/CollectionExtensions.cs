using Microsoft.Extensions.Primitives;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Encodings.Web;

namespace RoyalIdentity.Extensions;

internal static class CollectionExtensions
{
    [DebuggerStepThrough]
    public static NameValueCollection AsNameValueCollection(this IEnumerable<KeyValuePair<string, StringValues>> collection)
    {
        var nv = new NameValueCollection();

        foreach (var field in collection)
        {
            nv.Add(field.Key, field.Value[0]);
        }

        return nv;
    }

    [DebuggerStepThrough]
    public static NameValueCollection AsNameValueCollection(this IDictionary<string, StringValues> collection)
    {
        var nv = new NameValueCollection();

        foreach (var field in collection)
        {
            nv.Add(field.Key, field.Value[0]);
        }

        return nv;
    }

    public static Dictionary<string, string> ToScrubbedDictionary(
        this NameValueCollection collection,
        ICollection<string> nameFilter)
    {
        var dict = new Dictionary<string, string>();

        if (collection.Count is 0)
        {
            return dict;
        }

        foreach (string name in collection)
        {
            var value = collection.Get(name);
            if (value != null)
            {
                if (nameFilter.Contains(name, StringComparer.OrdinalIgnoreCase))
                {
                    value = "***REDACTED***";
                }
                dict.Add(name, value);
            }
        }

        return dict;
    }

    public static string ToQueryString(this NameValueCollection collection)
    {
        if (collection.Count == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder(128);
        var first = true;
        foreach (string? name in collection)
        {
            string?[]? values = collection.GetValues(name);
            if (values == null || values.Length == 0)
            {
                first = AppendNameValuePair(builder, first, true, name, string.Empty);
            }
            else
            {
                foreach (var value in values)
                {
                    first = AppendNameValuePair(builder, first, true, name, value);
                }
            }
        }

        return builder.ToString();
    }

    private static bool AppendNameValuePair(StringBuilder builder, bool first, bool urlEncode, string? name, string? value)
    {
        var effectiveName = name ?? string.Empty;
        var encodedName = urlEncode ? UrlEncoder.Default.Encode(effectiveName) : effectiveName;

        var effectiveValue = value ?? string.Empty;
        var encodedValue = urlEncode ? UrlEncoder.Default.Encode(effectiveValue) : effectiveValue;
        encodedValue = ConvertFormUrlEncodedSpacesToUrlEncodedSpaces(encodedValue);

        if (first)
        {
            first = false;
        }
        else
        {
            builder.Append('&');
        }

        builder.Append(encodedName);
        if (encodedValue.IsPresent())
        {
            builder.Append('=');
            builder.Append(encodedValue);
        }
        return first;
    }

    private static string? ConvertFormUrlEncodedSpacesToUrlEncodedSpaces(string? str)
    {
        if (str != null && str.IndexOf('+') >= 0)
        {
            str = str.Replace("+", "%20");
        }

        return str;
    }

    public static string ToFormPost(this NameValueCollection collection)
    {
        var builder = new StringBuilder(128);
        const string inputFieldFormat = "<input type='hidden' name='{0}' value='{1}' />\n";

        foreach (string name in collection)
        {
            var values = collection.GetValues(name);
            if (values is null || values.Length is 0)
                continue;

            var value = values[0];
            value = HtmlEncoder.Default.Encode(value);
            builder.AppendFormat(inputFieldFormat, name, value);
        }

        return builder.ToString();
    }

    public static void AddRange<T>(this HashSet<T> set, IEnumerable<T> values)
    {
        foreach (var value in values)
            set.Add(value);
    }

    public static void AddRange<T>(this ICollection<T> set, IEnumerable<T> values)
    {
        foreach (var value in values)
            set.Add(value);
    }

    internal static IEnumerable<T> IntersectMany<T>(this IEnumerable<IEnumerable<T>> lists)
    {
        return lists.Aggregate((l1, l2) => l1.Intersect(l2));
    }

    public static bool TryGet(this NameValueCollection values, string key,[NotNullWhen(true)] out string? value)
    {
        value = values[key];
        return value is not null;
    }

    public static bool IsNullOrEmpty<T>(this T[]? values)
    {
        return values is null || values.Length == 0;
    }

    public static bool IsNullOrEmpty<TKey, TValue>(this IDictionary<TKey, TValue>? dictionary)
    {
        return dictionary is null || dictionary.Count == 0;
    }

    public static bool IsNullOrEmpty<T>(this ICollection<T>? collection)
    {
        return collection is null || collection.Count == 0;
    }

    public static bool IsNullOrEmpty<T>(this IEnumerable<T>? collection)
    {
        return collection is null || !collection.Any();
    }
}