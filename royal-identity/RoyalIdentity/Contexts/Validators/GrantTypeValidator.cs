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
        context.ClientParameters.AssertHasClient();

        if (!context.ClientParameters.Client.AllowedGrantTypes.Contains(context.GrantType))
        {
            logger.LogError(context, "Client not authorized for flow", context.GrantType);

            // RFC 6749 §5.2: the client authenticated fine and the grant is one the server implements — what
            // fails is the client's authorization to use it, which is unauthorized_client. invalid_grant is
            // about the credential presented, not about who may present it.
            context.Error(
                Oidc.Token.Errors.UnauthorizedClient,
                $"Client not authorized for {context.GrantType} flow");
        }

        return default;
    }
}
