using Microsoft.AspNetCore.Http;
using RoyalIdentity.Extensions;
using RoyalIdentity.Models;
using RoyalIdentity.Users.Contexts;
using System.Diagnostics.CodeAnalysis;

namespace RoyalIdentity.Razor.Services;

public class SessionContextService(IHttpContextAccessor httpContextAccessor) : ISessionContextService
{
    public bool TryGetCurrentRealm([NotNullWhen(true)] out Realm? realm)
    {
        var httpContext = httpContextAccessor.HttpContext;
        if (httpContext is null)
        {
            realm = null;
            return false;
        }
        return httpContext.TryGetCurrentRealm(out realm);
    }

    public ValueTask<AuthorizationContext?> GetAuthorizationContextAsync(string? returnUrl)
    {
        var httpContext = httpContextAccessor.HttpContext;
        if (httpContext is null)
            return ValueTask.FromResult<AuthorizationContext?>(null);
        return httpContext.GetAuthorizationContextAsync(returnUrl);
    }
}
