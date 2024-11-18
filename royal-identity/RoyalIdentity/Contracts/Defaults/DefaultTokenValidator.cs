using RoyalIdentity.Extensions;
using RoyalIdentity.Options;
using System.Security.Claims;
using Microsoft.Extensions.Logging;
using RoyalIdentity.Models;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;
using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Contracts.Models;
using RoyalIdentity.Contexts.Items;
using RoyalIdentity.Utils;

namespace RoyalIdentity.Contracts.Defaults;

public class DefaultTokenValidator : ITokenValidator
{
    private readonly IKeyManager _keys;
    private readonly IClientStore _clients;
    private readonly IAccessTokenStore _tokens;
    private readonly ILogger _logger;
    private readonly ServerOptions _options;
    private readonly TimeProvider _clock;

    public async Task<TokenEvaluationResult> ValidateJwtAccessTokenAsync(
        string jwt, string? expectedScope = null, string? audience = null, CancellationToken ct = default)
    {
        var keys = await _keys.GetValidationKeysAsync(ct);

        var handler = new JwtSecurityTokenHandler();
        handler.InboundClaimTypeMap.Clear();

        var parameters = new TokenValidationParameters
        {
            ValidIssuer = _options.IssuerUri,
            IssuerSigningKeys = keys.Keys,
            ValidateLifetime = true
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

            // if no audience is specified, we make at least sure that it is an access token
            if (audience.IsMissing() &&
                _options.AccessTokenJwtType.IsPresent() &&
                securityToken is JwtSecurityToken jwtSecurityToken && 
                !string.Equals(jwtSecurityToken.Header.Typ, _options.AccessTokenJwtType))
            {
                return new(new ValidationError()
                {
                    Error = "invalid JWT token type"
                });
            }

            // load the client that belongs to the client_id claim
            Client? client = null;
            var clientId = principal.FindFirst(JwtClaimTypes.ClientId);
            if (clientId is not null)
                client = await _clients.FindEnabledClientByIdAsync(clientId.Value, ct);

            if (client is null)
            {
                _logger.LogError("Client not found or deleted or disabled: {ClientId}", clientId);
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
        catch (SecurityTokenExpiredException expiredException)
        {
            _logger.LogInformation(expiredException, "JWT token validation error: {Exception}", expiredException.Message);
            return new(new ValidationError()
            {
                Error = OidcConstants.ProtectedResourceErrors.ExpiredToken
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "JWT token validation error: {Exception}", ex.Message);
            return new(new ValidationError()
            {
                Error = OidcConstants.ProtectedResourceErrors.InvalidToken
            });
        }
    }

    public async Task<TokenEvaluationResult> ValidateReferenceAccessTokenAsync(string jti, CancellationToken ct = default)
    {
        var token = await _tokens.GetAsync(jti, ct);

        if (token is null)
        {
            _logger.LogError("Invalid reference token.");
            return new(new ValidationError()
            {
                Error = OidcConstants.ProtectedResourceErrors.InvalidToken
            });
        }

        if (token.CreationTime.HasExceeded(token.Lifetime, _clock.GetUtcNow().UtcDateTime))
        {
            _logger.LogError("Token expired.");
            return new(new ValidationError()
            {
                Error = OidcConstants.ProtectedResourceErrors.ExpiredToken
            });
        }

        // load the client that belongs to the client_id claim
        var client = await _clients.FindEnabledClientByIdAsync(token.ClientId, ct);
        if (client is null)
        {
            _logger.LogError("Client deleted or disabled: {ClientId}", token.ClientId);
            return new(new ValidationError()
            {
                Error = OidcConstants.ProtectedResourceErrors.InvalidToken
            });
        }

        var identity = new ClaimsIdentity(
            token.Claims.Distinct(new ClaimComparer()),
            Constants.ServerAuthenticationType,
            JwtClaimTypes.Subject,
            JwtClaimTypes.Role);

        var principal = new ClaimsPrincipal(identity);

        var evaluatedToken = new EvaluatedToken()
        {
            Principal = principal,
            Client = client,
            ReferenceTokenId = jti,
        };
        
        return new TokenEvaluationResult(evaluatedToken);
    }

    public Task<TokenEvaluationResult> ValidateIdentityTokenAsync(string token, string? clientId = null, bool validateLifetime = true)
    {
        throw new NotImplementedException();
    }
}
