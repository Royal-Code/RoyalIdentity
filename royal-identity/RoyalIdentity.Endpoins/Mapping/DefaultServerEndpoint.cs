
using Microsoft.AspNetCore.Http;
using RoyalIdentity.Endpoins.Abstractions;

namespace RoyalIdentity.Endpoins.Mapping;

public static class ServerEndpoint<T>
    where T: IEndpointHandler
{

    public static IResult EndpointHandler(
        HttpContext httpContext, 
        T endpointHandler)
    {

    }
}

public class DefaultServerEndpoint
{

}