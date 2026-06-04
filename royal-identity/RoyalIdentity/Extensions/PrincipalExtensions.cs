using RoyalIdentity.Options;
using System.Diagnostics;
using System.Security.Claims;
using System.Security.Principal;

namespace RoyalIdentity.Extensions;

/// <summary>
/// Extension methods for <see cref="System.Security.Principal.IPrincipal"/> and <see cref="System.Security.Principal.IIdentity"/> .
/// </summary>
public static class PrincipalExtensions
{
    /// <summary>
    /// Gets the authentication time.
    /// </summary>
    /// <param name="principal">The principal.</param>
    /// <returns></returns>
    [DebuggerStepThrough]
    public static DateTime GetAuthenticationTime(this IPrincipal principal)
    {
        return DateTimeOffset.FromUnixTimeSeconds(principal.GetAuthenticationTimeEpoch()).UtcDateTime;
    }

    /// <summary>
    /// Gets the authentication epoch time.
    /// </summary>
    /// <param name="principal">The principal.</param>
    /// <returns></returns>
    [DebuggerStepThrough]
    public static long GetAuthenticationTimeEpoch(this IPrincipal principal)
    {
        return principal?.Identity?.GetAuthenticationTimeEpoch() ?? throw new InvalidOperationException("auth_time is missing.");
    }

    /// <summary>
    /// Gets the authentication epoch time.
    /// </summary>
    /// <param name="identity">The identity.</param>
    /// <returns></returns>
    [DebuggerStepThrough]
    public static long GetAuthenticationTimeEpoch(this IIdentity identity)
    {
        if (identity is ClaimsIdentity id)
        {
            var claim = id.FindFirst(JwtRegisteredClaimNames.AuthTime);
            if (claim is not null)
                return long.Parse(claim.Value);
        }

        throw new InvalidOperationException("auth_time is missing.");
    }

    /// <summary>
    /// Gets the subject identifier.
    /// </summary>
    /// <param name="principal">The principal.</param>
    /// <returns></returns>
    [DebuggerStepThrough]
    public static string GetSubjectId(this IPrincipal principal)
    {
        return principal.Identity?.GetSubjectId() ?? throw new InvalidOperationException("sub claim is missing");
    }

    /// <summary>
    /// Gets the subject identifier.
    /// </summary>
    /// <param name="identity">The identity.</param>
    /// <returns></returns>
    /// <exception cref="System.InvalidOperationException">sub claim is missing</exception>
    [DebuggerStepThrough]
    public static string GetSubjectId(this IIdentity identity)
    {
        if (identity is ClaimsIdentity id)
        {
            var claim = id.FindFirst(JwtRegisteredClaimNames.Sub);
            if (claim is not null)
                return claim.Value;
        }

        throw new InvalidOperationException("sub claim is missing");
    }

    /// <summary>
    /// Gets the session identifier.
    /// </summary>
    /// <param name="principal">The principal.</param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException">sid claim is missing</exception>
    [DebuggerStepThrough]
    public static string GetSessionId(this IPrincipal principal)
    {
        return principal.Identity?.GetSessionId() ?? throw new InvalidOperationException("sid claim is missing");
    }

    /// <summary>
    /// Gets the session identifier.
    /// </summary>
    /// <param name="identity">The identity.</param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException">sid claim is missing</exception>
    [DebuggerStepThrough]
    public static string GetSessionId(this IIdentity identity)
    {
        if (identity is ClaimsIdentity id)
        {
            var claim = id.FindFirst(JwtRegisteredClaimNames.Sid);
            if (claim is not null)
                return claim.Value;
        }

        throw new InvalidOperationException("sid claim is missing");
    }

    /// <summary>
    /// Gets the name.
    /// </summary>
    /// <param name="principal">The principal.</param>
    /// <returns></returns>
    [DebuggerStepThrough]
    public static string GetDisplayName(this ClaimsPrincipal principal)
    {
        var name = principal.Identity?.Name;
        if (name.IsPresent())
            return name;

        var sub = principal.FindFirst(JwtRegisteredClaimNames.Sub);
        if (sub != null) 
            return sub.Value;

        return string.Empty;
    }

    /// <summary>
    /// Gets the authentication method.
    /// </summary>
    /// <param name="principal">The principal.</param>
    /// <returns></returns>
    [DebuggerStepThrough]
    public static string GetAuthenticationMethod(this IPrincipal principal)
    {
        return principal.Identity?.GetAuthenticationMethod() ?? throw new InvalidOperationException("amr claim is missing");
    }

    /// <summary>
    /// Gets the authentication method.
    /// </summary>
    /// <param name="identity">The identity.</param>
    /// <returns></returns>
    /// <exception cref="System.InvalidOperationException">amr claim is missing</exception>
    [DebuggerStepThrough]
    public static string GetAuthenticationMethod(this IIdentity identity)
    {
        if (identity is ClaimsIdentity id)
        {
            var claim = id.FindFirst(JwtRegisteredClaimNames.Amr);
            if (claim is not null)
                return claim.Value;
        }

        throw new InvalidOperationException("amr claim is missing");
    }

    /// <summary>
    /// Gets the authentication method claims.
    /// </summary>
    /// <param name="principal">The principal.</param>
    /// <returns></returns>
    [DebuggerStepThrough]
    public static IEnumerable<Claim> GetAuthenticationMethods(this IPrincipal principal)
    {
        return principal.Identity?.GetAuthenticationMethods() ?? throw new InvalidOperationException("amr claim is missing");
    }

    /// <summary>
    /// Gets the authentication method claims.
    /// </summary>
    /// <param name="identity">The identity.</param>
    /// <returns></returns>
    [DebuggerStepThrough]
    public static IEnumerable<Claim> GetAuthenticationMethods(this IIdentity identity)
    {
        return identity is ClaimsIdentity id
            ? id.FindAll(JwtRegisteredClaimNames.Amr) 
            : ([]);
    }

    /// <summary>
    /// Gets the identity provider.
    /// </summary>
    /// <param name="principal">The principal.</param>
    /// <returns></returns>
    [DebuggerStepThrough]
    public static string GetIdentityProvider(this IPrincipal principal)
    {
        return principal?.Identity?.GetIdentityProvider() ?? throw new InvalidOperationException("IdP claim is missing");
    }

    /// <summary>
    /// Gets the identity provider.
    /// </summary>
    /// <param name="identity">The identity.</param>
    /// <returns></returns>
    /// <exception cref="System.InvalidOperationException">IdP claim is missing</exception>
    [DebuggerStepThrough]
    public static string GetIdentityProvider(this IIdentity identity)
    {
        if (identity is ClaimsIdentity id)
        {
            var claim = id.FindFirst(Jwt.ClaimTypes.IdentityProvider);
            if (claim is not null)
                return claim.Value;
        }

        throw new InvalidOperationException("IdP claim is missing");
    }

    /// <summary>
    /// Determines whether this instance is authenticated.
    /// </summary>
    /// <param name="principal">The principal.</param>
    /// <returns>
    ///   <c>true</c> if the specified principal is authenticated; otherwise, <c>false</c>.
    /// </returns>
    [DebuggerStepThrough]
    public static bool IsAuthenticated(this IPrincipal principal)
    {
        return principal != null && principal.Identity != null && principal.Identity.IsAuthenticated;
    }
}
