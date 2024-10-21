using Microsoft.Extensions.Logging;
using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Extensions;
using RoyalIdentity.Options;
using RoyalIdentity.Users.Contexts;
using System.Collections.Specialized;

namespace RoyalIdentity.Users.Defaults;

public class DefaultSignInManager : ISignInManager
{
    private readonly ILogger logger;
    private readonly IAuthorizeParametersStore? authorizeParametersStore;
    private readonly IUserSession userSession;

    public async Task<AuthorizationContext> GetAuthorizationContextAsync(string returnUrl, CancellationToken ct)
    {
        if (returnUrl.IsValidReturnUrl())
        {
            logger.LogTrace("returnUrl is valid");

            var parameters = returnUrl.ReadQueryStringAsNameValueCollection();
            if (authorizeParametersStore is not null
                && parameters.TryGet(Constants.AuthorizationParamsStore.MessageStoreIdParameterName, out var messageStoreId))
            {
                parameters = await authorizeParametersStore.ReadAsync(messageStoreId, ct) ?? new();
            }

            var user = await userSession.GetUserAsync();
            var result = await _validator.ValidateAsync(parameters, user);
            if (!result.IsError)
            {
                logger.LogTrace("AuthorizationRequest being returned");
                return new AuthorizationRequest(result.ValidatedRequest);
            }
        }
        else
        {
            logger.LogTrace("returnUrl is not valid");
        }

        logger.LogTrace("No AuthorizationRequest being returned");
        return null;
    }
}
