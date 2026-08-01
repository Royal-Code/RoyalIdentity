using Microsoft.AspNetCore.Http;
using RoyalIdentity.Models;
using RoyalIdentity.Options;
using RoyalIdentity.Security.Cryptography;
using RoyalIdentity.Security.Encoding;
using RoyalIdentity.Extensions;
using AuthenticationProperties = Microsoft.AspNetCore.Authentication.AuthenticationProperties;

namespace RoyalIdentity.Authentication;

/// <summary>
/// Owns the protected-ticket, request-local and browser-cookie representations of the opaque OP User Agent State.
/// This type remains independent from storage and user modules.
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

    internal void PrepareSignIn(HttpContext httpContext, AuthenticationProperties properties)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(properties);

        var realm = httpContext.GetCurrentRealm();
        if (!realm.Options.Endpoints.EnableCheckSessionEndpoint)
        {
            properties.Items.Remove(Server.CheckSessionStateAuthenticationProperty);
            ClearRequestState(httpContext);
            DeleteCookieIfPresent(httpContext, realm);
            return;
        }

        var state = GetOrCreateState(properties);
        PublishRequestState(httpContext, state);
        IssueCookieIfRequired(httpContext, realm, state);
    }

    internal bool SynchronizeAuthenticatedRequest(
        HttpContext httpContext,
        AuthenticationProperties properties)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(properties);

        var realm = httpContext.GetCurrentRealm();
        if (!realm.Options.Endpoints.EnableCheckSessionEndpoint)
        {
            var removedTicketState = properties.Items.Remove(Server.CheckSessionStateAuthenticationProperty);
            ClearRequestState(httpContext);
            DeleteCookieIfPresent(httpContext, realm);
            return removedTicketState;
        }

        var ticketChanged = !TryGetState(properties, out var state);
        if (ticketChanged)
            state = GetOrCreateState(properties);

        PublishRequestState(httpContext, state);
        IssueCookieIfRequired(httpContext, realm, state);
        return ticketChanged;
    }

    internal void RemoveCookie(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        ClearRequestState(httpContext);
        DeleteCookieIfPresent(httpContext, httpContext.GetCurrentRealm());
    }

    internal void RemoveCookie(HttpContext httpContext, Realm realm)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(realm);

        ClearRequestState(httpContext);
        DeleteCookieIfPresent(httpContext, realm);
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

    private static bool TryGetState(AuthenticationProperties properties, out string state)
    {
        if (properties.Items.TryGetValue(Server.CheckSessionStateAuthenticationProperty, out var current)
            && IsValidState(current))
        {
            state = current!;
            return true;
        }

        state = string.Empty;
        return false;
    }

    private static void ClearRequestState(HttpContext httpContext)
        => httpContext.Items.Remove(Server.CheckSessionStateHttpContextItem);

    private static void IssueCookieIfRequired(HttpContext httpContext, Realm realm, string state)
    {
        var name = GetCookieName(realm.Options.Authentication, realm);
        if (!string.Equals(httpContext.Request.Cookies[name], state, StringComparison.Ordinal))
            httpContext.Response.Cookies.Append(name, state, CreateCookieOptions(realm));
    }

    private static void DeleteCookieIfPresent(HttpContext httpContext, Realm realm)
    {
        var name = GetCookieName(realm.Options.Authentication, realm);
        if (httpContext.Request.Cookies.ContainsKey(name))
            httpContext.Response.Cookies.Delete(name, CreateCookieOptions(realm));
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
