using Microsoft.Extensions.Logging;
using RoyalIdentity.Contracts;
using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Extensions;
using RoyalIdentity.Options;
using RoyalIdentity.Users.Contexts;

namespace RoyalIdentity.Users.Defaults;

public class DefaultSignInManager : ISignInManager
{
    private readonly ILogger logger;
    private readonly IAuthorizeParametersStore? authorizeParametersStore;
    private readonly IUserSession userSession;
    private readonly IAuthorizeRequestValidator authorizeRequestValidator;

    [Redesign("Disparar exception ou mudar para método Try com out do context e error")]
    public async Task<AuthorizationContext?> GetAuthorizationContextAsync(string returnUrl, CancellationToken ct)
    {
        if (returnUrl.IsValidReturnUrl())
        {
            logger.LogTrace("returnUrl is valid");

            var parameters = returnUrl.ReadQueryStringAsNameValueCollection();
            if (authorizeParametersStore is not null
                && parameters.TryGet(Constants.AuthorizationParamsStore.MessageStoreIdParameterName, out var messageStoreId))
            {
                parameters = await authorizeParametersStore.ReadAsync(messageStoreId, ct) ?? [];
            }

            var user = await userSession.GetUserAsync();
            var authorizationRequest = new AuthorizationValidationRequest()
            {
                Parameters = parameters,
                Subject = user
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
            logger.LogTrace("returnUrl is not valid");
        }

        logger.LogTrace("No AuthorizationRequest being returned");
        return null;
    }
}
