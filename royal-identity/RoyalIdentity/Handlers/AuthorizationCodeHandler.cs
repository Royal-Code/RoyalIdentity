using Microsoft.Extensions.Logging;
using RoyalIdentity.Contexts;
using RoyalIdentity.Contracts;
using RoyalIdentity.Contracts.Models;
using RoyalIdentity.Pipelines.Abstractions;

namespace RoyalIdentity.Handlers;

public class AuthorizationCodeHandler : IHandler<AuthorizationCodeContext>
{
    private readonly ITokenFactory tokenFactory;
    private readonly ILogger logger;

    public async Task Handle(AuthorizationCodeContext context, CancellationToken ct)
    {
        logger.LogDebug("Handle authorization code context start");

        context.AssertHasCode();
        context.AssertHasClient();

        var accessTokenRequest = new AccessTokenRequest
        {
            Context = context,
            Raw = context.Raw,
            Subject = context.AuthorizationCode.Subject,
            Resources = context.Resources,
            Confirmation = context.ClientSecret.Confirmation,
            Caller = context.GrantType
        };

        var accessToken = await tokenFactory.CreateAccessTokenAsync(accessTokenRequest, ct);

        if (context.Resources.OfflineAccess)
        {
            var refreshTokenRequest = new RefreshTokenRequest()
            {
                Context = context,
                Raw = context.Raw,
                Subject = context.AuthorizationCode.Subject,
                AccessToken = accessToken,
                Caller = context.GrantType
            };

            var refreshToken = await tokenFactory.CreateRefreshTokenAsync(refreshTokenRequest, ct);
        }

        throw new NotImplementedException();
    }
}
