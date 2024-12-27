using Microsoft.Extensions.Logging;
using RoyalIdentity.Extensions;
using RoyalIdentity.Options;
using RoyalIdentity.Pipelines.Abstractions;
using RoyalIdentity.Utils;

namespace RoyalIdentity.Contexts.Validators;

public class PkceMatchValidator : IValidator<AuthorizationCodeContext>
{
    private readonly ILogger logger;

    public PkceMatchValidator(ILogger<PkceMatchValidator> logger)
    {
        this.logger = logger;
    }

    public ValueTask Validate(AuthorizationCodeContext context, CancellationToken ct)
    {
        context.AssertHasCode();

        if (context.AuthorizationCode.CodeChallenge.IsMissing())
            return default;

        if (context.CodeVerifier.IsMissing())
        {
            logger.LogError(context, "Client is missing code challenge or code challenge method");
            context.InvalidGrant("Code verifier required");
            return default;
        }

        bool equals;
        switch (context.AuthorizationCode.CodeChallengeMethod)
        {
            case OidcConstants.CodeChallengeMethods.Plain:

                equals = TimeConstantComparer.IsEqual(
                    context.CodeVerifier.Sha256(),
                    context.AuthorizationCode.CodeChallenge);

                if (!equals)
                    context.InvalidGrant("Code verifier does not match code challenge");

                break;

            case OidcConstants.CodeChallengeMethods.Sha256:

                var transformedCodeVerifier = PkceHelper.GenerateCodeChallengeS256(context.CodeVerifier);

                equals = TimeConstantComparer.IsEqual(
                    transformedCodeVerifier, 
                    context.AuthorizationCode.CodeChallenge);

                if (!equals)
                {
                    context.InvalidGrant("Code verifier does not match code challenge");
                }

                break;

            default:
                context.InvalidGrant("Code challenge method is not supported");
                break;
        }

        return default;
    }
}