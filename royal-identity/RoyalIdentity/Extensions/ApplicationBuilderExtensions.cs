using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using RoyalIdentity.Authentication;

namespace RoyalIdentity.Extensions;

/// <summary>Configures the provider-neutral RoyalIdentity protocol pipeline.</summary>
public static class ApplicationBuilderExtensions
{
    /// <summary>
    /// <para>
    /// Applies forwarded headers, then adds realm discovery, realm CORS, authentication and authorization in
    /// their required order before mapping the OpenID Connect and OAuth 2.0 endpoints.
    /// </para>
    /// <para>
    /// The caller must invoke <c>UseRouting()</c> before this method so realm discovery can read route values.
    /// A host behind a proxy must also configure the trusted proxies or networks used by forwarded headers.
    /// It must not enable <c>ASPNETCORE_FORWARDEDHEADERS_ENABLED</c> unless an equivalent trusted-ingress
    /// boundary exists, because that compatibility setting clears the middleware's proxy/network trust lists.
    /// Because this method owns forwarded-header processing, the host must not place middleware that depends on
    /// the effective scheme, host or remote IP (such as HTTPS redirection) before this call.
    /// UI, static files, error handling and antiforgery are deliberately not installed here. A web host adds
    /// those concerns itself and installs antiforgery after this protocol pipeline and before mapping its UI.
    /// </para>
    /// </summary>
    /// <param name="app">The independently composed web application.</param>
    /// <returns>The same application for chaining.</returns>
    public static WebApplication UseRoyalIdentityProtocol(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.UseForwardedHeaders();
        app.UseRealmDiscovery();
        // Culture is negotiated from realm options, so it must run after realm discovery; everything that can
        // render text — CORS-preflighted UI, authentication challenges, components — must see it already set
        // (plan-localization DF9).
        app.UseRealmRequestLocalization();
        app.UseRealmCors();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapOpenIdConnectProviderEndpoints();

        return app;
    }
}
