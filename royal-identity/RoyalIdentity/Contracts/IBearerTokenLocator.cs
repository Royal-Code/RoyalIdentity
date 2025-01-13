using Microsoft.AspNetCore.Http;
using RoyalIdentity.Contracts.Models;

namespace RoyalIdentity.Contracts;

public interface IBearerTokenLocator
{
    public Task<BearerTokenResult> LocateAsync(HttpContext context);
}
