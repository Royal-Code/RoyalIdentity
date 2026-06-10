using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Extensions;
using RoyalIdentity.Models;
using RoyalIdentity.Options;

namespace RoyalIdentity.Authentication;

public class RealmCorsMiddleware
{
    private const string Wildcard = "*";
    private const string AccessControlAllowOrigin = "Access-Control-Allow-Origin";
    private const string AccessControlAllowMethods = "Access-Control-Allow-Methods";
    private const string AccessControlAllowHeaders = "Access-Control-Allow-Headers";
    private const string AccessControlAllowCredentials = "Access-Control-Allow-Credentials";
    private const string AccessControlRequestMethod = "Access-Control-Request-Method";
    private const string AccessControlRequestHeaders = "Access-Control-Request-Headers";
    private const string Origin = "Origin";
    private const string Vary = "Vary";

    private readonly RequestDelegate next;
    private readonly ILogger logger;

    public RealmCorsMiddleware(
        RequestDelegate next,
        ILogger<RealmCorsMiddleware> logger)
    {
        this.next = next;
        this.logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.TryGetCurrentRealm(out var realm) ||
            !IsCorsPath(context, realm))
        {
            await next(context);
            return;
        }

        var origin = context.Request.GetCorsOrigin();
        if (origin.IsMissing())
        {
            await next(context);
            return;
        }

        AddVaryOrigin(context.Response);

        var options = realm.Options.Cors;
        var isPreflight = IsPreflight(context.Request);

        if (!options.Enabled)
        {
            await next(context);
            return;
        }

        var storage = context.RequestServices.GetRequiredService<IStorage>();
        var evaluation = await EvaluateAsync(context, storage, realm, options, origin, isPreflight);
        if (!evaluation.Allowed)
        {
            if (isPreflight)
            {
                logger.LogDebug("CORS preflight rejected for realm {RealmId} and origin {Origin}", realm.Id, origin);
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return;
            }

            await next(context);
            return;
        }

        ApplyResponseHeaders(context, options, origin);

        if (isPreflight)
        {
            ApplyPreflightHeaders(context, evaluation.RequestedMethod!, evaluation.RequestedHeaders);
            context.Response.StatusCode = StatusCodes.Status204NoContent;
            return;
        }

