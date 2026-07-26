// Ignore Spelling: jti

using Microsoft.AspNetCore.Http;
using RoyalIdentity.Extensions;
using System.Security.Claims;
using Microsoft.Extensions.Logging;
using RoyalIdentity.Models;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;
using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Contracts.Models;
using RoyalIdentity.Models.Tokens;

namespace RoyalIdentity.Contracts.Defaults;

public class DefaultTokenValidator : ITokenValidator
{
    private readonly IKeyManager keys;
    private readonly IStorage storage;
    private readonly IHttpContextAccessor httpContextAccessor;
    private readonly ILogger logger;
    private readonly TimeProvider clock;

    public DefaultTokenValidator(
        IKeyManager keys,
        IStorage storage,
        IHttpContextAccessor httpContextAccessor,
        ILogger<DefaultTokenValidator> logger,
        TimeProvider clock)
    {
        this.keys = keys;
        this.storage = storage;
        this.httpContextAccessor = httpContextAccessor;
        this.logger = logger;
        this.clock = clock;
    }

    /// <summary>
    /// The issuer a token of this realm must carry, computed the same way issuance computes it. Reading
    /// <c>options.IssuerUri</c> alone only worked while a backing handed out one shared options instance that
    /// an earlier request had populated; with a real store the field is null and every token would be rejected
    /// (plan-data-operational-storage Fase 8).
    /// </summary>
    private string? ExpectedIssuer(Realm realm)
        => realm.Options.IssuerUri.IsPresent()
            ? realm.Options.IssuerUri
            : httpContextAccessor.HttpContext?.GetServerIssuerUri(realm.Options);

    public async Task<TokenEvaluationResult> ValidateJwtAccessTokenAsync(
        Realm realm, string jwt, string? expectedScope = null, string? audience = null, CancellationToken ct = default)
    {

        var (principal, securityToken, error) = await TryGetPrincipal(realm, jwt, audience, true, ct);

        if (error is not null)
        {
            return new(error);
        }

        // if no audience is specified, we make at least sure that it is an access token
        var expectedAccessTokenJwtType = realm.Options.AccessTokenJwtType;
        if (audience.IsMissing() &&
            expectedAccessTokenJwtType.IsPresent() &&
            securityToken is JwtSecurityToken jwtSecurityToken &&
            !string.Equals(jwtSecurityToken.Header.Typ, expectedAccessTokenJwtType))
        {
            return new(new ErrorDetails()
            {
                Error = Oidc.ProtectedResource.Errors.InvalidToken,
                ErrorDescription = "invalid JWT token type"
            });
        }

        // load the client that belongs to the client_id claim
        Client? client = null;
        var clientId = principal!.FindFirst(Jwt.ClaimTypes.ClientId);
        if (clientId is not null)
        {
            var clients = storage.GetClientStore(realm);
            client = await clients.FindEnabledClientByIdAsync(clientId.Value, ct);
        }

        if (client is null)
        {
            logger.LogError("Client not found or deleted or disabled: {ClientId}", clientId);
            return new(new ErrorDetails()
            {
                Error = Oidc.ProtectedResource.Errors.InvalidToken,
            });
        }

        var evaluatedToken = new EvaluatedToken()
        {
            Principal = principal,
            Client = client,
            Jwt = jwt,
        };

        return new TokenEvaluationResult(evaluatedToken);
    }

