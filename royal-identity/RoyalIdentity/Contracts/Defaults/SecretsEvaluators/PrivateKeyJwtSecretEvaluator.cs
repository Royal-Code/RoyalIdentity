using System.IdentityModel.Tokens.Jwt;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using RoyalIdentity.Contexts;
using RoyalIdentity.Contracts.Models;
using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Extensions;
using RoyalIdentity.Options;

namespace RoyalIdentity.Contracts.Defaults.SecretsEvaluators;

public class PrivateKeyJwtSecretEvaluator : SecretEvaluatorBase
{
    private static readonly EvaluatedCredential PrivateKeyJwtInvalidCredentials =
        new(ServerConstants.ParsedSecretTypes.JwtBearer, false);

    private readonly IReplayCache replayCache;

    public PrivateKeyJwtSecretEvaluator(
        IStorage storage,
        TimeProvider clock,
        IReplayCache replayCache,
        ILogger<PrivateKeyJwtSecretEvaluator> logger) : base(storage, clock, logger)
    {
        this.replayCache = replayCache;
    }

    protected override EvaluatedCredential InvalidCredentials => PrivateKeyJwtInvalidCredentials;

    public override string AuthenticationMethod => OidcConstants.EndpointAuthenticationMethods.PrivateKeyJwt;

    public override async Task<EvaluatedClient?> EvaluateAsync(IEndpointContextBase context, CancellationToken ct)
    {
        logger.LogDebug("Start parsing and evaluate PrivateKeyJwt Authentication secret");

        var hasAssertion = context.Raw.TryGet(OidcConstants.TokenRequest.ClientAssertion, out var assertion);
        var hasAssertionType =
            context.Raw.TryGet(OidcConstants.TokenRequest.ClientAssertionType, out var assertionType);

        if (!hasAssertion || !hasAssertionType || assertionType != OidcConstants.ClientAssertionTypes.JwtBearer)
        {
            logger.LogDebug("Client assertion or assertion type not found in post body");
            return null;
        }

        if (assertion!.Length > options.InputLengthRestrictions.Jwt)
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

        if (clientId.Length > options.InputLengthRestrictions.ClientId)
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

            ClockSkew = TimeSpan.FromMinutes(5)
        };

        try
        {
            var handler = new JwtSecurityTokenHandler();
            handler.ValidateToken(assertion, tokenValidationParameters, out var token);

            var jwtToken = (JwtSecurityToken)token;
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

            var jti = jwtToken.Payload.Jti;
            if (jti.IsMissing())
            {
                logger.LogError(context, "jti is missing.");
                return new EvaluatedClient(client, InvalidCredentials, AuthenticationMethod);
            }

            if (await replayCache.ExistsAsync(nameof(PrivateKeyJwtSecretEvaluator), jti))
            {
                logger.LogError(context, "jti is found in replay cache. Possible replay attack.");
                return new EvaluatedClient(client, InvalidCredentials, AuthenticationMethod);
            }

            await replayCache.AddAsync(nameof(PrivateKeyJwtSecretEvaluator), jti, DateTimeOffset.FromUnixTimeSeconds(exp.Value).AddMinutes(5));
        }
        catch (Exception e)
        {
            logger.LogError(e, "JWT token validation error");
            return new EvaluatedClient(client, InvalidCredentials, AuthenticationMethod);
        }

        return new EvaluatedClient(client, new EvaluatedCredential(ServerConstants.ParsedSecretTypes.JwtBearer, true), AuthenticationMethod);
    }
}