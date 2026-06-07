using Microsoft.Extensions.Logging;
using RoyalIdentity.Contexts.Withs;
using RoyalIdentity.Pipelines.Abstractions;
using RoyalIdentity.Extensions;
using RoyalIdentity.Contracts;
using RoyalIdentity.Contracts.Models;

namespace RoyalIdentity.Contexts.Decorators;

public class EvaluateBearerToken : IDecorator<IWithBearerToken>
{
    private readonly ITokenValidator tokenValidator;
    private readonly ILogger logger;

    public EvaluateBearerToken(
        ITokenValidator tokenValidator, 
        ILogger<EvaluateBearerToken> logger)
    {
        this.tokenValidator = tokenValidator;
        this.logger = logger;
    }

    public async Task Decorate(IWithBearerToken context, Func<Task> next, CancellationToken ct)
    {
        logger.LogDebug("Start bearer token evaluation");

        var token = context.Token;
        TokenEvaluationResult evaluationResult;
        var restrictions = context.Options.InputLengthRestrictions;

        if (token.Contains('.'))
        {
            if (token.Length > restrictions.Jwt)
            {
                logger.LogError("JWT too long");
                context.InvalidClient("Token too long");
                return;
            }

            evaluationResult = await tokenValidator.ValidateJwtAccessTokenAsync(context.Realm, token, null, null, ct);
        }
        else
        {
            if (token.Length > restrictions.TokenHandle)
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

        context.BearerParameters.SetToken(evaluationResult.Token);

        await next();
    }
}
