using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using RoyalIdentity.Contracts;
using RoyalIdentity.Contracts.Models;
using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Extensions;
using RoyalIdentity.Models;
using RoyalIdentity.Options;
using RoyalIdentity.Users.Contexts;
using static RoyalIdentity.Users.CredentialsValidationResult.WellKnownReasons;

namespace RoyalIdentity.Users.Defaults;

public class DefaultSignInManager : ISignInManager
{
    private readonly IHttpContextAccessor httpContextAccessor;
    private readonly IStorage storage;
    private readonly IAuthorizeRequestValidator authorizeRequestValidator;
    private readonly IConsentService consentService;
    private readonly ILogger logger;

    public DefaultSignInManager(
        IHttpContextAccessor httpContextAccessor,
        IStorage storage,
        IAuthorizeRequestValidator authorizeRequestValidator,
        IConsentService consentService,
        ILogger<DefaultSignInManager> logger)
    {
        this.httpContextAccessor = httpContextAccessor;
        this.storage = storage;
        this.authorizeRequestValidator = authorizeRequestValidator;
        this.consentService = consentService;
        this.logger = logger;
    }

    [Redesign("Disparar exception ou mudar para método Try com out do context e error")]
    public async Task<AuthorizationContext?> GetAuthorizationContextAsync(string? returnUrl, CancellationToken ct)
    {
        if (returnUrl.IsValidReturnUrl())
        {
            logger.LogDebug("returnUrl is valid");

            var parameters = returnUrl.ReadQueryStringAsNameValueCollection();
            if (parameters.TryGet(Constants.AuthorizationParamsStore.MessageStoreIdParameterName, out var messageStoreId))
            {
                parameters = await storage.AuthorizeParameters.ReadAsync(messageStoreId, ct) ?? [];
            }

            var authorizationRequest = new AuthorizationValidationRequest()
            {
                Parameters = parameters
            };
            var authorizationResult = await authorizeRequestValidator.ValidateAsync(authorizationRequest, ct);
            if (authorizationResult.Error is null && authorizationResult.Context is not null)
            {
                logger.LogTrace("AuthorizationRequest being returned");
                return authorizationResult.Context;
            }

            // considerar logar o erro retornado e disparar uma exception.
        }
        else
        {
            logger.LogDebug("returnUrl is not valid");
        }

        logger.LogDebug("No AuthorizationRequest being returned");
        return null;
    }

    /// <inheritdoc />
    public async Task<CredentialsValidationResult> AuthenticateUserAsync(Realm realm, string username, string password,
        CancellationToken ct)
    {
        var accountOptions = realm.Options.Account;
        var userStore = storage.GetUserStore(realm);

        var user = await userStore.GetUserAsync(username, ct);
        if (user is null)
        {
            return new CredentialsValidationResult(NotFound, accountOptions.InvalidCredentialsErrorMessage);
        }

        if (!user.IsActive)
        {
            return new CredentialsValidationResult(Inactive, accountOptions.InactiveUserErrorMessage);
        }

        if (await user.IsBlockedAsync(ct))
        {
            return new CredentialsValidationResult(Blocked, accountOptions.BlockedUserErrorMessage);
        }

        var validationResult = await user.AuthenticateAndStartSessionAsync(password, ct);
        if (!validationResult.IsValid)
        {
            return new CredentialsValidationResult(InvalidCredentials, accountOptions.InvalidCredentialsErrorMessage);
        }

        return new CredentialsValidationResult(user, validationResult.Session);
    }

    public async Task<ClaimsPrincipal> SignInAsync(
        IdentityUser user,
        IdentitySession? session, 
        bool remember,
        CancellationToken ct)
    {
        var httpContext = httpContextAccessor.HttpContext 
            ?? throw new InvalidOperationException("HttpContext is required for SignInAsync");

        var realm = httpContext.GetCurrentRealm();
        var accountOptions = realm.Options.Account;

        // only set explicit expiration here if user chooses "remember me".
        // otherwise we rely upon expiration configured in cookie middle-ware.
        AuthenticationProperties? props = null;
        if (remember && accountOptions.AllowRememberLogin)
        {
            props = new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = DateTimeOffset.UtcNow.Add(accountOptions.RememberMeLoginDuration)
            };
        }

        var principal = await user.CreatePrincipalAsync(session, ct);

        var sid = principal.FindFirst(JwtClaimTypes.SessionId);
        if (sid is null)
        {
            // sid is required, when not present, throw exception
            throw new InvalidOperationException("SessionId claim is required, but it is not present in the principal");
        }

        props ??= new();
        props.Items[JwtClaimTypes.SessionId] = sid.Value;

        var authenticationScheme = httpContext.GetRealmAuthenticationScheme();
        await httpContext.SignInAsync(authenticationScheme, principal, props);

        logger.LogInformation("User logged in: {UserName}, Session id: {SessionId}", user.UserName, sid.Value);

        return principal;
    }

    public async Task<bool> ConsentRequired(ClaimsPrincipal user, Client client, Resources resources, CancellationToken ct)
    {
        return !await consentService.ValidateConsentAsync(user, client, resources, ct);
    }
}