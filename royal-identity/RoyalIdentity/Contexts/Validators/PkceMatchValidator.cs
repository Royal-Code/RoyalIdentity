// Ignore Spelling: Pkce

using Microsoft.Extensions.Logging;
using RoyalIdentity.Extensions;
using RoyalIdentity.Options;
using RoyalIdentity.Pipelines.Abstractions;
using FixedTimeComparer = RoyalIdentity.Security.Cryptography.FixedTimeComparer;
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
        context.CodeParameters.AssertHasCode();
        var code = context.CodeParameters.AuthorizationCode;

        if (code.CodeChallenge.IsMissing())
            return default;

        if (context.CodeVerifier.IsMissing())
        {
            logger.LogError(context, "Client is missing code challenge or code challenge method");
            context.InvalidGrant("Code verifier required");
            return default;
        }

        bool equals;
        switch (code.CodeChallengeMethod)
        {
            case Oidc.CodeChallenge.Methods.Plain:

                equals = FixedTimeComparer.IsEqualUtf8(
                    context.CodeVerifier.Sha256(),
                    code.CodeChallenge);

                if (!equals)
                    context.InvalidGrant("Code verifier does not match code challenge");

                break;

            case Oidc.CodeChallenge.Methods.Sha256:

                var transformedCodeVerifier = PkceHelper.GenerateCodeChallengeS256(context.CodeVerifier);

                equals = FixedTimeComparer.IsEqualUtf8(
                    transformedCodeVerifier, 
                    code.CodeChallenge);

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