using Microsoft.AspNetCore.Routing;
using RoyalIdentity.Endpoints;
using RoyalIdentity.Pipelines.Infrastructure;

namespace RoyalIdentity.Extensions;

public static class EndpointRouteBuilderExtensions
{
    public static void MapOpenIdConnectProviderEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPipeline<DiscoveryEndpoint>(ProtocolRoutePaths.DiscoveryConfiguration);
        endpoints.MapPipeline<JwkEndpoint>(ProtocolRoutePaths.DiscoveryWebKeys);
        endpoints.MapPipeline<AuthorizeEndpoint>(ProtocolRoutePaths.Authorize);
        endpoints.MapPipeline<AuthorizeCallbackEndpoint>(ProtocolRoutePaths.AuthorizeCallback);
        endpoints.MapPipeline<TokenEndpoint>(ProtocolRoutePaths.Token);
        endpoints.MapPipeline<UserInfoEndpoint>(ProtocolRoutePaths.UserInfo);
        endpoints.MapPipeline<RevocationEndpoint>(ProtocolRoutePaths.Revocation);
        endpoints.MapPipeline<EndSessionEndpoint>(ProtocolRoutePaths.EndSession);
    }
}