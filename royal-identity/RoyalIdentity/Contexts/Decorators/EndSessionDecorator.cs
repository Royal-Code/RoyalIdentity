using Microsoft.Extensions.Logging;
using RoyalIdentity.Contracts;
using RoyalIdentity.Extensions;
using RoyalIdentity.Pipelines.Abstractions;

namespace RoyalIdentity.Contexts.Decorators;

public class EndSessionDecorator : IDecorator<EndSessionContext>
{
    private readonly ITokenValidator tokenValidator;
    private readonly ILogger logger;

    public EndSessionDecorator(ITokenValidator tokenValidator, ILogger<EndSessionDecorator> logger)
    {
        this.tokenValidator = tokenValidator;
        this.logger = logger;
    }

    public async Task Decorate(EndSessionContext context, Func<Task> next, CancellationToken ct)
    {
        // load IdToken if id_token_hint is present.
        if (context.IdTokenHint.IsPresent())
        {
            var result = await tokenValidator.ValidateIdentityTokenAsync(context.IdTokenHint, context.ClientId, false, ct);
            
            if (result.HasError)
            {
                logger.LogError("Error validating id_token_hint: {Error}", result.Error.Error);
                context.Error(result.Error.Error, result.Error.ErrorDescription ?? "Error validating id_token_hint.");
                return;
            }

            context.IdToken = result.Token;

            if (context.ClientId.IsMissing())
                context.SetClient(result.Token.Client);
        }
        else if (!context.IsAuthenticated)
        {
            // when IdToken is not informed and user is not authenticated
            // then the request is invalid

            context.InvalidRequest("IdTokenHint is missing.");
            return;
        }

        await next();
    }
}
