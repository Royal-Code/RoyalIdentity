using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using RoyalIdentity.Contexts;
using RoyalIdentity.Contracts;
using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Extensions;
using RoyalIdentity.Options;
using RoyalIdentity.Pipelines.Abstractions;
using RoyalIdentity.Responses;

namespace RoyalIdentity.Handlers;

public class DiscoveryHandler : IHandler<DiscoveryContext>
{
    private readonly IKeyManager keys;
    private readonly IStorage storage;
    private readonly IClientSecretChecker clientSecretChecker;
    private readonly IExtensionsGrantsProvider extensionGrantsProvider;
    private readonly ILogger logger;

    public DiscoveryHandler(
        IKeyManager keys,
        IStorage storage,
        IClientSecretChecker clientSecretChecker,
        IExtensionsGrantsProvider extensionGrantsProvider,
        ILogger<DiscoveryHandler> logger)
    {
        this.keys = keys;
        this.storage = storage;
        this.clientSecretChecker = clientSecretChecker;
        this.extensionGrantsProvider = extensionGrantsProvider;
        this.logger = logger;
    }

    public async Task Handle(DiscoveryContext context, CancellationToken ct)
    {
        logger.LogDebug("Handle discovery context start");

        var issuerUri = context.IssuerUri;
        var baseUrl = context.BaseUrl;
        var options = context.Options;
        var realmPath = context.Realm.Path;


        var entries = new Dictionary<string, object>
        {
            { Oidc.Discovery.Issuer, issuerUri }
        };

        // jwks
        if (options.Discovery.ShowKeySet && (await keys.GetValidationKeysAsync(context.Realm, ct)).Keys.Count is not 0)
        {
            entries.Add(Oidc.Discovery.JwksUri, baseUrl + Oidc.Routes.BuildDiscoveryWebKeysUrl(realmPath));
        }

        // endpoints
        if (options.Discovery.ShowEndpoints)
        {
            if (options.Endpoints.EnableAuthorizeEndpoint)
            {
                entries.Add(Oidc.Discovery.AuthorizationEndpoint, baseUrl + Oidc.Routes.BuildAuthorizeUrl(realmPath));
            }

            if (options.Endpoints.EnableTokenEndpoint)
            {
                entries.Add(Oidc.Discovery.TokenEndpoint, baseUrl + Oidc.Routes.BuildTokenUrl(realmPath));
            }

            if (options.Endpoints.EnableUserInfoEndpoint)
            {
                entries.Add(Oidc.Discovery.UserInfoEndpoint, baseUrl + Oidc.Routes.BuildUserInfoUrl(realmPath));
            }

            if (options.Endpoints.EnableEndSessionEndpoint)
            {
                entries.Add(Oidc.Discovery.EndSessionEndpoint, baseUrl + Oidc.Routes.BuildEndSessionUrl(realmPath));
            }

            if (options.Endpoints.EnableCheckSessionEndpoint)
            {
                entries.Add(Oidc.Discovery.CheckSessionIframe, baseUrl + Oidc.Routes.BuildCheckSessionUrl(realmPath));
            }

            if (options.Endpoints.EnableTokenRevocationEndpoint)
            {
                entries.Add(Oidc.Discovery.RevocationEndpoint, baseUrl + Oidc.Routes.BuildRevocationUrl(realmPath));
            }

            if (options.Endpoints.EnableIntrospectionEndpoint)
            {
                entries.Add(Oidc.Discovery.IntrospectionEndpoint, baseUrl + Oidc.Routes.BuildIntrospectionUrl(realmPath));
            }

            if (options.Endpoints.EnableDeviceAuthorizationEndpoint)
            {
                entries.Add(Oidc.Discovery.DeviceAuthorizationEndpoint, baseUrl + Oidc.Routes.BuildDeviceAuthorizationUrl(realmPath));
            }

            if (options.MutualTls.Enabled)
            {
                var mtlsEndpoints = new Dictionary<string, string>();

                if (options.Endpoints.EnableTokenEndpoint)
                {
                    mtlsEndpoints.Add(Oidc.Discovery.TokenEndpoint, ConstructMtlsEndpoint(Oidc.Routes.BuildMtlsTokenUrl(realmPath)));
                }
                if (options.Endpoints.EnableTokenRevocationEndpoint)
                {
                    mtlsEndpoints.Add(Oidc.Discovery.RevocationEndpoint, ConstructMtlsEndpoint(Oidc.Routes.BuildMtlsTokenUrl(realmPath)));
                }
                if (options.Endpoints.EnableIntrospectionEndpoint)
                {
                    mtlsEndpoints.Add(Oidc.Discovery.IntrospectionEndpoint, ConstructMtlsEndpoint(Oidc.Routes.BuildMtlsTokenUrl(realmPath)));
                }
                if (options.Endpoints.EnableDeviceAuthorizationEndpoint)
                {
                    mtlsEndpoints.Add(Oidc.Discovery.DeviceAuthorizationEndpoint, ConstructMtlsEndpoint(Oidc.Routes.BuildMtlsTokenUrl(realmPath)));
                }

                if (mtlsEndpoints.Count is not 0)
                {
                    entries.Add(Oidc.Discovery.MtlsEndpointAliases, mtlsEndpoints);
                }

                string ConstructMtlsEndpoint(string endpoint)
                {
                    // path based
                    if (options.MutualTls.DomainName.IsMissing())
                    {
                        return baseUrl + endpoint;
                    }

                    // domain based
                    if (options.MutualTls.DomainName.Contains('.'))
                    {
                        return $"https://{options.MutualTls.DomainName}/{endpoint}";
                    }
                    // sub-domain based
                    else
                    {
                        var parts = baseUrl.Split("://");
                        return $"https://{options.MutualTls.DomainName}.{parts[1]}{endpoint}";
                    }
                }
            }
        }

        // logout
        if (options.Endpoints.EnableEndSessionEndpoint)
        {
            entries.Add(Oidc.Discovery.FrontChannelLogoutSupported, true);
            entries.Add(Oidc.Discovery.FrontChannelLogoutSessionSupported, true);
            entries.Add(Oidc.Discovery.BackChannelLogoutSupported, true);
            entries.Add(Oidc.Discovery.BackChannelLogoutSessionSupported, true);
        }

        // scopes and claims
        if (options.Discovery.ShowIdentityScopes ||
            options.Discovery.ShowScopes ||
            options.Discovery.ShowClaims)
        {
            var resources = await storage.GetResourceStore(context.Realm).GetAllEnabledResourcesAsync(ct);
            var scopes = new List<string>();

            // scopes
            if (options.Discovery.ShowIdentityScopes)
            {
                scopes.AddRange(resources.IdentityScopes.Where(x => x.ShowInDiscoveryDocument).Select(x => x.Name));
            }

            if (options.Discovery.ShowScopes)
            {
                // Only scopes that can actually be requested via the scope parameter
                // (resource servers with AllowScopeRequests = false are reachable only via resource indicators).
                var scopesSupported = resources.ResourceServers
                    .Where(rs => rs.AllowScopeRequests)
                    .SelectMany(rs => rs.Scopes)
                    .Where(scope => scope.ShowInDiscoveryDocument)
                    .Select(scope => scope.Name);

                scopes.AddRange(scopesSupported);
                scopes.Add(Server.StandardScopes.OfflineAccess);
            }

            if (scopes.Count is not 0)
            {
                entries.Add(Oidc.Discovery.ScopesSupported, scopes.ToArray());
            }

            // claims
            if (options.Discovery.ShowClaims)
            {
                var claims = new List<string>();

                // add non-hidden identity scopes related claims
                claims.AddRange(resources.IdentityScopes.Where(x => x.ShowInDiscoveryDocument).SelectMany(x => x.UserClaims));
                
                entries.Add(Oidc.Discovery.ClaimsSupported, claims.Distinct().ToArray());
            }
        }

        // grant types
        if (options.Discovery.ShowGrantTypes)
        {
            List<string> standardGrantTypes =
            [
                OpenIdConnectGrantTypes.AuthorizationCode,
                OpenIdConnectGrantTypes.ClientCredentials,
                OpenIdConnectGrantTypes.RefreshToken
            ];

            if (options.Endpoints.EnableDeviceAuthorizationEndpoint)
            {
                standardGrantTypes.Add(OpenIdConnectGrantTypes.DeviceCode);
            }

            var showGrantTypes = new List<string>(standardGrantTypes);

            if (options.Discovery.ShowExtensionGrantTypes)
            {
                showGrantTypes.AddRange(extensionGrantsProvider.GetAvailableGrantTypes());
            }

            entries.Add(Oidc.Discovery.GrantTypesSupported, showGrantTypes.ToArray());
        }

        // response types
        if (options.Discovery.ShowResponseTypes)
        {
            entries.Add(Oidc.Discovery.ResponseTypesSupported, options.Discovery.SupportedResponseTypes.ToArray());
        }

        // response modes
        if (options.Discovery.ShowResponseModes)
        {
            entries.Add(Oidc.Discovery.ResponseModesSupported, options.Discovery.SupportedResponseModes.ToArray());
        }

        // misc
        if (options.Discovery.ShowTokenEndpointAuthenticationMethods)
        {
            var types = clientSecretChecker.GetAvailableAuthenticationMethods().ToList();
            if (options.MutualTls.Enabled)
            {
                types.Add(Oidc.Endpoint.AuthMethods.TlsClientAuth);
                types.Add(Oidc.Endpoint.AuthMethods.SelfSignedTlsClientAuth);
            }

            entries.Add(Oidc.Discovery.TokenEndpointAuthenticationMethodsSupported, types);
        }

        var signingCredentials = await keys.GetAllSigningCredentialsAsync(context.Realm, ct);
        if (signingCredentials.Count is not 0)
        {
            var signingAlgorithms = signingCredentials.Select(c => c.Algorithm).Distinct();
            entries.Add(Oidc.Discovery.IdTokenSigningAlgorithmsSupported, signingAlgorithms);
        }

        entries.Add(Oidc.Discovery.SubjectTypesSupported, options.Discovery.SupportedSubjectTypes.ToArray());
        entries.Add(Oidc.Discovery.CodeChallengeMethodsSupported, options.Discovery.CodeChallengeMethodsSupported.ToArray());

        if (options.Endpoints.EnableAuthorizeEndpoint)
        {
            entries.Add(Oidc.Discovery.RequestParameterSupported, true);

            if (options.Endpoints.EnableJwtRequestUri)
            {
                entries.Add(Oidc.Discovery.RequestUriParameterSupported, true);
            }
        }

        if (options.MutualTls.Enabled)
        {
            entries.Add(Oidc.Discovery.TlsClientCertificateBoundAccessTokens, true);
        }

        // custom entries
        if (!options.Discovery.CustomEntries.IsNullOrEmpty())
        {
            foreach ((string key, object value) in options.Discovery.CustomEntries)
            {
                if (entries.ContainsKey(key))
                {
                    logger.LogError("Discovery custom entry {Key} cannot be added, because it already exists.", key);
                }
                else
                {
                    if (value is string customValueString
                        && customValueString.StartsWith("~/")
                        && options.Discovery.ExpandRelativePathsInCustomEntries)
                    {
                        entries.Add(key, baseUrl + customValueString.Substring(2));
                        continue;
                    }

                    entries.Add(key, value);
                }
            }
        }

        context.Response = new DiscoveryResponse(entries);
    }
}
