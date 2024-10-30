using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using RoyalIdentity.Contexts;
using RoyalIdentity.Extensions;
using RoyalIdentity.Options;
using RoyalIdentity.Pipelines.Abstractions;

namespace RoyalIdentity.Handlers;

public class DiscoveryHandler: IHandler<DiscoveryContext>
{
    private readonly ILogger logger;

    public async Task Handle(DiscoveryContext context, CancellationToken ct)
    {
        var issuerUri = context.IssuerUri;
        var baseUrl = context.BaseUrl;
        var options = context.Options;

        var entries = new Dictionary<string, object>
            {
                { OidcConstants.Discovery.Issuer, issuerUri }
            };

            // jwks
            if (options.Discovery.ShowKeySet && (await Keys.GetValidationKeysAsync()).Any())
            {
                entries.Add(OidcConstants.Discovery.JwksUri, baseUrl + Constants.ProtocolRoutePaths.DiscoveryWebKeys);
            }

            // endpoints
            if (options.Discovery.ShowEndpoints)
            {
                if (options.Endpoints.EnableAuthorizeEndpoint)
                {
                    entries.Add(OidcConstants.Discovery.AuthorizationEndpoint, baseUrl + Constants.ProtocolRoutePaths.Authorize);
                }

                if (options.Endpoints.EnableTokenEndpoint)
                {
                    entries.Add(OidcConstants.Discovery.TokenEndpoint, baseUrl + Constants.ProtocolRoutePaths.Token);
                }

                if (options.Endpoints.EnableUserInfoEndpoint)
                {
                    entries.Add(OidcConstants.Discovery.UserInfoEndpoint, baseUrl + Constants.ProtocolRoutePaths.UserInfo);
                }

                if (options.Endpoints.EnableEndSessionEndpoint)
                {
                    entries.Add(OidcConstants.Discovery.EndSessionEndpoint, baseUrl + Constants.ProtocolRoutePaths.EndSession);
                }

                if (options.Endpoints.EnableCheckSessionEndpoint)
                {
                    entries.Add(OidcConstants.Discovery.CheckSessionIframe, baseUrl + Constants.ProtocolRoutePaths.CheckSession);
                }

                if (options.Endpoints.EnableTokenRevocationEndpoint)
                {
                    entries.Add(OidcConstants.Discovery.RevocationEndpoint, baseUrl + Constants.ProtocolRoutePaths.Revocation);
                }

                if (options.Endpoints.EnableIntrospectionEndpoint)
                {
                    entries.Add(OidcConstants.Discovery.IntrospectionEndpoint, baseUrl + Constants.ProtocolRoutePaths.Introspection);
                }

                if (options.Endpoints.EnableDeviceAuthorizationEndpoint)
                {
                    entries.Add(OidcConstants.Discovery.DeviceAuthorizationEndpoint, baseUrl + Constants.ProtocolRoutePaths.DeviceAuthorization);
                }

                if (options.MutualTls.Enabled)
                {
                    var mtlsEndpoints = new Dictionary<string, string>();

                    if (options.Endpoints.EnableTokenEndpoint)
                    {
                        mtlsEndpoints.Add(OidcConstants.Discovery.TokenEndpoint, ConstructMtlsEndpoint(Constants.ProtocolRoutePaths.Token));
                    }
                    if (options.Endpoints.EnableTokenRevocationEndpoint)
                    {
                        mtlsEndpoints.Add(OidcConstants.Discovery.RevocationEndpoint, ConstructMtlsEndpoint(Constants.ProtocolRoutePaths.Revocation));
                    }
                    if (options.Endpoints.EnableIntrospectionEndpoint)
                    {
                        mtlsEndpoints.Add(OidcConstants.Discovery.IntrospectionEndpoint, ConstructMtlsEndpoint(Constants.ProtocolRoutePaths.Introspection));
                    }
                    if (options.Endpoints.EnableDeviceAuthorizationEndpoint)
                    {
                        mtlsEndpoints.Add(OidcConstants.Discovery.DeviceAuthorizationEndpoint, ConstructMtlsEndpoint(Constants.ProtocolRoutePaths.DeviceAuthorization));
                    }

                    if (mtlsEndpoints.Any())
                    {
                        entries.Add(OidcConstants.Discovery.MtlsEndpointAliases, mtlsEndpoints);
                    }

                    string ConstructMtlsEndpoint(string endpoint)
                    {
                        // path based
                        if (options.MutualTls.DomainName.IsMissing())
                        {
                            return baseUrl + endpoint.Replace(Constants.ProtocolRoutePaths.ConnectPathPrefix, Constants.ProtocolRoutePaths.MtlsPathPrefix);
                        }

                        // domain based
                        if (options.MutualTls.DomainName.Contains("."))
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
                entries.Add(OidcConstants.Discovery.FrontChannelLogoutSupported, true);
                entries.Add(OidcConstants.Discovery.FrontChannelLogoutSessionSupported, true);
                entries.Add(OidcConstants.Discovery.BackChannelLogoutSupported, true);
                entries.Add(OidcConstants.Discovery.BackChannelLogoutSessionSupported, true);
            }

            // scopes and claims
            if (options.Discovery.ShowIdentityScopes ||
                options.Discovery.ShowApiScopes ||
                options.Discovery.ShowClaims)
            {
                var resources = await ResourceStore.GetAllEnabledResourcesAsync();
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

                if (scopes.Any())
                {
                    entries.Add(OidcConstants.Discovery.ScopesSupported, scopes.ToArray());
                }

                // claims
                if (options.Discovery.ShowClaims)
                {
                    var claims = new List<string>();

                    // add non-hidden identity scopes related claims
                    claims.AddRange(resources.IdentityResources.Where(x => x.ShowInDiscoveryDocument).SelectMany(x => x.UserClaims));
                    claims.AddRange(resources.ApiResources.Where(x => x.ShowInDiscoveryDocument).SelectMany(x => x.UserClaims));
                    claims.AddRange(resources.ApiScopes.Where(x => x.ShowInDiscoveryDocument).SelectMany(x => x.UserClaims));

                    entries.Add(OidcConstants.Discovery.ClaimsSupported, claims.Distinct().ToArray());
                }
            }

            // grant types
            if (options.Discovery.ShowGrantTypes)
            {
                List<string> standardGrantTypes =
                [
                    OidcConstants.GrantTypes.AuthorizationCode,
                    OidcConstants.GrantTypes.ClientCredentials,
                    OidcConstants.GrantTypes.RefreshToken
                ];

                if (options.Endpoints.EnableDeviceAuthorizationEndpoint)
                {
                    standardGrantTypes.Add(OidcConstants.GrantTypes.DeviceCode);
                }

                var showGrantTypes = new List<string>(standardGrantTypes);

                if (options.Discovery.ShowExtensionGrantTypes)
                {
                    showGrantTypes.AddRange(ExtensionGrants.GetAvailableGrantTypes());
                }

                entries.Add(OidcConstants.Discovery.GrantTypesSupported, showGrantTypes.ToArray());
            }

            // response types
            if (options.Discovery.ShowResponseTypes)
            {
                entries.Add(OidcConstants.Discovery.ResponseTypesSupported, Constants.SupportedResponseTypes.ToArray());
            }

            // response modes
            if (options.Discovery.ShowResponseModes)
            {
                entries.Add(OidcConstants.Discovery.ResponseModesSupported, Constants.SupportedResponseModes.ToArray());
            }

            // misc
            if (options.Discovery.ShowTokenEndpointAuthenticationMethods)
            {
                var types = SecretParsers.GetAvailableAuthenticationMethods().ToList();
                if (options.MutualTls.Enabled)
                {
                    types.Add(OidcConstants.EndpointAuthenticationMethods.TlsClientAuth);
                    types.Add(OidcConstants.EndpointAuthenticationMethods.SelfSignedTlsClientAuth);
                }

                entries.Add(OidcConstants.Discovery.TokenEndpointAuthenticationMethodsSupported, types);
            }

            var signingCredentials = await Keys.GetAllSigningCredentialsAsync();
            if (signingCredentials.Any())
            {
                var signingAlgorithms = signingCredentials.Select(c => c.Algorithm).Distinct();
                entries.Add(OidcConstants.Discovery.IdTokenSigningAlgorithmsSupported, signingAlgorithms);
            }

            entries.Add(OidcConstants.Discovery.SubjectTypesSupported, new[] { "public" });
            entries.Add(OidcConstants.Discovery.CodeChallengeMethodsSupported, new[] { OidcConstants.CodeChallengeMethods.Plain, OidcConstants.CodeChallengeMethods.Sha256 });

            if (options.Endpoints.EnableAuthorizeEndpoint)
            {
                entries.Add(OidcConstants.Discovery.RequestParameterSupported, true);

                if (options.Endpoints.EnableJwtRequestUri)
                {
                    entries.Add(OidcConstants.Discovery.RequestUriParameterSupported, true);
                }
            }

            if (options.MutualTls.Enabled)
            {
                entries.Add(OidcConstants.Discovery.TlsClientCertificateBoundAccessTokens, true);
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
    }
}