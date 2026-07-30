using System.IdentityModel.Tokens.Jwt;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using RoyalIdentity.Contexts;
using RoyalIdentity.Contracts.Models;
using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Extensions;

namespace RoyalIdentity.Contracts.Defaults.SecretsEvaluators;

public class PrivateKeyJwtSecretEvaluator : SecretEvaluatorBase
{
    private static readonly EvaluatedCredential PrivateKeyJwtInvalidCredentials =
        new(Server.ParsedSecretTypes.JwtBearer, false);

    /// <summary>
    /// Clock skew tolerated when validating the assertion, and therefore the margin the replay record must
    /// outlive the assertion by: a record expiring before the assertion does would reopen the replay window.
    /// </summary>
    private static readonly TimeSpan ClockSkew = TimeSpan.FromMinutes(5);

    private readonly IReplayProtectionStore replayProtection;

    public PrivateKeyJwtSecretEvaluator(
        IStorage storage,
        TimeProvider clock,
        IReplayProtectionStore replayProtection,
        ILogger<PrivateKeyJwtSecretEvaluator> logger) : base(storage, clock, logger)
    {
        this.replayProtection = replayProtection;
    }

    protected override EvaluatedCredential InvalidCredentials => PrivateKeyJwtInvalidCredentials;

    public override string AuthenticationMethod => Oidc.Endpoint.AuthMethods.PrivateKeyJwt;

