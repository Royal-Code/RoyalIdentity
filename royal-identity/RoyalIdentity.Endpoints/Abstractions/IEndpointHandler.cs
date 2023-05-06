
using Microsoft.AspNetCore.Http;

namespace RoyalIdentity.Endpoints.Abstractions;

public interface IEndpointHandler<TContext>
    where TContext : class, IContextBase
{

    ValueTask<bool> TryCreateContextAsync(HttpContext httpContext, out TContext context);
}
