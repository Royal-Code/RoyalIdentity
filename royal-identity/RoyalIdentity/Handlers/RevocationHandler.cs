using Microsoft.Extensions.Logging;
using RoyalIdentity.Contexts;
using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Endpoints.Abstractions;
using RoyalIdentity.Endpoints.Defaults;
using RoyalIdentity.Extensions;
using RoyalIdentity.Options;
using RoyalIdentity.Pipelines.Abstractions;

namespace RoyalIdentity.Handlers;

/// <summary>
/// <para>
///     Handles token revocation requests.
/// </para>
/// <para>
///     The revocation endpoint provides a mechanism for clients to inform the authorization server that a previously
///     obtained token is no longer needed. This might be due to a compromised client, or because an access token is no
///     longer needed by a client. 
///     <br />
///     The revocation endpoint is an OAuth 2.0 endpoint that clients can use to notify the
///     authorization server that a previously obtained token is no longer needed.
///     <br />
///     https://www.rfc-editor.org/info/rfc7009
/// </para>
/// </summary>
public class RevocationHandler : IHandler<RevocationContext>
{
    private readonly ILogger logger;
    private readonly IStorage storage;

    public RevocationHandler(ILogger<RevocationHandler> logger, IStorage storage)
    {
        this.logger = logger;
        this.storage = storage;
    }

    public async Task Handle(RevocationContext context, CancellationToken ct)
    {
        logger.LogDebug("Handling revocation request");

        bool success = false;

        // revoke tokens
        if (context.TokenTypeHint == Oidc.TokenTypeHints.AccessToken)
        {
            logger.LogDebug("Hint was for access token");

            success = await RevokeAccessTokenAsync(context.Token!, context.ClientId!, ct);
        }
        else if (context.TokenTypeHint == Oidc.TokenTypeHints.RefreshToken)
        {
            logger.LogDebug("Hint was for refresh token");

            success = await RevokeRefreshTokenAsync(context.Token!, context.ClientId!, ct);
        }
        else if (context.TokenTypeHint.IsPresent())
        {
            logger.LogWarning("Unknown token type hint: {TokenTypeHint}", context.TokenTypeHint);

            // Note: invalid tokens do not cause an error response since the client
            // cannot handle such an error in a reasonable way.
            context.Response = ResponseHandler.Error(new ErrorResponseParameters()
            {
                Error = Oidc.Errors.Revocation.UnsupportedTokenType
            }, 200);

            return;
        }
        else
        {
            logger.LogDebug("No hint for token type");

            success = await RevokeAccessTokenAsync(context.Token!, context.ClientId!, ct);

            if (!success)
                success = await RevokeRefreshTokenAsync(context.Token!, context.ClientId!, ct);
        }

        if (success)
        {
            logger.LogDebug("Token revocation complete");
        }
        else
        {
            logger.LogInformation("Token revocation failed");
        }

        // The authorization server responds with HTTP status code 200 if the
        // token has been revoked successfully or if the client submitted an
        // invalid token.
        // Note: invalid tokens do not cause an error response since the client
        // cannot handle such an error in a reasonable way.
        context.Response = ResponseHandler.Ok();
    }

    /// <summary>
    /// Revoke access token only if it belongs to client doing the request.
    /// </summary>
    private async Task<bool> RevokeAccessTokenAsync(string token, string clientId, CancellationToken ct)
    {
        var accessToken = await storage.AccessTokens.GetAsync(token, ct);

        if (accessToken is null)
            return false;

        if (accessToken.ClientId == clientId)
        {
            logger.LogDebug("Access token revoked");

            await storage.AccessTokens.RemoveAsync(token, ct);
        }
        else
        {
            logger.LogWarning(
                "Client {ClientId} denied from revoking access token belonging to Client {TokenClientId}",
                clientId,
                accessToken.ClientId);
        }

        return true;
    }

    /// <summary>
    /// Revoke refresh token only if it belongs to client doing the request
    /// </summary>
    private async Task<bool> RevokeRefreshTokenAsync(string token, string clientId, CancellationToken ct)
    {
        var refreshToken = await storage.RefreshTokens.GetAsync(token, ct);

        if (refreshToken == null)
            return false;

        if (refreshToken.ClientId == clientId)
        {
            logger.LogDebug("Refresh token revoked");

            await storage.RefreshTokens.RemoveAsync(token, ct);
            await storage.AccessTokens.RemoveReferenceTokensAsync(refreshToken.SubjectId!, refreshToken.ClientId, ct);
        }
        else
        {
            logger.LogWarning(
                "Client {ClientId} denied from revoking a refresh token belonging to Client {TokenClientId}",
                clientId,
                refreshToken.ClientId);
        }

        return true;
    }
}