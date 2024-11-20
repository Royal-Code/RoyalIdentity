using Microsoft.Extensions.Logging;
using RoyalIdentity.Contexts;
using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Endpoints.Defaults;
using RoyalIdentity.Options;
using RoyalIdentity.Pipelines.Abstractions;

namespace RoyalIdentity.Handlers;

public class RevocationHandler : IHandler<RevocationContext>
{
    private readonly ILogger logger;
    private readonly IAccessTokenStore accessTokenStore;
    private readonly IRefreshTokenStore refreshTokenStore;

    public RevocationHandler(
        ILogger<RevocationHandler> logger,
        IAccessTokenStore accessTokenStore,
        IRefreshTokenStore refreshTokenStore)
    {
        this.logger = logger;
        this.accessTokenStore = accessTokenStore;
        this.refreshTokenStore = refreshTokenStore;
    }

    public async Task Handle(RevocationContext context, CancellationToken ct)
    {
        bool success = false;

        // revoke tokens
        if (context.TokenTypeHint == Constants.TokenTypeHints.AccessToken)
        {
            logger.LogTrace("Hint was for access token");

            success = await RevokeAccessTokenAsync(context.Token!, context.ClientId!, ct);
        }
        else if (context.TokenTypeHint == Constants.TokenTypeHints.RefreshToken)
        {
            logger.LogTrace("Hint was for refresh token");
            success = await RevokeRefreshTokenAsync(context.Token!, context.ClientId!, ct);
        }
        else
        {
            logger.LogTrace("No hint for token type");

            success = await RevokeAccessTokenAsync(context.Token!, context.ClientId!, ct);

            if (!success)
                success = await RevokeRefreshTokenAsync(context.Token!, context.ClientId!, ct);
        }

        if (success)
        {
            logger.LogInformation("Token revocation complete");

            // It is not necessary to raise an event here, there is nothing to do with the result
            //// await _events.RaiseAsync(new TokenRevokedSuccessEvent(requestValidationResult, requestValidationResult.Client));
        }

        context.Response = ResponseHandler.Ok();
    }

    /// <summary>
    /// Revoke access token only if it belongs to client doing the request.
    /// </summary>
    private async Task<bool> RevokeAccessTokenAsync(string token, string clientId, CancellationToken ct)
    {
        var accessToken = await accessTokenStore.GetAsync(token, ct);

        if (accessToken is null)
            return false;

        if (accessToken.ClientId == clientId)
        {
            logger.LogDebug("Access token revoked");

            await accessTokenStore.RemoveAsync(token, ct);
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
        var refreshToken = await refreshTokenStore.GetAsync(token, ct);

        if (refreshToken == null)
            return false;

        if (refreshToken.ClientId == clientId)
        {
            logger.LogDebug("Refresh token revoked");

            await refreshTokenStore.RemoveAsync(token, ct);
            await accessTokenStore.RemoveReferenceTokensAsync(refreshToken.SubjectId!, refreshToken.ClientId, ct);
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