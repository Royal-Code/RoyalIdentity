
using Microsoft.AspNetCore.Http;

namespace RoyalIdentity.Endpoins.Abstractions;

public interface IEndpointHandler<TContext>
    where TContext : class, IContextBase
{

    ValueTask<bool> TryCreateContextAsync(HttpContext httpContext, out TContext context);
}
