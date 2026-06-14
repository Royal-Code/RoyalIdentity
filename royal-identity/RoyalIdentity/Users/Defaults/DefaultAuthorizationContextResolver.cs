using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using RoyalIdentity.Contracts;
using RoyalIdentity.Contracts.Models;
using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Extensions;
using RoyalIdentity.Options;
using RoyalIdentity.Users.Contexts;
using RoyalIdentity.Users.Contracts;

namespace RoyalIdentity.Users.Defaults;

/// <summary>
/// Resolves the <see cref="AuthorizationContext"/> from a returnUrl for the login/consent/logout screens
/// (Q2, ADR-014 §2.4). Replaces <c>ISignInManager.GetAuthorizationContextAsync</c>.
/// </summary>
public sealed class DefaultAuthorizationContextResolver(
    IHttpContextAccessor httpContextAccessor,
    IStorage storage,
    IAuthorizeRequestValidator authorizeRequestValidator,
    ILogger<DefaultAuthorizationContextResolver> logger) : IAuthorizationContextResolver
{
    public async Task<AuthorizationContext?> ResolveAsync(string? returnUrl, CancellationToken ct = default)
    {
        var httpContext = httpContextAccessor.HttpContext
            ?? throw new InvalidOperationException("Resolving an authorization context requires an HTTP context.");

        var realm = httpContext.GetRealmPath();
        if (realm is null)
            throw new InvalidOperationException("Resolving an authorization context requires a realm context.");

        if (returnUrl.IsValidReturnUrl(realm))
        {
            logger.LogDebug("returnUrl is valid");

            var parameters = returnUrl.ReadQueryStringAsNameValueCollection();
            if (parameters.TryGet(Oidc.Routes.Params.Authorization, out var messageStoreId))
            {
                parameters = await storage.AuthorizeParameters.ReadAsync(messageStoreId, ct) ?? [];
            }

            var authorizationRequest = new AuthorizationValidationRequest
            {
                HttpContext = httpContext,
                Parameters = parameters
            };
            var authorizationResult = await authorizeRequestValidator.ValidateAsync(authorizationRequest, ct);
            if (authorizationResult.Error is null && authorizationResult.Context is not null)
            {
                logger.LogTrace("AuthorizationRequest being returned");
                return authorizationResult.Context;
            }
        }
        else
        {
            logger.LogDebug("The parameter 'returnUrl' is not valid: {ReturnUrl}", returnUrl);
        }

        logger.LogDebug("No AuthorizationRequest being returned");
        return null;
    }
}
