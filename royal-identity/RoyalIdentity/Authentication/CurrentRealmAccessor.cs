using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Http;
using RoyalIdentity.Contracts;
using RoyalIdentity.Extensions;
using RoyalIdentity.Models;

namespace RoyalIdentity.Authentication;

/// <summary>
/// HTTP-backed <see cref="ICurrentRealmAccessor"/> that reads the current realm from the request's
/// <see cref="HttpContext"/> (populated by the realm discovery middleware). It only resolves the realm;
/// it performs no cookie I/O (ADR-014 §2.5).
/// </summary>
public sealed class CurrentRealmAccessor(IHttpContextAccessor httpContextAccessor) : ICurrentRealmAccessor
{
    public Realm GetCurrentRealm()
    {
        var httpContext = httpContextAccessor.HttpContext
            ?? throw new InvalidOperationException("No HttpContext available to resolve the current realm.");

        return httpContext.GetCurrentRealm();
    }

    public bool TryGetCurrentRealm([NotNullWhen(true)] out Realm? realm)
    {
        var httpContext = httpContextAccessor.HttpContext;
        if (httpContext is not null)
            return httpContext.TryGetCurrentRealm(out realm);

        realm = null;
        return false;
    }
}
