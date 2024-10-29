using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RoyalIdentity.Contracts;
using RoyalIdentity.Contracts.Models;
using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Extensions;
using RoyalIdentity.Options;
using RoyalIdentity.Users.Contexts;
using RoyalIdentity.Users.Contracts;
using static RoyalIdentity.Users.CredentialsValidationResult.WellKnownReasons;

namespace RoyalIdentity.Users.Defaults;

public class DefaultSignInManager : ISignInManager
{
    private readonly IHttpContextAccessor httpContextAccessor;
    private readonly IAuthorizeParametersStore? authorizeParametersStore;
    private readonly IAuthorizeRequestValidator authorizeRequestValidator;
    private readonly IUserStore userStore;
    private readonly AccountOptions accountOptions;
    private readonly ILogger logger;

    public DefaultSignInManager(
        IHttpContextAccessor httpContextAccessor,
        IAuthorizeRequestValidator authorizeRequestValidator,
        IUserStore userStore,
        IOptions<AccountOptions> accountOptions,
        ILogger<DefaultSignInManager> logger,
        IAuthorizeParametersStore? authorizeParametersStore = null)
    {
        this.httpContextAccessor = httpContextAccessor;
        this.authorizeRequestValidator = authorizeRequestValidator;
        this.userStore = userStore;
        this.accountOptions = accountOptions.Value;
        this.logger = logger;
        this.authorizeParametersStore = authorizeParametersStore;
    }

    [Redesign("Disparar exception ou mudar para método Try com out do context e error")]
    public async Task<AuthorizationContext?> GetAuthorizationContextAsync(string? returnUrl, CancellationToken ct)
    {
        if (returnUrl.IsValidReturnUrl())
        {
            logger.LogDebug("returnUrl is valid");

            var parameters = returnUrl.ReadQueryStringAsNameValueCollection();
            if (authorizeParametersStore is not null
                && parameters.TryGet(Constants.AuthorizationParamsStore.MessageStoreIdParameterName,
                    out var messageStoreId))
            {
                parameters = await authorizeParametersStore.ReadAsync(messageStoreId, ct) ?? [];
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

    public async Task<CredentialsValidationResult> ValidateCredentialsAsync(string username, string password,
        CancellationToken ct)
    {
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

        if (!await user.ValidateCredentialsAsync(password, ct))
        {
            return new CredentialsValidationResult(InvalidCredentials, accountOptions.InvalidCredentialsErrorMessage);
        }

        return new CredentialsValidationResult(user);
    }

    public async Task SignInAsync(IdentityUser user, bool inputRememberLogin, CancellationToken ct)
    {
        var httpContext = httpContextAccessor.HttpContext;
        if (httpContext is null)
            throw new InvalidOperationException("HttpContext is required for SignInAsync");

        // only set explicit expiration here if user chooses "remember me".
        // otherwise we rely upon expiration configured in cookie middleware.
        AuthenticationProperties? props = null;
        if (inputRememberLogin && accountOptions.AllowRememberLogin)
        {
            props = new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = DateTimeOffset.UtcNow.Add(accountOptions.RememberMeLoginDuration)
            };
        }

        var principal = await user.CreatePrincipalAsync(ct);

        var sid = principal.FindFirst(JwtClaimTypes.SessionId);
        if (sid is null)
        {
            // sid is required, when not present, throw exception
            throw new InvalidOperationException("SessionId claim is required, but it is not present in the principal");
        }

        props ??= new();
        props.Items[JwtClaimTypes.SessionId] = sid.Value;

        await httpContext.SignInAsync(principal, props);

        logger.LogInformation("User logged in: {Username}, Session id: {SessionId}", user.UserName, sid.Value);
    }
}