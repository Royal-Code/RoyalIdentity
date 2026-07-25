using Microsoft.Extensions.Logging;
using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Extensions;
using RoyalIdentity.Pipelines.Abstractions;

namespace RoyalIdentity.Contexts.Decorators;

public class LoadCode : IDecorator<AuthorizationCodeContext>
{
    private readonly IAuthorizationCodeConsumer consumer;
    private readonly TimeProvider clock;
    private readonly ILogger logger;

    public LoadCode(
        IAuthorizationCodeConsumer consumer,
        TimeProvider clock,
        ILogger<LoadCode> logger)
    {
        this.consumer = consumer;
        this.clock = clock;
        this.logger = logger;
    }

    public async Task Decorate(AuthorizationCodeContext context, Func<Task> next, CancellationToken ct)
    {
        context.AssertHasRedirectUri();

        var restrictions = context.Options.InputLengthRestrictions;
        var code = context.Code;

        if (code.IsMissing())
        {
            logger.LogError(context, "Authorization code is missing");
            context.InvalidGrant("Authorization code is missing");
            return;
        }

        if (code.Length > restrictions.AuthorizationCode)
        {
            logger.LogError(context, "Authorization code is too long");
            context.InvalidGrant("Authorization code is too long");
            return;
        }

        // Single-use consumption: the expected client and redirect URI are part of the operation, so exactly
        // one concurrent caller obtains the code and a request whose binding does not match consumes nothing
        // (MP-2 / plan-data-operational-storage DF11).
        var authorizationCode = await consumer.ConsumeAsync(context.Realm, code, context.ClientId, context.RedirectUri, ct);

        if (authorizationCode is null)
        {
            // Absent, already consumed and mismatched binding are deliberately indistinguishable: the response
            // must not become an oracle about which codes exist or who they belong to. The OAuth code stays
            // invalid_grant; only the description is now this single generic one (DF11).
            logger.LogError(context, "Authorization code is invalid");
            context.InvalidGrant("Authorization code is invalid");
            return;
        }

        // Expiration and PKCE are checked after the consumption, so an attempt that won the code and then fails
        // them does not make it reusable.
        if (authorizationCode.CreationTime.HasExceeded(authorizationCode.Lifetime, clock.GetUtcNow().DateTime))
        {
            logger.LogError(context, "Authorization code expired");
            context.InvalidGrant("Authorization code expired");
            return;
        }

        context.CodeParameters.SetCode(authorizationCode);

        await next();
    }
}
