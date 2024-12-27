using Microsoft.Extensions.Logging;
using RoyalIdentity.Extensions;
using RoyalIdentity.Pipelines.Abstractions;

namespace RoyalIdentity.Contexts.Validators;

public class GrantTypeValidator : IValidator<ITokenEndpointContextBase>
{
    private readonly ILogger logger;

    public GrantTypeValidator(ILogger<GrantTypeValidator> logger)
    {
        this.logger = logger;
    }

    public ValueTask Validate(ITokenEndpointContextBase context, CancellationToken ct)
    {
        context.AssertHasClient();

        if (context.Client.AllowedGrantTypes.Contains(context.GrantType))
        {
            logger.LogError(context, "Client not authorized for code flow");
            context.InvalidGrant("Client not authorized for code flow");
        }

        return default;
    }
}
