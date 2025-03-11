using Microsoft.Extensions.Logging;
using RoyalIdentity.Extensions;
using RoyalIdentity.Options;
using RoyalIdentity.Pipelines.Abstractions;

namespace RoyalIdentity.Contexts.Validators;

public class RevocationValidator : IValidator<RevocationContext>
{
    private readonly ILogger logger;

    public RevocationValidator(ILogger<RevocationValidator> logger)
    {
        this.logger = logger;
    }

    public ValueTask Validate(RevocationContext context, CancellationToken ct)
    {
        ////////////////////////////
        // make sure token is present
        ///////////////////////////
        var token = context.Token;
        if (token.IsMissing())
        {
            logger.LogError("No token found in request");
            context.InvalidRequest("No token found");
            return default;
        }

        ////////////////////////////
        // check token type hint
        ///////////////////////////
        var hint = context.TokenTypeHint;
        if (hint.IsPresent() && !context.Options.Discovery.TokenTypeHintIsSupported(hint))
        {
            logger.LogError("Invalid token type hint: {TokenTypeHint}", hint);
            context.InvalidRequest("Invalid token type hint");
        }

        return default;
    }
}