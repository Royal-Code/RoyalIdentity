using RoyalIdentity.Security;

namespace Tests.Security;

/// <summary>
/// Phase 1 smoke test: proves <c>Tests.Security</c> is wired to the shared technical library and that the
/// library loads under <c>net10.0</c>. Real component tests (CryptoRandom, Base64Url, hashing, …) land from
/// Phase 2 onward of <c>plan-royalidentity-security</c>.
/// </summary>
public class SecurityLibrarySmokeTests
{
    [Fact]
    public void Marker_Resolves_To_Security_Assembly()
    {
        var assemblyName = typeof(SecurityAssemblyMarker).Assembly.GetName().Name;

        Assert.Equal("RoyalIdentity.Security", assemblyName);
    }
}
