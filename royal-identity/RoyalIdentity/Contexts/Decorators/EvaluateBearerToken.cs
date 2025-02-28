using Microsoft.Extensions.Logging;
using RoyalIdentity.Contexts.Withs;
using RoyalIdentity.Options;
using RoyalIdentity.Pipelines.Abstractions;
using RoyalIdentity.Extensions;
using RoyalIdentity.Contracts;
using RoyalIdentity.Contracts.Models;
using RoyalIdentity.Contexts.Items;
using Microsoft.Extensions.Options;

namespace RoyalIdentity.Contexts.Decorators;

public class EvaluateBearerToken : IDecorator<IWithBearerToken>
{
    private readonly ITokenValidator tokenValidator;
    private readonly ServerOptions options;
    private readonly ILogger logger;

    public EvaluateBearerToken(
        ITokenValidator tokenValidator, 
        IOptions<ServerOptions> options,
        ILogger<EvaluateBearerToken> logger)
    {
        this.tokenValidator = tokenValidator;
        this.options = options.Value;
        this.logger = logger;
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
