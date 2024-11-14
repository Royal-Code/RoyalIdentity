using Microsoft.Extensions.Logging;
using RoyalIdentity.Extensions;
using RoyalIdentity.Options;
using RoyalIdentity.Pipelines.Abstractions;

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
            context.Error(OidcConstants.TokenErrors.InvalidGrant, "Code verifier required");
            return default;
        }

        switch (context.AuthorizationCode.CodeChallengeMethod)
        {
            case OidcConstants.CodeChallengeMethods.Plain:
            {
                if (context.AuthorizationCode.CodeChallenge != context.CodeVerifier)
                    context.Error(OidcConstants.TokenErrors.InvalidGrant, "Code verifier does not match code challenge");

                break;
            }
            case OidcConstants.CodeChallengeMethods.Sha256:
            {
                var hash = context.CodeVerifier.Sha256();

                if (context.AuthorizationCode.CodeChallenge != hash)
                {
                    context.Error(OidcConstants.TokenErrors.InvalidGrant, "Code verifier does not match code challenge");
                }

                break;
            }
            default:
                context.Error(OidcConstants.TokenErrors.InvalidGrant, "Code challenge method is not supported");
                break;
        }

        return default;
    }
}