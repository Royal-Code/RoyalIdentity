namespace Tests.Browser;

/// <summary>
/// Public hostnames used by the opt-in browser acceptance. Chromium resolves each name directly to
/// loopback, but their distinct registrable domains keep the OP and RPs genuinely cross-site.
/// </summary>
internal static class BrowserTopology
{
    public const string OpHost = "op.royalidentity.test";
    public const string PrimaryRpHost = "rp.royalidentity.example";
    public const string AlternateRpHost = "rp.royalidentity.invalid";

    public static string HostResolverRules
        => $"MAP {OpHost} 127.0.0.1, MAP {PrimaryRpHost} 127.0.0.1, "
            + $"MAP {AlternateRpHost} 127.0.0.1";

    public static Uri ToPublicOrigin(string listenerAddress, string publicHost)
    {
        var origin = new UriBuilder(listenerAddress)
        {
            Host = publicHost,
        };
        return origin.Uri;
    }
}
