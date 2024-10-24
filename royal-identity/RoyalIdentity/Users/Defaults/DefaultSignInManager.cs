using Microsoft.Extensions.Logging;
using RoyalIdentity.Contracts;
using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Extensions;
using RoyalIdentity.Options;
using RoyalIdentity.Users.Contexts;
using RoyalIdentity.Users.Contracts;

namespace RoyalIdentity.Users.Defaults;

public class DefaultSignInManager : ISignInManager
{
    private readonly IAuthorizeParametersStore? authorizeParametersStore;
    private readonly IAuthorizeRequestValidator authorizeRequestValidator;
    private readonly IUserStore userStore;
    private readonly ILogger logger;

    public DefaultSignInManager(
        IAuthorizeRequestValidator authorizeRequestValidator,
        IUserStore userStore,
        ILogger<DefaultSignInManager> logger,
        IAuthorizeParametersStore? authorizeParametersStore = null)
    {
        this.authorizeRequestValidator = authorizeRequestValidator;
        this.userStore = userStore;
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
                && parameters.TryGet(Constants.AuthorizationParamsStore.MessageStoreIdParameterName, out var messageStoreId))
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
}
