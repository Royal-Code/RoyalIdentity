using RoyalIdentity.Extensions;
using RoyalIdentity.Options;
using System.Security.Claims;
using Microsoft.Extensions.Logging;
using RoyalIdentity.Models;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;
using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Contracts.Models;
using RoyalIdentity.Utils;
using Microsoft.Extensions.Options;

namespace RoyalIdentity.Contracts.Defaults;

public class DefaultTokenValidator : ITokenValidator
{
    private readonly IKeyManager keys;
    private readonly IClientStore clients;
    private readonly IAccessTokenStore tokens;
    private readonly ILogger logger;
    private readonly ServerOptions options;
    private readonly TimeProvider clock;

    public DefaultTokenValidator(
        IKeyManager keys,
        IClientStore clients,
        IAccessTokenStore tokens,
        ILogger<DefaultTokenValidator> logger,
        IOptions<ServerOptions> options,
        TimeProvider clock)
    {
        this.keys = keys;
        this.clients = clients;
        this.tokens = tokens;
        this.logger = logger;
        this.options = options.Value;
        this.clock = clock;
    }

    public async Task<TokenEvaluationResult> ValidateJwtAccessTokenAsync(
        string jwt, string? expectedScope = null, string? audience = null, CancellationToken ct = default)
    {

        var (principal, securityToken, error) = await TryGetPrincipal(jwt, audience, true, ct);

        if (error is not null)
        {
            return new(error);
        }

        // if no audience is specified, we make at least sure that it is an access token
        if (audience.IsMissing() &&
            options.AccessTokenJwtType.IsPresent() &&
            securityToken is JwtSecurityToken jwtSecurityToken &&
            !string.Equals(jwtSecurityToken.Header.Typ, options.AccessTokenJwtType))
        {
            return new(new ValidationError()
            {
                Error = "invalid JWT token type"
            });
        }

        // load the client that belongs to the client_id claim
        Client? client = null;
        var clientId = principal!.FindFirst(JwtClaimTypes.ClientId);
        if (clientId is not null)
            client = await clients.FindEnabledClientByIdAsync(clientId.Value, ct);

        if (client is null)
        {
            logger.LogError("Client not found or deleted or disabled: {ClientId}", clientId);
            return new(new ValidationError()
            {
                Error = OidcConstants.ProtectedResourceErrors.InvalidToken
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

    public async Task<TokenEvaluationResult> ValidateReferenceAccessTokenAsync(string jti, CancellationToken ct = default)
    {
        var token = await tokens.GetAsync(jti, ct);

        if (token is null)
        {
            logger.LogError("Invalid reference token.");
            return new(new ValidationError()
            {
                Error = OidcConstants.ProtectedResourceErrors.InvalidToken
            });
        }

        if (token.CreationTime.HasExceeded(token.Lifetime, clock.GetUtcNow().UtcDateTime))
        {
            logger.LogError("Token expired.");
            return new(new ValidationError()
            {
                Error = OidcConstants.ProtectedResourceErrors.ExpiredToken
            });
        }

        // load the client that belongs to the client_id claim
        var client = await clients.FindEnabledClientByIdAsync(token.ClientId, ct);
        if (client is null)
        {
            logger.LogError("Client deleted or disabled: {ClientId}", token.ClientId);
            return new(new ValidationError()
            {
                Error = OidcConstants.ProtectedResourceErrors.InvalidToken
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
        string token, string? clientId = null, bool validateLifetime = true, CancellationToken ct = default)
    {
        logger.LogDebug("Start identity token validation");

        if (token.Length > options.InputLengthRestrictions.Jwt)
        {
            logger.LogError("JWT too long");
            return new(new ValidationError()
            {
                Error = OidcConstants.ProtectedResourceErrors.InvalidToken
            });
        }

        if (clientId.IsMissing())
            clientId = GetClientIdFromJwt(token);

        if (clientId.IsMissing())
        {
            logger.LogError("No clientId supplied, can't find id in identity token.");
            return new(new ValidationError()
            {
                Error = OidcConstants.ProtectedResourceErrors.InvalidToken
            });
        }

        var client = await clients.FindEnabledClientByIdAsync(clientId, ct);
        if (client == null)
        {
            logger.LogError("Unknown or disabled client: {ClientId}.", clientId);
            return new(new ValidationError()
            {
                Error = OidcConstants.ProtectedResourceErrors.InvalidToken
            });
        }

        logger.LogDebug("Client found: {ClientId} / {ClientName}", client.Id, client.Name);

        var (principal, _, error) = await TryGetPrincipal(token, clientId, validateLifetime, ct);

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

    private async Task<(ClaimsPrincipal?, SecurityToken?, ValidationError?)> TryGetPrincipal(
        string jwt,
        string? audience,
        bool validateLifetime,
        CancellationToken ct)
    {
        var validationsKeys = await keys.GetValidationKeysAsync(ct);

        var handler = new JwtSecurityTokenHandler();
        handler.InboundClaimTypeMap.Clear();

        var parameters = new TokenValidationParameters
        {
            ValidIssuer = options.IssuerUri,
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
            return (null, null, new ValidationError()
            {
                Error = OidcConstants.ProtectedResourceErrors.ExpiredToken
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "JWT token validation error: {Exception}", ex.Message);
            return (null, null, new ValidationError()
            {
                Error = OidcConstants.ProtectedResourceErrors.InvalidToken
            });
        }
    }
}
