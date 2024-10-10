using Microsoft.Extensions.Primitives;
using System.Collections.Specialized;
using System.Diagnostics;

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

        if (collection == null || collection.Count == 0)
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
}
