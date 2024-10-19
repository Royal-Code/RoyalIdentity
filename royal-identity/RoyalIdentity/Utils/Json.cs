using System.Text.Json;
using System.Text.Json.Serialization;

namespace RoyalIdentity.Utils;

public static class Json
{
    private static readonly JsonSerializerOptions options = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static string Serialize(object o)
    {
        return JsonSerializer.Serialize(o, options);
    }

    public static T Deserialize<T>(string value)
    {
        return JsonSerializer.Deserialize<T>(value, options)!;
    }
}
