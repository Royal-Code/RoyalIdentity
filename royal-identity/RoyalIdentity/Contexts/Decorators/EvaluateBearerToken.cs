using Microsoft.Extensions.Logging;
using RoyalIdentity.Contexts.Withs;
using RoyalIdentity.Options;
using RoyalIdentity.Pipelines.Abstractions;
using RoyalIdentity.Extensions;
using RoyalIdentity.Contracts;
using RoyalIdentity.Contracts.Models;
using Microsoft.Extensions.Options;
using RoyalIdentity.Contracts.Storage;

namespace RoyalIdentity.Contexts.Decorators;

public class EvaluateBearerToken : IDecorator<IWithBearerToken>
{
    private readonly ITokenValidator tokenValidator;
    private readonly ILogger logger;
    private readonly ServerOptions options;

    public EvaluateBearerToken(
        ITokenValidator tokenValidator, 
        IStorage storage,
        ILogger<EvaluateBearerToken> logger)
    {
        this.tokenValidator = tokenValidator;
        this.logger = logger;

        options = storage.ServerOptions;
    }

    public async Task Decorate(IWithBearerToken context, Func<Task> next, CancellationToken ct)
    {
        logger.LogDebug("Start bearer token evaluation");

        var token = context.Token;
        TokenEvaluationResult evaluationResult;

        if (token.Contains('.'))
        {
            if (token.Length > options.InputLengthRestrictions.Jwt)
            {
                logger.LogError("JWT too long");
                context.InvalidClient("Token too long");
                return;
            }

            evaluationResult = await tokenValidator.ValidateJwtAccessTokenAsync(context.Realm, token, null, null, ct);
        }
        else
        {
            if (token.Length > options.InputLengthRestrictions.TokenHandle)
            {
                logger.LogError("token handle too long");
                context.InvalidClient("Token too long");
                return;
            }

            evaluationResult = await tokenValidator.ValidateReferenceAccessTokenAsync(context.Realm, token, ct);
        }

        if (evaluationResult.HasError)
        {
            context.Error(evaluationResult.Error);
            return;
        }

        context.BearerParameters.EvaluatedToken = evaluationResult.Token;

        await next();
    }
}
