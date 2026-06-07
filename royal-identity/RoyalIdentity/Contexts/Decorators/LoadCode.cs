using Microsoft.Extensions.Logging;
using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Extensions;
using RoyalIdentity.Pipelines.Abstractions;

namespace RoyalIdentity.Contexts.Decorators;

public class LoadCode : IDecorator<AuthorizationCodeContext>
{
    private readonly IStorage storage;
    private readonly TimeProvider clock;
    private readonly ILogger logger;

    public LoadCode(
        IStorage storage,
        TimeProvider clock,
        ILogger<LoadCode> logger)
    {
        this.storage = storage;
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

        // load the authorization code.
        var authorizationCode = await storage.GetAuthorizationCodeStore(context.Realm).GetAuthorizationCodeAsync(code, ct);

        if (authorizationCode == null)
        {
            logger.LogError(context, "Authorization code is invalid");
            context.InvalidGrant("Authorization code is invalid");
            return;
        }

        if (authorizationCode.ClientId != context.ClientId)
        {
            logger.LogError(context, "Client is trying to use a code from a different client");
            context.InvalidGrant("Authorization code is invalid");
            return;
        }

        if (!context.RedirectUri.Equals(authorizationCode.RedirectUri, StringComparison.Ordinal))
        {
            logger.LogError(context, "Invalid redirect_uri");
            context.InvalidGrant("Invalid redirect_uri");
            return;
        }

        // Removes the authorization code.
        await storage.GetAuthorizationCodeStore(context.Realm).RemoveAuthorizationCodeAsync(code, ct);

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
