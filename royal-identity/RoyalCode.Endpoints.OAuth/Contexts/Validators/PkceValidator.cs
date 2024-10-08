﻿using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RoyalIdentity.Extensions;
using RoyalIdentity.Options;
using RoyalIdentity.Pipelines.Abstractions;
using static RoyalIdentity.Options.OidcConstants;

namespace RoyalIdentity.Contexts.Validators;

public class PkceValidator : IValidator<AuthorizeContext>
{
    private readonly ServerOptions options;
    private readonly ILogger logger;

    public PkceValidator(IOptions<ServerOptions> options, ILogger<PkceValidator> logger)
    {
        this.options = options.Value;
        this.logger = logger;
    }

    public ValueTask Validate(AuthorizeContext context, CancellationToken cancellationToken)
    {
        context.AssertHasClient();
        context.AssertHasGrantType();

        //////////////////////////////////////////////////////////
        // check if PKCE is required and validate parameters
        //////////////////////////////////////////////////////////
        if (context.GrantType != GrantType.AuthorizationCode && context.GrantType != GrantType.Hybrid)
        {
            return default;
        }

        logger.LogDebug("Checking for PKCE parameters");

        /////////////////////////////////////////////////////////////////////////////
        // validate code_challenge and code_challenge_method
        /////////////////////////////////////////////////////////////////////////////

        var codeChallenge = context.CodeChallenge;
        if (codeChallenge.IsMissing())
        {
            if (context.Client.RequirePkce)
            {
                logger.LogError(options, "The parameter code_challenge is missing", context.ResponseType, context);
                context.InvalidRequest("Code challenge required");
            }
            else
            {
                logger.LogDebug("No PKCE used, neither is it required");
            }

            return default;
        }

        if (codeChallenge.Length < options.InputLengthRestrictions.CodeChallengeMinLength ||
            codeChallenge.Length > options.InputLengthRestrictions.CodeChallengeMaxLength)
        {
            logger.LogError(options, "The parameter code_challenge is either too short or too long", context);
            context.InvalidRequest("Invalid code_challenge", "too long");

            return default;
        }

        var codeChallengeMethod = context.CodeChallengeMethod;
        if (codeChallengeMethod.IsMissing())
        {
            logger.LogDebug("Missing code_challenge_method, defaulting to plain");
            codeChallengeMethod = CodeChallengeMethods.Plain;
        }

        if (!Constants.SupportedCodeChallengeMethods.Contains(codeChallengeMethod))
        {
            logger.LogError(options, "Unsupported code_challenge_method", codeChallengeMethod, context);
            context.InvalidRequest("Transform algorithm not supported", "unsupported code_challenge_method");
            return default;
        }

        // check if plain method is allowed
        if (codeChallengeMethod == CodeChallengeMethods.Plain)
        {
            if (!context.Client.AllowPlainTextPkce)
            {
                logger.LogError(options, "The parameter code_challenge_method of plain is not allowed", codeChallengeMethod, context);
                context.InvalidRequest("Transform algorithm not supported", "code_challenge_method of plain is not allowed");
            }
        }

        return default;
    }
}
