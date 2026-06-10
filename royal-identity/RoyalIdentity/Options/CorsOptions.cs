namespace RoyalIdentity.Options;

/// <summary>
/// Cross-origin request policy for realm-aware protocol endpoints.
/// </summary>
public class CorsOptions
{
    public CorsOptions()
    {
    }

    public CorsOptions(CorsOptions other)
    {
        Enabled = other.Enabled;
        AllowCredentials = other.AllowCredentials;

        foreach (var origin in other.AllowedOrigins)
        {
            AllowedOrigins.Add(origin);
        }

        foreach (var header in other.AllowedHeaders)
        {
            AllowedHeaders.Add(header);
        }

        foreach (var method in other.AllowedMethods)
        {
            AllowedMethods.Add(method);
        }
    }

    /// <summary>
    /// Enables CORS processing for the realm.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Origins allowed for the whole realm. Use exact scheme, host and port.
    /// </summary>
    public HashSet<string> AllowedOrigins { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Request headers accepted in preflight requests.
    /// </summary>
    public HashSet<string> AllowedHeaders { get; } = new(StringComparer.OrdinalIgnoreCase)
    {
        "authorization",
        "content-type"
    };

    /// <summary>
    /// Request methods accepted in preflight requests.
    /// </summary>
    public HashSet<string> AllowedMethods { get; } = new(StringComparer.OrdinalIgnoreCase)
    {
        "GET",
        "POST"
    };

    /// <summary>
    /// Emits Access-Control-Allow-Credentials when an origin is allowed.
    /// </summary>
    public bool AllowCredentials { get; set; }
}