    public async Task<TokenEvaluationResult> ValidateReferenceAccessTokenAsync(Realm realm, string jti, CancellationToken ct = default)
    {
        var token = await storage.GetAccessTokenStore(realm).GetAsync(jti, ct);

        if (token is null)
        {
            logger.LogError("Invalid reference token.");
            return new(new ErrorDetails()
            {
                Error = Oidc.ProtectedResource.Errors.InvalidToken
            });
        }

        // Only a reference token may be presented as an opaque bearer. A JWT access token is persisted for
        // metadata/full or by the transitional in-memory backing, and its `jti` travels in the (unencrypted)
        // JWT payload — so without this check anyone holding a JWT could read its `jti` and present it as a
        // second bearer, skipping signature validation entirely. The response is deliberately the same as for
        // an absent token, so it is no oracle about which jtis exist.
        if (token.AccessTokenType is not AccessTokenType.Reference)
        {
            logger.LogError("Invalid reference token.");
            return new(new ErrorDetails()
            {
                Error = Oidc.ProtectedResource.Errors.InvalidToken
            });
        }

        if (token.CreationTime.HasExceeded(token.Lifetime, clock.GetUtcNow().UtcDateTime))
        {
            logger.LogError("Token expired.");
            return new(new ErrorDetails()
            {
                Error = Oidc.ProtectedResource.Errors.ExpiredToken
            });
        }

        // load the client that belongs to the client_id claim
        var clients = storage.GetClientStore(realm);
        var client = await clients.FindEnabledClientByIdAsync(token.ClientId, ct);
        if (client is null)
        {
            logger.LogError("Client deleted or disabled: {ClientId}", token.ClientId);
            return new(new ErrorDetails()
            {
                Error = Oidc.ProtectedResource.Errors.InvalidToken
            });
        }

        var identity = token.Claims.CreateIdentity();

        var principal = new ClaimsPrincipal(identity);

        var evaluatedToken = new EvaluatedToken()
        {
            Principal = principal,
            Client = client,
            ReferenceTokenId = jti,
        };

        return new TokenEvaluationResult(evaluatedToken);
    }

    public async Task<TokenEvaluationResult> ValidateIdentityTokenAsync(
        Realm realm, string token, string? clientId = null, bool validateLifetime = true, CancellationToken ct = default)
    {
        logger.LogDebug("Start identity token validation");

        if (token.Length > realm.Options.InputLengthRestrictions.Jwt)
        {
            logger.LogError("JWT too long");
            return new(new ErrorDetails()
            {
                Error = Oidc.ProtectedResource.Errors.InvalidToken
            });
        }

        if (clientId.IsMissing())
            clientId = GetClientIdFromJwt(token);

        if (clientId.IsMissing())
        {
            logger.LogError("No clientId supplied, can't find id in identity token.");
            return new(new ErrorDetails()
            {
                Error = Oidc.ProtectedResource.Errors.InvalidToken
            });
        }

        var clients = storage.GetClientStore(realm);
        var client = await clients.FindEnabledClientByIdAsync(clientId, ct);
        if (client == null)
        {
            logger.LogError("Unknown or disabled client: {ClientId}.", clientId);
            return new(new ErrorDetails()
            {
                Error = Oidc.ProtectedResource.Errors.InvalidToken
            });
        }

        logger.LogDebug("Client found: {ClientId} / {ClientName}", client.Id, client.Name);

        var (principal, _, error) = await TryGetPrincipal(realm, token, clientId, validateLifetime, ct);

        if (error is not null)
        {
            return new(error);
        }

        var evaluatedToken = new EvaluatedToken()
        {
            Principal = principal!,
            Client = client,
            Jwt = token,
        };

        return new TokenEvaluationResult(evaluatedToken);
    }

    private string? GetClientIdFromJwt(string token)
    {
        try
        {
            var jwt = new JwtSecurityToken(token);
            var clientId = jwt.Audiences.FirstOrDefault();

            return clientId;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Malformed JWT token: {Exception}", ex.Message);
            return null;
        }
    }

    private async Task<(ClaimsPrincipal?, SecurityToken?, ErrorDetails?)> TryGetPrincipal(
        Realm realm, 
        string jwt,
        string? audience,
        bool validateLifetime,
        CancellationToken ct)
    {
        var validationsKeys = await keys.GetValidationKeysAsync(realm, ct);

        var handler = new JwtSecurityTokenHandler();
        handler.InboundClaimTypeMap.Clear();

        var parameters = new TokenValidationParameters
        {
            ValidIssuer = ExpectedIssuer(realm),
            IssuerSigningKeys = validationsKeys.Keys,
            ValidateLifetime = validateLifetime
        };

        if (audience.IsPresent())
        {
            parameters.ValidAudience = audience;
        }
        else
        {
            parameters.ValidateAudience = false;
        }

        try
        {
            var principal = handler.ValidateToken(jwt, parameters, out var securityToken);

            return (principal, securityToken, null);
        }
        catch (SecurityTokenExpiredException expiredException)
        {
            logger.LogInformation(expiredException, "JWT token validation error: {Exception}", expiredException.Message);
            return (null, null, new ErrorDetails()
            {
                Error = Oidc.ProtectedResource.Errors.ExpiredToken
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "JWT token validation error: {Exception}", ex.Message);
            return (null, null, new ErrorDetails()
            {
                Error = Oidc.ProtectedResource.Errors.InvalidToken
            });
        }
    }
}
