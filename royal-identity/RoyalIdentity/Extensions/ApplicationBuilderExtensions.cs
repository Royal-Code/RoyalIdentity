using Microsoft.AspNetCore.Builder;
using RoyalIdentity.Authentication;

namespace RoyalIdentity.Extensions;

/// <summary>Configures the provider-neutral RoyalIdentity protocol pipeline.</summary>
public static class ApplicationBuilderExtensions
{
    /// <summary>
    /// <para>
    /// Adds realm discovery, realm CORS, authentication and authorization in their required order, then maps
    /// the OpenID Connect and OAuth 2.0 endpoints.
    /// </para>
    /// <para>
    /// The caller must invoke <c>UseRouting()</c> before this method so realm discovery can read route values.
    /// UI, static files, error handling and antiforgery are deliberately not installed here. A web host adds
    /// those concerns itself and installs antiforgery after this protocol pipeline and before mapping its UI.
    /// </para>
    /// </summary>
    /// <param name="app">The independently composed web application.</param>
    /// <returns>The same application for chaining.</returns>
    public static WebApplication UseRoyalIdentityProtocol(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.UseRealmDiscovery();
        app.UseRealmCors();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapOpenIdConnectProviderEndpoints();

        return app;
    }
}
