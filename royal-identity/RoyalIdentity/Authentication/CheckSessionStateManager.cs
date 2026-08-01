using Microsoft.AspNetCore.Http;
using RoyalIdentity.Models;
using RoyalIdentity.Options;
using RoyalIdentity.Security.Cryptography;
using RoyalIdentity.Security.Encoding;
using AuthenticationProperties = Microsoft.AspNetCore.Authentication.AuthenticationProperties;

namespace RoyalIdentity.Authentication;

/// <summary>
/// Owns the request-local and protected-ticket representation of the opaque OP User Agent State.
/// Cookie I/O is added by the lifecycle phase; this type remains independent from storage and user modules.
/// </summary>
public sealed class CheckSessionStateManager
{
    internal const int StateEntropyBytes = 32;

    internal string CreateState() => CryptoRandom.CreateUniqueId(StateEntropyBytes);

    internal string GetOrCreateState(AuthenticationProperties properties)
    {
        ArgumentNullException.ThrowIfNull(properties);

        if (properties.Items.TryGetValue(Server.CheckSessionStateAuthenticationProperty, out var current)
            && IsValidState(current))
        {
            return current!;
        }

        var created = CreateState();
        properties.Items[Server.CheckSessionStateAuthenticationProperty] = created;
        return created;
    }

    internal string GetOrCreateRequestState(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        if (TryGetRequestState(httpContext, out var current))
            return current;

        var created = CreateState();
        PublishRequestState(httpContext, created);
        return created;
    }

    internal void PublishRequestState(HttpContext httpContext, string state)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        if (!IsValidState(state))
        {
            throw new ArgumentException(
                "The OP User Agent State is not a canonical 256-bit Base64Url value.",
                nameof(state));
        }

        httpContext.Items[Server.CheckSessionStateHttpContextItem] = state;
    }

    internal static bool TryGetRequestState(HttpContext httpContext, out string state)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        if (httpContext.Items.TryGetValue(Server.CheckSessionStateHttpContextItem, out var item)
            && item is string current
            && IsValidState(current))
        {
            state = current;
            return true;
        }

        state = string.Empty;
        return false;
    }

    internal static string GetCookieName(AuthenticationOptions options, Realm realm)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(realm);

        if (!CookieNameValidation.IsValid(options.CheckSessionCookieName))
            throw new InvalidOperationException("Authentication.CheckSessionCookieName is not a valid cookie name.");

        if (!CookieNameValidation.IsValid(realm.Path))
            throw new InvalidOperationException("The realm path cannot be used as a check-session cookie qualifier.");

        var checkSessionName = $"{options.CheckSessionCookieName}.{realm.Path}";
        var realmAuthenticationName = $"{options.CookieName}.{realm.Path}";

        if (string.Equals(checkSessionName, options.CookieName, StringComparison.Ordinal)
            || string.Equals(checkSessionName, realmAuthenticationName, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The effective check-session cookie name collides with an authentication cookie name.");
        }

        return checkSessionName;
    }

    internal static PathString GetCookiePath(Realm realm)
    {
        ArgumentNullException.ThrowIfNull(realm);

        if (string.IsNullOrEmpty(realm.Path) || realm.Path.Any(char.IsControl) || realm.Path.Contains('/'))
            throw new InvalidOperationException("The realm path cannot be used as a check-session cookie path.");

        return new PathString($"/{realm.Path}");
    }

    internal static CookieOptions CreateCookieOptions(Realm realm)
        => new()
        {
            Path = GetCookiePath(realm),
            Secure = true,
            SameSite = SameSiteMode.None,
            HttpOnly = false,
            IsEssential = true,
        };

    internal static bool IsValidState(string? state)
        => state is not null
            && Base64Url.TryDecode(state, out var bytes)
            && bytes.Length == StateEntropyBytes
            && string.Equals(state, Base64Url.Encode(bytes), StringComparison.Ordinal);
}
