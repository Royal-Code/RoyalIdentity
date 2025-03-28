﻿// Ignore Spelling: Pkce

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RoyalIdentity.Contexts.Withs;
using RoyalIdentity.Extensions;
using RoyalIdentity.Options;
using RoyalIdentity.Pipelines.Abstractions;
using static RoyalIdentity.Options.OidcConstants;

namespace RoyalIdentity.Contexts.Validators;

public class PkceValidator : IValidator<IWithCodeChallenge>
{
    private readonly ServerOptions options;
    private readonly ILogger logger;

    public PkceValidator(IOptions<ServerOptions> options, ILogger<PkceValidator> logger)
    {
        this.options = options.Value;
        this.logger = logger;
    }

    public ValueTask Validate(IWithCodeChallenge context, CancellationToken ct)
    {
        context.ClientParameters.AssertHasClient();

        //////////////////////////////////////////////////////////
        // check if PKCE is required and validate parameters
        //////////////////////////////////////////////////////////
        if (!context.ResponseTypes.Contains(ResponseTypes.Code))
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
            if (context.ClientParameters.Client.RequirePkce)
            {
                logger.LogError(
                    context, 
                    "The parameter code_challenge is missing", 
                    context.ResponseTypes.ToSpaceSeparatedString());

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
            logger.LogError(context, "The parameter code_challenge is either too short or too long");
            context.InvalidRequest("Invalid code_challenge", "too long");

            return default;
        }

        var codeChallengeMethod = context.CodeChallengeMethod;
        if (codeChallengeMethod.IsMissing())
        {
            logger.LogDebug("Missing code_challenge_method, defaulting to plain");
            codeChallengeMethod = CodeChallengeMethods.Plain;
        }

        if (!context.Options.Discovery.CodeChallengeMethodIsSupported(codeChallengeMethod))
        {
            logger.LogError(context, "Unsupported code_challenge_method", codeChallengeMethod);
            context.InvalidRequest("Transform algorithm not supported", "unsupported code_challenge_method");
            return default;
        }

        // check if plain method is allowed
        if (codeChallengeMethod == CodeChallengeMethods.Plain && !context.ClientParameters.Client.AllowPlainTextPkce)
        {
            logger.LogError(context, "The parameter code_challenge_method of plain is not allowed", codeChallengeMethod);
            context.InvalidRequest("Transform algorithm not supported", "code_challenge_method of plain is not allowed");
        }

        return default;
    }
}
