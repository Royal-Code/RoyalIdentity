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
            { Discovery.Issuer, issuerUri }
        };

        // jwks
        if (options.Discovery.ShowKeySet && (await keys.GetValidationKeysAsync(context.Realm, ct)).Keys.Count is not 0)
        {
            entries.Add(Discovery.JwksUri, baseUrl + Oidc.Routes.BuildDiscoveryWebKeysUrl(realmPath));
        }

        // endpoints
        if (options.Discovery.ShowEndpoints)
        {
            if (options.Endpoints.EnableAuthorizeEndpoint)
            {
                entries.Add(Discovery.AuthorizationEndpoint, baseUrl + Oidc.Routes.BuildAuthorizeUrl(realmPath));
            }

            if (options.Endpoints.EnableTokenEndpoint)
            {
                entries.Add(Discovery.TokenEndpoint, baseUrl + Oidc.Routes.BuildTokenUrl(realmPath));
            }

            if (options.Endpoints.EnableUserInfoEndpoint)
            {
                entries.Add(Discovery.UserInfoEndpoint, baseUrl + Oidc.Routes.BuildUserInfoUrl(realmPath));
            }

            if (options.Endpoints.EnableEndSessionEndpoint)
            {
                entries.Add(Discovery.EndSessionEndpoint, baseUrl + Oidc.Routes.BuildEndSessionUrl(realmPath));
            }

            if (options.Endpoints.EnableCheckSessionEndpoint)
            {
                entries.Add(Discovery.CheckSessionIframe, baseUrl + Oidc.Routes.BuildCheckSessionUrl(realmPath));
            }

            if (options.Endpoints.EnableTokenRevocationEndpoint)
            {
                entries.Add(Discovery.RevocationEndpoint, baseUrl + Oidc.Routes.BuildRevocationUrl(realmPath));
            }

            if (options.Endpoints.EnableIntrospectionEndpoint)
            {
                entries.Add(Discovery.IntrospectionEndpoint, baseUrl + Oidc.Routes.BuildIntrospectionUrl(realmPath));
            }

            if (options.Endpoints.EnableDeviceAuthorizationEndpoint)
            {
                entries.Add(Discovery.DeviceAuthorizationEndpoint, baseUrl + Oidc.Routes.BuildDeviceAuthorizationUrl(realmPath));
            }

            if (options.MutualTls.Enabled)
            {
                var mtlsEndpoints = new Dictionary<string, string>();

                if (options.Endpoints.EnableTokenEndpoint)
                {
                    mtlsEndpoints.Add(Discovery.TokenEndpoint, ConstructMtlsEndpoint(Oidc.Routes.BuildMtlsTokenUrl(realmPath)));
                }
                if (options.Endpoints.EnableTokenRevocationEndpoint)
                {
                    mtlsEndpoints.Add(Discovery.RevocationEndpoint, ConstructMtlsEndpoint(Oidc.Routes.BuildMtlsTokenUrl(realmPath)));
                }
                if (options.Endpoints.EnableIntrospectionEndpoint)
                {
                    mtlsEndpoints.Add(Discovery.IntrospectionEndpoint, ConstructMtlsEndpoint(Oidc.Routes.BuildMtlsTokenUrl(realmPath)));
                }
                if (options.Endpoints.EnableDeviceAuthorizationEndpoint)
                {
                    mtlsEndpoints.Add(Discovery.DeviceAuthorizationEndpoint, ConstructMtlsEndpoint(Oidc.Routes.BuildMtlsTokenUrl(realmPath)));
                }

                if (mtlsEndpoints.Count is not 0)
                {
                    entries.Add(Discovery.MtlsEndpointAliases, mtlsEndpoints);
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
            entries.Add(Discovery.FrontChannelLogoutSupported, true);
            entries.Add(Discovery.FrontChannelLogoutSessionSupported, true);
            entries.Add(Discovery.BackChannelLogoutSupported, true);
            entries.Add(Discovery.BackChannelLogoutSessionSupported, true);
        }

        // scopes and claims
        if (options.Discovery.ShowIdentityScopes ||
            options.Discovery.ShowApiScopes ||
            options.Discovery.ShowClaims)
        {
            var resources = await storage.GetResourceStore(context.Realm).GetAllEnabledResourcesAsync(ct);
            var scopes = new List<string>();

            // scopes
            if (options.Discovery.ShowIdentityScopes)
            {
                scopes.AddRange(resources.IdentityResources.Where(x => x.ShowInDiscoveryDocument).Select(x => x.Name));
            }

            if (options.Discovery.ShowApiScopes)
            {
                var apiScopes = from scope in resources.ApiScopes
                                where scope.ShowInDiscoveryDocument
                                select scope.Name;

                scopes.AddRange(apiScopes);
                scopes.Add(ServerConstants.StandardScopes.OfflineAccess);
            }

            if (scopes.Count is not 0)
            {
                entries.Add(Discovery.ScopesSupported, scopes.ToArray());
            }

            // claims
            if (options.Discovery.ShowClaims)
            {
                var claims = new List<string>();

                // add non-hidden identity scopes related claims
                claims.AddRange(resources.IdentityResources.Where(x => x.ShowInDiscoveryDocument).SelectMany(x => x.UserClaims));
                claims.AddRange(resources.ApiResources.Where(x => x.ShowInDiscoveryDocument).SelectMany(x => x.UserClaims));
                claims.AddRange(resources.ApiScopes.Where(x => x.ShowInDiscoveryDocument).SelectMany(x => x.UserClaims));

                entries.Add(Discovery.ClaimsSupported, claims.Distinct().ToArray());
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

            entries.Add(Discovery.GrantTypesSupported, showGrantTypes.ToArray());
        }

        // response types
        if (options.Discovery.ShowResponseTypes)
        {
            entries.Add(Discovery.ResponseTypesSupported, options.Discovery.SupportedResponseTypes.ToArray());
        }

        // response modes
        if (options.Discovery.ShowResponseModes)
        {
            entries.Add(Discovery.ResponseModesSupported, options.Discovery.SupportedResponseModes.ToArray());
        }

        // misc
        if (options.Discovery.ShowTokenEndpointAuthenticationMethods)
        {
            var types = clientSecretChecker.GetAvailableAuthenticationMethods().ToList();
            if (options.MutualTls.Enabled)
            {
                types.Add(EndpointAuthenticationMethods.TlsClientAuth);
                types.Add(EndpointAuthenticationMethods.SelfSignedTlsClientAuth);
            }

            entries.Add(Discovery.TokenEndpointAuthenticationMethodsSupported, types);
        }

        var signingCredentials = await keys.GetAllSigningCredentialsAsync(context.Realm, ct);
        if (signingCredentials.Count is not 0)
        {
            var signingAlgorithms = signingCredentials.Select(c => c.Algorithm).Distinct();
            entries.Add(Discovery.IdTokenSigningAlgorithmsSupported, signingAlgorithms);
        }

        entries.Add(Discovery.SubjectTypesSupported, options.Discovery.SupportedSubjectTypes.ToArray());
        entries.Add(Discovery.CodeChallengeMethodsSupported, options.Discovery.CodeChallengeMethodsSupported.ToArray());

        if (options.Endpoints.EnableAuthorizeEndpoint)
        {
            entries.Add(Discovery.RequestParameterSupported, true);

            if (options.Endpoints.EnableJwtRequestUri)
            {
                entries.Add(Discovery.RequestUriParameterSupported, true);
            }
        }

        if (options.MutualTls.Enabled)
        {
            entries.Add(Discovery.TlsClientCertificateBoundAccessTokens, true);
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