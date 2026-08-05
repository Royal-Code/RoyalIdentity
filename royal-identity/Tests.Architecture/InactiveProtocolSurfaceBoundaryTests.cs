using RoyalIdentity.Options;

namespace Tests.Architecture;

/// <summary>
/// Guards the inactive protocol and logging surfaces removed by plan-refactoring-debt-closure Fase 3.
/// </summary>
public class InactiveProtocolSurfaceBoundaryTests
{
    [Fact]
    public void InactiveProtocolOptions_AreAbsentFromTheConfigurationContract()
    {
        Assert.Null(typeof(EndpointsOptions).GetProperty("Enable" + "IntrospectionEndpoint"));
        Assert.Null(typeof(EndpointsOptions).GetProperty("Enable" + "DeviceAuthorizationEndpoint"));
        Assert.Null(typeof(InputLengthRestrictions).GetProperty("Device" + "Code"));
        Assert.Null(typeof(LoggingOptions).GetProperty("UseLog" + "Service"));
    }

    [Fact]
    public void TokenEndpoint_DelegatesStandardizedNonCoreGrantsToTheExtensionProvider()
    {
        var source = ReadCoreSource("Endpoints", "TokenEndpoint.cs");

        Assert.DoesNotContain(
            "case OpenIdConnectGrantTypes." + "DeviceCode",
            source,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "case OpenIdConnectGrantTypes." + "TokenExchange",
            source,
            StringComparison.Ordinal);
        Assert.Contains("extensionsGrantsProvider.CreateContextAsync", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Discovery_DoesNotPublishAliasesForUnmappedMtlsEndpoints()
    {
        var source = ReadCoreSource("Handlers", "DiscoveryHandler.cs");

        Assert.DoesNotContain("Enable" + "IntrospectionEndpoint", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Enable" + "DeviceAuthorizationEndpoint", source, StringComparison.Ordinal);
        Assert.DoesNotContain("BuildMtls" + "IntrospectionUrl", source, StringComparison.Ordinal);
        Assert.DoesNotContain("BuildMtls" + "DeviceAuthorizationUrl", source, StringComparison.Ordinal);
        Assert.DoesNotContain("BuildMtlsTokenUrl", source, StringComparison.Ordinal);
        Assert.DoesNotContain("BuildMtlsRevocationUrl", source, StringComparison.Ordinal);
        Assert.DoesNotContain("MtlsEndpointAliases", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Logging_HasNoDeadServiceBranchAndRetainsTheMandatoryRedactionFloor()
    {
        var expected = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            Constants.Oidc.Token.Request.ClientSecret,
            Constants.Oidc.Token.Request.Password,
            Constants.Oidc.Token.Request.ClientAssertion,
            Constants.Oidc.Token.Request.RefreshToken,
            Constants.Oidc.Token.Request.DeviceCode,
            Constants.Oidc.Authorize.Request.IdTokenHint,
            Constants.Oidc.Token.Request.Code,
            Constants.Oidc.Token.Request.CodeVerifier,
        };

        Assert.Equal(expected.Count, LoggingOptions.AlwaysRedacted.Count);
        Assert.True(expected.SetEquals(LoggingOptions.AlwaysRedacted));

        var source = ReadCoreSource("Extensions", "LoggerExtensions.cs");
        Assert.DoesNotContain("UseLog" + "Service", source, StringComparison.Ordinal);
        Assert.DoesNotContain("chamar o log " + "sevice", source, StringComparison.Ordinal);
    }

    [Fact]
    public void EndpointRoutes_DoNotMapInactiveProtocolEndpoints()
    {
        var source = ReadCoreSource("Extensions", "EndpointRouteBuilderExtensions.cs");

        Assert.DoesNotContain("Introspection" + "Endpoint", source, StringComparison.Ordinal);
        Assert.DoesNotContain("DeviceAuthorization" + "Endpoint", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Oidc.Routes.MtlsToken", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Oidc.Routes.MtlsRevocation", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Oidc.Routes.MtlsIntrospection", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Oidc.Routes.MtlsDeviceAuthorization", source, StringComparison.Ordinal);
    }

    private static string ReadCoreSource(params string[] relativePath)
    {
        var path = Path.Combine(
            ProjectReferenceReader.FindRepositoryRoot(),
            "RoyalIdentity",
            Path.Combine(relativePath));

        return File.ReadAllText(path);
    }
}
