using Microsoft.Extensions.Logging;
using RoyalIdentity.Extensions;
using RoyalIdentity.Pipelines.Abstractions;
using static RoyalIdentity.Options.OidcConstants;

namespace RoyalIdentity.Contexts.Validators;

public class ValidateGrantType : IValidator<ITokenEndpointContextBase>
{
    private readonly ILogger logger;

    public ValueTask Validate(ITokenEndpointContextBase context, CancellationToken ct)
    {
        context.AssertHasClient();

        if (context.Client.AllowedGrantTypes.Contains(context.GrantType))
        {
            logger.LogError(context, "Client not authorized for code flow");
            context.Error(TokenErrors.InvalidGrant, "Client not authorized for code flow");
        }

        return default;
    }
}
