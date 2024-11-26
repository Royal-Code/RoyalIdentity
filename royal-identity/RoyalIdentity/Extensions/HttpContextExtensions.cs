using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RoyalIdentity.Options;
using RoyalIdentity.Users.Contracts;

namespace RoyalIdentity.Extensions;

public static class HttpContextExtensions
{
    private static readonly string[] separator = ["://"];

    public static async Task<bool> GetSchemeSupportsSignOutAsync(this HttpContext context, string scheme)
    {
        var provider = context.RequestServices.GetRequiredService<IAuthenticationHandlerProvider>();
        var handler = await provider.GetHandlerAsync(context, scheme);
        return (handler is IAuthenticationSignOutHandler);
    }

    public static void SetServerOrigin(this HttpContext context, string value)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(value);

        var split = value.Split(separator, StringSplitOptions.RemoveEmptyEntries);

        var request = context.Request;
        request.Scheme = split[0];
        request.Host = new HostString(split[^1]);
    }

    public static void SetServerBasePath(this HttpContext context, string value)
    {
        if (context == null) throw new ArgumentNullException(nameof(context));

        context.Items[Constants.EnvironmentKeys.ServerBasePath] = value;
    }

    public static string GetServerOrigin(this HttpContext context, ServerOptions options)
    {
        var request = context.Request;

        if (options.MutualTls.Enabled &&
            options.MutualTls.DomainName.IsPresent() &&
            !options.MutualTls.DomainName.Contains(".") &&
            request.Host.Value.StartsWith(options.MutualTls.DomainName, StringComparison.OrdinalIgnoreCase))
        {
            return string.Concat(request.Scheme, "://", request.Host.Value.AsSpan(options.MutualTls.DomainName.Length + 1));
        }

        return $"{request.Scheme}://{request.Host.Value}";
    }


    internal static void SetSignOutCalled(this HttpContext? context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.Items[Constants.EnvironmentKeys.SignOutCalled] = "true";
    }

    internal static bool GetSignOutCalled(this HttpContext context)
    {
        return context.Items.ContainsKey(Constants.EnvironmentKeys.SignOutCalled);
    }

    /// <summary>
    /// Gets the host name of IdentityServer.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <returns></returns>
    public static string GetServerHost(this HttpContext context)
    {
        var request = context.Request;
        return request.Scheme + "://" + request.Host.ToUriComponent();
    }

    /// <summary>
    /// Gets the base path of IdentityServer.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <returns></returns>
    public static string? GetServerBasePath(this HttpContext context)
    {
        return context.Items[Constants.EnvironmentKeys.ServerBasePath] as string;
    }

    /// <summary>
    /// Gets the public base URL for IdentityServer.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <returns></returns>
    public static string GetServerBaseUrl(this HttpContext context)
    {
        return context.GetServerHost() + context.GetServerBasePath();
    }

    /// <summary>
    /// Gets the identity server relative URL.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="path">The path.</param>
    /// <returns></returns>
    public static string? GetServerRelativeUrl(this HttpContext context, string path)
    {
        if (!path.IsLocalUrl())
            return null;

        if (path.StartsWith("~/"))
            path = path.Substring(1);

        path = context.GetServerBaseUrl().EnsureTrailingSlash() + path.RemoveLeadingSlash();
        return path;
    }

    /// <summary>
    /// Gets the identity server issuer URI.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="options">The options.</param>
    /// <returns></returns>
    /// <exception cref="System.ArgumentNullException">context</exception>
    public static string GetServerIssuerUri(this HttpContext context)
    {
        var options = context.RequestServices.GetRequiredService<IOptions<ServerOptions>>().Value;
        return context.GetServerIssuerUri(options);
    }

    /// <summary>
    /// Gets the identity server issuer URI.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="options">The options.</param>
    /// <returns></returns>
    /// <exception cref="System.ArgumentNullException">context</exception>
    public static string GetServerIssuerUri(this HttpContext context, ServerOptions options)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(options);

        // if they've explicitly configured a URI then use it,
        // otherwise dynamically calculate it
        if (options.IssuerUri.IsPresent())
            return options.IssuerUri;

        var uri = $"{context.GetServerOrigin(options)}{context.GetServerBasePath()}";
        if (uri.EndsWith('/'))
            uri = uri[..^1];
        if (options.LowerCaseIssuerUri)
            uri = uri.ToLowerInvariant();

        options.IssuerUri = uri;

        return options.IssuerUri;
    }

    public static async Task<bool> ValidateUserSessionAsync(this HttpContext context, ClaimsPrincipal principal)
    {
        var sessionId = principal.GetSessionId();
        var userSessionStore = context.RequestServices.GetRequiredService<IUserSessionStore>();
        var currentSession = await userSessionStore.GetUserSessionAsync(sessionId, context.RequestAborted);
        return currentSession is { IsActive : true };
    }

    //internal static async Task<string> GetIdentityServerSignoutFrameCallbackUrlAsync(this HttpContext context, LogoutMessage logoutMessage = null)
    //{
    //    var userSession = context.RequestServices.GetRequiredService<IUserSession>();
    //    var user = await userSession.GetUserAsync();
    //    var currentSubId = user?.GetSubjectId();

    //    LogoutNotificationContext endSessionMsg = null;

    //    // if we have a logout message, then that take precedence over the current user
    //    if (logoutMessage?.ClientIds?.Any() == true)
    //    {
    //        var clientIds = logoutMessage?.ClientIds;

    //        // check if current user is same, since we might have new clients (albeit unlikely)
    //        if (currentSubId == logoutMessage?.SubjectId)
    //        {
    //            clientIds = clientIds.Union(await userSession.GetClientListAsync());
    //            clientIds = clientIds.Distinct();
    //        }

    //        endSessionMsg = new LogoutNotificationContext
    //        {
    //            SubjectId = logoutMessage.SubjectId,
    //            SessionId = logoutMessage.SessionId,
    //            ClientIds = clientIds
    //        };
    //    }
    //    else if (currentSubId != null)
    //    {
    //        // see if current user has any clients they need to signout of 
    //        var clientIds = await userSession.GetClientListAsync();
    //        if (clientIds.Any())
    //        {
    //            endSessionMsg = new LogoutNotificationContext
    //            {
    //                SubjectId = currentSubId,
    //                SessionId = await userSession.GetSessionIdAsync(),
    //                ClientIds = clientIds
    //            };
    //        }
    //    }

    //    if (endSessionMsg != null)
    //    {
    //        var clock = context.RequestServices.GetRequiredService<ISystemClock>();
    //        var msg = new Message<LogoutNotificationContext>(endSessionMsg, clock.UtcNow.UtcDateTime);

    //        var endSessionMessageStore = context.RequestServices.GetRequiredService<IMessageStore<LogoutNotificationContext>>();
    //        var id = await endSessionMessageStore.WriteAsync(msg);

    //        var signoutIframeUrl = context.GetIdentityServerBaseUrl().EnsureTrailingSlash() + Constants.ProtocolRoutePaths.EndSessionCallback;
    //        signoutIframeUrl = signoutIframeUrl.AddQueryString(Constants.UIConstants.DefaultRoutePathParams.EndSessionCallback, id);

    //        return signoutIframeUrl;
    //    }

    //    // no sessions, so nothing to cleanup
    //    return null;
    //}
}