        await next(context);
    }

    private async ValueTask<CorsEvaluation> EvaluateAsync(
        HttpContext context,
        IStorage storage,
        Realm realm,
        CorsOptions options,
        string origin,
        bool isPreflight)
    {
        if (!IsOriginAllowed(origin, options.AllowedOrigins, options.AllowCredentials))
        {
            var clientId = await TryGetClientIdAsync(context, isPreflight);
            if (clientId.IsMissing())
            {
                return CorsEvaluation.Rejected();
            }

            var client = await storage.GetClientStore(realm)
                .FindEnabledClientByIdAsync(clientId, context.RequestAborted);

            if (client is null ||
                !IsOriginAllowed(origin, client.AllowedCorsOrigins, options.AllowCredentials))
            {
                return CorsEvaluation.Rejected();
            }
        }

        if (!isPreflight)
        {
            return CorsEvaluation.Accepted(null, []);
        }

        var requestedMethod = context.Request.Headers[AccessControlRequestMethod].FirstOrDefault();
        if (requestedMethod.IsMissing() ||
            !IsItemAllowed(requestedMethod, options.AllowedMethods))
        {
            return CorsEvaluation.Rejected();
        }

        var requestedHeaders = GetRequestedHeaders(context.Request);
        if (requestedHeaders.Any(header => !IsItemAllowed(header, options.AllowedHeaders)))
        {
            return CorsEvaluation.Rejected();
        }

        return CorsEvaluation.Accepted(requestedMethod, requestedHeaders);
    }

    private static bool IsPreflight(HttpRequest request)
    {
        return HttpMethods.IsOptions(request.Method) &&
            request.Headers.ContainsKey(AccessControlRequestMethod);
    }

    private static bool IsCorsPath(HttpContext context, Realm realm)
    {
        var requestPath = context.Request.Path.Value?.Trim('/');
        if (requestPath.IsMissing())
        {
            return false;
        }

        foreach (var corsPath in Oidc.Routes.CorsPaths)
        {
            var expectedPath = corsPath
                .Replace($"{{{Server.RealmRouteKey}}}", realm.Path)
                .Trim('/');

            if (string.Equals(requestPath, expectedPath, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private async ValueTask<string?> TryGetClientIdAsync(HttpContext context, bool isPreflight)
    {
        var request = context.Request;
        var clientId = request.Query[Oidc.Token.Request.ClientId].FirstOrDefault();
        if (clientId.IsPresent())
        {
            return clientId;
        }

        if (isPreflight || !request.HasApplicationFormContentType())
        {
            return null;
        }

        var form = await request.ReadFormAsync(context.RequestAborted);
        return form[Oidc.Token.Request.ClientId].FirstOrDefault();
    }

    private static bool IsOriginAllowed(
        string origin,
        IEnumerable<string> allowedOrigins,
        bool allowCredentials)
    {
        var normalizedOrigin = NormalizeOrigin(origin);
        if (normalizedOrigin is null)
        {
            return false;
        }

        foreach (var allowedOrigin in allowedOrigins)
        {
            if (allowedOrigin == Wildcard)
            {
                if (!allowCredentials)
                {
                    return true;
                }

                continue;
            }

            var normalizedAllowedOrigin = NormalizeOrigin(allowedOrigin);
            if (string.Equals(normalizedOrigin, normalizedAllowedOrigin, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string? NormalizeOrigin(string origin)
    {
        if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri))
        {
            return null;
        }

        if (uri.Scheme is not "http" and not "https" ||
            uri.UserInfo.IsPresent())
        {
            return null;
        }

        var port = uri.IsDefaultPort ? string.Empty : $":{uri.Port}";
        return $"{uri.Scheme}://{uri.IdnHost}{port}";
    }

    private static bool IsItemAllowed(string item, HashSet<string> allowedItems)
    {
        return allowedItems.Contains(Wildcard) || allowedItems.Contains(item);
    }

    private static IReadOnlyList<string> GetRequestedHeaders(HttpRequest request)
    {
        var rawHeaders = request.Headers[AccessControlRequestHeaders].FirstOrDefault();
        if (rawHeaders.IsMissing())
        {
            return [];
        }

        return rawHeaders
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(header => header.IsPresent())
            .ToArray();
    }

    private static void ApplyResponseHeaders(HttpContext context, CorsOptions options, string origin)
    {
        context.Response.Headers[AccessControlAllowOrigin] = origin;
        AddVaryOrigin(context.Response);

        if (options.AllowCredentials)
        {
            context.Response.Headers[AccessControlAllowCredentials] = "true";
        }
    }

    private static void ApplyPreflightHeaders(
        HttpContext context,
        string requestedMethod,
        IReadOnlyList<string> requestedHeaders)
    {
        context.Response.Headers[AccessControlAllowMethods] = requestedMethod;

        if (requestedHeaders.Count is not 0)
        {
            context.Response.Headers[AccessControlAllowHeaders] = string.Join(", ", requestedHeaders);
        }
    }

    private static void AddVaryOrigin(HttpResponse response)
    {
        var vary = response.Headers[Vary].ToString();
        if (vary.IsMissing())
        {
            response.Headers[Vary] = Origin;
            return;
        }

        var values = vary.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (!values.Contains(Origin, StringComparer.OrdinalIgnoreCase))
        {
            response.Headers[Vary] = $"{vary}, {Origin}";
        }
    }

    private readonly record struct CorsEvaluation(
        bool Allowed,
        string? RequestedMethod,
        IReadOnlyList<string> RequestedHeaders)
    {
        public static CorsEvaluation Accepted(string? requestedMethod, IReadOnlyList<string> requestedHeaders)
            => new(true, requestedMethod, requestedHeaders);

        public static CorsEvaluation Rejected()
            => new(false, null, []);
    }
}

public static class RealmCorsMiddlewareExtensions
{
    /// <summary>
    /// Adds realm-aware CORS processing for OIDC endpoints.
    /// </summary>
    public static IApplicationBuilder UseRealmCors(this IApplicationBuilder app)
    {
        return app.UseMiddleware<RealmCorsMiddleware>();
    }
}
