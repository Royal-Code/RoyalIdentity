// Ignore Spelling: Pkce

using Microsoft.Extensions.Logging;
using RoyalIdentity.Extensions;
using RoyalIdentity.Options;
using RoyalIdentity.Pipelines.Abstractions;
using RoyalIdentity.Security.Cryptography;
using RoyalIdentity.Utils;

namespace RoyalIdentity.Contexts.Validators;

/// <summary>
/// Matches the presented <c>code_verifier</c> against the <c>code_challenge</c> stored with the authorization
/// code.
/// </summary>
/// <remarks>
/// The classification follows OAuth 2.1 draft-15 §§3.2.4/4.1.3 and RFC 7636 §4.6, and turns on which question
/// failed. Verifier and challenge disagreeing about <b>presence</b> is a malformed request: either the client
/// sent a verifier for a code that has no challenge, or it omitted the verifier a code with a challenge
/// requires. Only a verifier that was presented and does not <b>match</b> is an invalid grant.
/// </remarks>
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

        // The challenge is server state: stored or not. The verifier is a request parameter, and there
        // "presented" means the key is there — an empty or blank code_verifier was still sent, and reading it
        // as absence would let a code with no challenge be exchanged in silence, which is the very downgrade
        // this check exists to refuse. The preflight already guarantees the parameter appears at most once.
        var hasChallenge = code.CodeChallenge.IsPresent();
        var hasVerifier = context.Raw.GetValues(Oidc.Token.Request.CodeVerifier) is not null;

        if (hasChallenge != hasVerifier)
        {
            // Draft-15 §3.2.4 added the first direction explicitly: a code_verifier sent for a code that was
            // never bound to a challenge used to be accepted in silence, which is the PKCE downgrade.
            logger.LogError(
                context,
                hasVerifier
                    ? "A code_verifier was presented for an authorization code without a code_challenge"
                    : "The authorization code requires a code_verifier and none was presented");

            context.Error(
                Oidc.Token.Errors.InvalidRequest,
                "code_verifier is required if, and only if, the authorization code has a code_challenge");

            return default;
        }

        // No PKCE on either side: nothing to match.
        if (!hasChallenge)
            return default;

        // DF9: a core parameter whose syntax is wrong is a malformed request, and every other one already gets
        // this treatment — LoadCode and LoadRefreshToken both check their length before looking anything up.
        // The code_verifier was the only one comparing first and never checking, which turned "you sent 3
        // characters" into "your grant is invalid". Nothing is revealed by the distinction: both answers are
        // about the caller's own input, and its length and alphabet are things it can see for itself.
        var verifier = context.CodeVerifier ?? string.Empty;
        if (!IsWellFormed(verifier, context.Options.InputLengthRestrictions))
        {
            logger.LogError(context, "The presented code_verifier does not satisfy the syntax of RFC 7636 §4.1");

            context.Error(
                Oidc.Token.Errors.InvalidRequest,
                "code_verifier must be 43 to 128 characters of [A-Za-z0-9-._~]");

            return default;
        }

        bool equals;
        switch (code.CodeChallengeMethod)
        {
            case Oidc.CodeChallenge.Methods.Plain:

                equals = FixedTimeComparer.IsEqualUtf8(
                    PkceHelper.HashCodeChallengeForStorage(context.CodeVerifier),
                    code.CodeChallenge);

                if (!equals)
                    RefuseTheCode(context, "The code_verifier does not match the stored code_challenge");

                break;

            case Oidc.CodeChallenge.Methods.Sha256:

                var transformedCodeVerifier = PkceHelper.GenerateStoredS256CodeChallengeHash(context.CodeVerifier);

                equals = FixedTimeComparer.IsEqualUtf8(
                    transformedCodeVerifier,
                    code.CodeChallenge);

                if (!equals)
                {
                    RefuseTheCode(context, "The code_verifier does not match the stored code_challenge");
                }

                break;

            default:

                // DF18: the client presented an artifact this server cannot honour, and that is a protocol
                // answer, not a 5xx — HTTP 5xx belongs to bugs and outages, never to data a request carried.
                // The method reaching this branch means a corrupted record or a bad seed, so it is worth
                // knowing; it is logged and kept out of the response, whose wording must stay identical to
                // every other refusal so a caller cannot tell a wrong verifier from a server it broke.
                RefuseTheCode(
                    context,
                    $"The stored code_challenge_method '{code.CodeChallengeMethod}' is not supported");

                break;
        }

        return default;
    }

    /// <summary>
    /// Whether the verifier satisfies RFC 7636 §4.1: <c>43*128unreserved</c>, where <c>unreserved</c> is
    /// <c>ALPHA / DIGIT / "-" / "." / "_" / "~"</c>. The bounds come from
    /// <see cref="InputLengthRestrictions"/>, which had declared them since before this validator existed and
    /// had never read them.
    /// </summary>
    private static bool IsWellFormed(string verifier, InputLengthRestrictions restrictions)
    {
        if (verifier.Length < restrictions.CodeVerifierMinLength
            || verifier.Length > restrictions.CodeVerifierMaxLength)
        {
            return false;
        }

        foreach (var character in verifier)
        {
            if (!char.IsAsciiLetterOrDigit(character) && character is not ('-' or '.' or '_' or '~'))
                return false;
        }

        return true;
    }

    /// <summary>
    /// Refuses the presented code with the shared, indistinguishable answer, keeping the reason in the log.
    /// </summary>
    private void RefuseTheCode(AuthorizationCodeContext context, string reason)
    {
        logger.LogError(context, reason);

        context.Error(Oidc.Token.Errors.InvalidGrant, AuthorizationCodeRefusal.Description);
    }
}
