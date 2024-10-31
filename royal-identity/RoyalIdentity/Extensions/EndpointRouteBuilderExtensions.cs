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
        endpoints.MapPipeline<AuthorizeEndpoint>(Constants.ProtocolRoutePaths.Authorize);
    }
}