    public override async Task<EvaluatedClient?> EvaluateAsync(IEndpointContextBase context, CancellationToken ct)
    {
        logger.LogDebug("Start parsing and evaluate PrivateKeyJwt Authentication secret");

        var hasAssertion = context.Raw.TryGet(Oidc.Token.Request.ClientAssertion, out var assertion);
        var hasAssertionType =
            context.Raw.TryGet(Oidc.Token.Request.ClientAssertionType, out var assertionType);

        if (!hasAssertion || !hasAssertionType || assertionType != Oidc.ClientAssertionTypes.JwtBearer)
        {
            logger.LogDebug("Client assertion or assertion type not found in post body");
            return null;
        }

        var restrictions = context.Options.InputLengthRestrictions;

        if (assertion!.Length > restrictions.Jwt)
        {
            logger.LogError("Client assertion token exceeds maximum length.");
            return null;
        }

        string clientId;
        try
        {
            var jwt = new JwtSecurityToken(assertion);
            clientId = jwt.Subject;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not parse client assertion");
            return null;
        }

        if (!clientId.IsPresent())
        {
            return null;
        }

        if (clientId.Length > restrictions.ClientId)
        {
            logger.LogError("Client ID exceeds maximum length.");
            return null;
        }

        // load client
        var clientStore = storage.GetClientStore(context.Realm);
        var client = await clientStore.FindEnabledClientByIdAsync(clientId, ct);
        if (client is null)
        {
            logger.LogError(context, $"No client with id '{clientId}' found. aborting client evaluation");

            return null;
        }

        List<SecurityKey> trustedKeys;
        try
        {
            trustedKeys = await client.ClientSecrets.GetKeysAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(context, ex, "Could not parse secrets");
            return new EvaluatedClient(client, InvalidCredentials, AuthenticationMethod);
        }

        if (trustedKeys.Count is 0)
        {
            logger.LogError(context, "There are no keys available to validate client assertion.");
            return new EvaluatedClient(client, InvalidCredentials, AuthenticationMethod);
        }

        var validAudiences = new[]
        {
            // token endpoint URL
            string.Concat(
                context.HttpContext.GetServerIssuerUri(context.Options, false).EnsureTrailingSlash(),
                Oidc.Routes.BuildTokenUrl(context.Realm.Path))
        };

        var tokenValidationParameters = new TokenValidationParameters
        {
            IssuerSigningKeys = trustedKeys,
            ValidateIssuerSigningKey = true,

            ValidIssuer = clientId,
            ValidateIssuer = true,

            ValidAudiences = validAudiences,
            ValidateAudience = true,

            RequireSignedTokens = true,
            RequireExpirationTime = true,

            ClockSkew = ClockSkew,

            // The injected clock is the single temporal authority: lifetime validation, the ceiling below and
            // the retention of the replay record must all agree, or a test with a controlled clock proves
            // nothing about the real flow. TokenValidationParameters.TimeProvider is not public in the
            // Microsoft.IdentityModel version in use, so the delegate carries the clock instead — it replaces
            // the default lifetime validation, keeping the same skew and the same refusals.
            LifetimeValidator = (notBefore, expires, _, _) => IsWithinLifetime(notBefore, expires)
        };

        JwtSecurityToken jwtToken;
        try
        {
            var handler = new JwtSecurityTokenHandler();
            handler.ValidateToken(assertion, tokenValidationParameters, out var token);
            jwtToken = (JwtSecurityToken)token;
        }
        catch (Exception e)
        {
            // Only token validation is caught here. Everything after it — including the replay store — must be
            // free to fail loudly: an infrastructure failure reported as invalid_client would be a security
            // control silently degrading into a credential error.
            logger.LogError(e, "JWT token validation error");
            return new EvaluatedClient(client, InvalidCredentials, AuthenticationMethod);
        }

        if (jwtToken.Subject != jwtToken.Issuer)
        {
            logger.LogError(context, "Both 'sub' and 'iss' in the client assertion token must have a value of client_id.");
            return new EvaluatedClient(client, InvalidCredentials, AuthenticationMethod);
        }

        var exp = jwtToken.Payload.Expiration;
        if (!exp.HasValue)
        {
            logger.LogError(context, "exp is missing.");
            return new EvaluatedClient(client, InvalidCredentials, AuthenticationMethod);
        }

        var maxLifetime = context.Options.Authentication.ClientAssertionMaxLifetime;

        // Nothing validates RealmOptions when the configuration snapshot is published, so the persisted value
        // reaches here unchecked. Refusing out of range is the only fail-closed answer: honouring a value above
        // the maximum would contradict the option's own contract, and the arithmetic below overflows outright on
        // a negative one. A configuration error must never become an accepted credential.
        if (maxLifetime < Server.MinClientAssertionMaxLifetime
            || maxLifetime > Server.MaxClientAssertionMaxLifetime)
        {
            logger.LogError(
                context,
                "Authentication.ClientAssertionMaxLifetime is outside the accepted range for this realm, so no " +
                "client assertion can be accepted until it is corrected.");
            return new EvaluatedClient(client, InvalidCredentials, AuthenticationMethod);
        }

        var now = clock.GetUtcNow();
        var latestAcceptedExpiration = now < DateTimeOffset.MaxValue - maxLifetime
            ? now + maxLifetime
            : DateTimeOffset.MaxValue;

        // Compared in unix seconds so an absurd exp is refused before it can overflow the conversion below.
        if (exp.Value > latestAcceptedExpiration.ToUnixTimeSeconds())
        {
            logger.LogError(
                context,
                "The client assertion expires beyond the maximum lifetime accepted by this realm.");
            return new EvaluatedClient(client, InvalidCredentials, AuthenticationMethod);
        }

        var jti = jwtToken.Payload.Jti;
        if (jti.IsMissing())
        {
            logger.LogError(context, "jti is missing.");
            return new EvaluatedClient(client, InvalidCredentials, AuthenticationMethod);
        }

        // One atomic operation: a check followed by a write would let two concurrent presentations of the same
        // assertion both pass. The record outlives the assertion by the same skew the validation tolerated.
        var registered = await replayProtection.TryAddAsync(
            context.Realm.Id,
            clientId,
            nameof(PrivateKeyJwtSecretEvaluator),
            jti,
            DateTimeOffset.FromUnixTimeSeconds(exp.Value) + ClockSkew,
            ct);

        if (!registered)
        {
            logger.LogError(context, "The client assertion identifier was already used. Possible replay attack.");
            return new EvaluatedClient(client, InvalidCredentials, AuthenticationMethod);
        }

        return new EvaluatedClient(client, new EvaluatedCredential(Server.ParsedSecretTypes.JwtBearer, true), AuthenticationMethod);
    }

    /// <summary>
    /// Replicates the default lifetime validation over the injected clock, refusal for refusal: <c>exp</c> is
    /// required, the window itself must be coherent (<c>nbf &lt;= exp</c>, which the default validation rejects
    /// independently of the current instant), and both bounds are compared with the same
    /// <see cref="ClockSkew"/> the parameters declare.
    /// </summary>
    private bool IsWithinLifetime(DateTime? notBefore, DateTime? expires)
    {
        if (!expires.HasValue)
            return false;

        // A token valid "from later than it expires" describes no window at all. Each bound can still pass on
        // its own — expiring now while starting four minutes from now clears both skew comparisons — so this has
        // to be checked before them, not derived from them.
        if (notBefore.HasValue && notBefore.Value > expires.Value)
            return false;

        var now = clock.GetUtcNow().UtcDateTime;

        if (notBefore.HasValue && notBefore.Value > now.Add(ClockSkew))
            return false;

        return expires.Value >= now.Subtract(ClockSkew);
    }
}
