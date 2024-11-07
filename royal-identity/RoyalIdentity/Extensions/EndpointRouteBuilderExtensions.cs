using Microsoft.AspNetCore.Routing;
using RoyalIdentity.Endpoints;
using RoyalIdentity.Options;
using RoyalIdentity.Pipelines.Infrastructure;

namespace RoyalIdentity.Extensions;

public static class EndpointRouteBuilderExtensions
{
    public static void MapOpenIdConnectProviderEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPipeline<DiscoveryEndpoint>(Constants.ProtocolRoutePaths.DiscoveryConfiguration);
        endpoints.MapPipeline<JwkEndpoint>(Constants.ProtocolRoutePaths.DiscoveryWebKeys);
        endpoints.MapPipeline<AuthorizeEndpoint>(Constants.ProtocolRoutePaths.Authorize);
        endpoints.MapPipeline<AuthorizeCallbackEndpoint>(Constants.ProtocolRoutePaths.AuthorizeCallback);
        endpoints.MapPipeline<TokenEndpoint>(Constants.ProtocolRoutePaths.Token);
    }
}