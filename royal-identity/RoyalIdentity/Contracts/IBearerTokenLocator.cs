using Microsoft.AspNetCore.Http;
using RoyalIdentity.Contracts.Models;

namespace RoyalIdentity.Contracts;

public interface IBearerTokenLocator
{
    public Task<BearerTokenResult> LocatorAsync(HttpContext context);

    public BearerTokenResult LocatorAuthorizationHeader(string authorizationHeader);

    public Task<BearerTokenResult> LocatorPostBodyAsync(HttpContext context);
}
