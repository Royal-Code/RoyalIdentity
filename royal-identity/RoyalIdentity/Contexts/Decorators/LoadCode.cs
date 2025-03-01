using Microsoft.Extensions.Logging;
using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Extensions;
using RoyalIdentity.Options;
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

        var option = context.Items.GetOrCreate<ServerOptions>();
        var code = context.Code;

        if (code.IsMissing())
        {
            logger.LogError(option, "Authorization code is missing", context);
            context.InvalidGrant("Authorization code is missing");
            return;
        }

        if (code.Length > option.InputLengthRestrictions.AuthorizationCode)
        {
            logger.LogError(option, "Authorization code is too long", context);
            context.InvalidGrant("Authorization code is too long");
            return;
        }

        // load the authorization code.
        var authorizationCode = await storage.AuthorizationCodes.GetAuthorizationCodeAsync(code, ct);

        if (authorizationCode == null)
        {
            logger.LogError(option, "Authorization code is invalid", context);
            context.InvalidGrant("Authorization code is invalid");
            return;
        }

        if (authorizationCode.ClientId != context.ClientId)
        {
            logger.LogError(option, "Client is trying to use a code from a different client", context);
            context.InvalidGrant("Authorization code is invalid");
            return;
        }

        if (!context.RedirectUri.Equals(authorizationCode.RedirectUri, StringComparison.Ordinal))
        {
            logger.LogError(option, "Invalid redirect_uri", context);
            context.InvalidGrant("Invalid redirect_uri");
            return;
        }

        // Removes the authorization code.
        await storage.AuthorizationCodes.RemoveAuthorizationCodeAsync(code, ct);

        if (authorizationCode.CreationTime.HasExceeded(authorizationCode.Lifetime, clock.GetUtcNow().DateTime))
        {
            logger.LogError(option, "Authorization code expired", context);
            context.InvalidGrant("Authorization code expired");
            return;
        }

        context.CodeParameters.AuthorizationCode = authorizationCode;

        await next();
    }
}
