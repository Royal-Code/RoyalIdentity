using Microsoft.AspNetCore.Http;
using RoyalIdentity.Endpoints.Abstractions;

namespace RoyalCode.Endpoints.OAuth.Endpoints;

public class AuthorizeEndpoint : IEndpointHandler
{
    public ValueTask<EndpointCreationResult> TryCreateContextAsync(HttpContext httpContext)
    {
        throw new NotImplementedException();
    }
}