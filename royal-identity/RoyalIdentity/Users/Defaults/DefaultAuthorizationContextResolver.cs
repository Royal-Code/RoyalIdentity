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

        // MP-5: the authorize-parameters store is realm-bound, so the resolver needs the current realm, not
        // only its path (plan-data-operational-storage DF16).
        if (!httpContext.TryGetCurrentRealm(out var realm))
            throw new InvalidOperationException("Resolving an authorization context requires a realm context.");

        if (returnUrl.IsValidReturnUrl(realm.Path))
        {
            logger.LogDebug("returnUrl is valid");

            var parameters = returnUrl.ReadQueryStringAsNameValueCollection();

            // DF16: the store is only consulted when the realm keeps authorize parameters server-side. With the
            // option off, the parameters travel in the query string and nothing here touches the store.
            if (realm.Options.StoreAuthorizationParameters
                && parameters.TryGet(Oidc.Routes.Params.Authorization, out var messageStoreId))
            {
                parameters = await storage.GetAuthorizeParametersStore(realm).ReadAsync(messageStoreId, ct) ?? [];
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
