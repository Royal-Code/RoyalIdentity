using Microsoft.Extensions.Logging;
using RoyalIdentity.Contexts;
using RoyalIdentity.Contracts;
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
    private readonly IAuthorizeRequestValidator _validator;

    public async Task<AuthorizationContext?> GetAuthorizationContextAsync(string returnUrl, CancellationToken ct)
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
            var validationContext = new AuthorizeValidationContext()
            {
                Parameters = parameters,
                Subject = user
            };
            await _validator.ValidateAsync(validationContext, ct);
            if (validationContext.Error is null && validationContext.Context is not null)
            {
                logger.LogTrace("AuthorizationRequest being returned");
                return validationContext.Context;
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
