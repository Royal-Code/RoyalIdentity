using Microsoft.AspNetCore.Routing;
using RoyalIdentity.Endpoints;
using RoyalIdentity.Pipelines.Infrastructure;

namespace RoyalIdentity.Extensions;

public static class EndpointRouteBuilderExtensions
{
    public static void MapOpenIdConnectProviderEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPipeline<DiscoveryEndpoint>(Oidc.Routes.DiscoveryConfiguration);
        endpoints.MapPipeline<JwkEndpoint>(Oidc.Routes.DiscoveryWebKeys);
        endpoints.MapPipeline<AuthorizeEndpoint>(Oidc.Routes.Authorize);
        endpoints.MapPipeline<AuthorizeCallbackEndpoint>(Oidc.Routes.AuthorizeCallback);
        endpoints.MapPipeline<TokenEndpoint>(Oidc.Routes.Token);
        endpoints.MapPipeline<UserInfoEndpoint>(Oidc.Routes.UserInfo);
        endpoints.MapPipeline<RevocationEndpoint>(Oidc.Routes.Revocation);
        endpoints.MapPipeline<EndSessionEndpoint>(Oidc.Routes.EndSession);
    }
}