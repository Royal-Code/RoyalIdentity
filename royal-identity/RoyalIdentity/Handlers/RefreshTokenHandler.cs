using Microsoft.Extensions.Logging;
using RoyalIdentity.Contexts;
using RoyalIdentity.Contracts.Models;
using RoyalIdentity.Pipelines.Abstractions;

namespace RoyalIdentity.Handlers;

public class RefreshTokenHandler : IHandler<RefreshTokenContext>
{
    private readonly ILogger logger;



    public Task Handle(RefreshTokenContext context, CancellationToken ct)
    {
        context.AssertHasClient();

        logger.LogDebug("Processing refresh token request.");


        if (context.Client.UpdateAccessTokenClaimsOnRefresh)
        {
            var request = new AccessTokenRequest()
            {
                Caller = nameof(RefreshTokenHandler),
                Context = context,
                Raw = context.Raw,
                Resources = context.Resources,
                Subject = context.GetSubject()!
            };

            // create a new principal for the refreshed token

        }
        else
        {
            // loads the old access token principal
        }

        throw new NotImplementedException();
    }
}
