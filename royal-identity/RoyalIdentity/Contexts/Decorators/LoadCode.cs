using Microsoft.Extensions.Logging;
using RoyalIdentity.Contexts.Items;
using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Extensions;
using RoyalIdentity.Options;
using RoyalIdentity.Pipelines.Abstractions;
using static RoyalIdentity.Options.OidcConstants;

namespace RoyalIdentity.Contexts.Decorators;

public class LoadCode : IDecorator<AuthorizationCodeContext>
{
    private readonly IAuthorizationCodeStore codeStore;
    private readonly IResourceStore resourceStore;
    private readonly TimeProvider clock;
    private readonly ILogger logger;

    public LoadCode(
        IAuthorizationCodeStore codeStore,
        IResourceStore resourceStore,
        TimeProvider clock,
        ILogger<LoadCode> logger)
    {
        this.codeStore = codeStore;
        this.resourceStore = resourceStore;
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
            context.Error(TokenErrors.InvalidGrant, "Authorization code is missing");
            return;
        }

        if (code.Length > option.InputLengthRestrictions.AuthorizationCode)
        {
            logger.LogError(option, "Authorization code is too long", context);
            context.Error(TokenErrors.InvalidGrant, "Authorization code is too long");
            return;
        }

        // load the authorization code.
        var authorizationCode = await codeStore.GetAuthorizationCodeAsync(code, ct);

        if (authorizationCode == null)
        {
            logger.LogError(option, "Authorization code is invalid", context);
            context.Error(TokenErrors.InvalidGrant, "Authorization code is invalid");
            return;
        }

        if (authorizationCode.ClientId != context.ClientId)
        {
            logger.LogError(option, "Client is trying to use a code from a different client", context);
            context.Error(TokenErrors.InvalidGrant, "Authorization code is invalid");
            return;
        }

        if (!context.RedirectUri.Equals(authorizationCode.RedirectUri, StringComparison.Ordinal))
        {
            logger.LogError(option, "Invalid redirect_uri", context);
            context.Error(TokenErrors.InvalidGrant, "Invalid redirect_uri");
            return;
        }

        // Removes the authorization code.
        await codeStore.RemoveAuthorizationCodeAsync(code, ct);

        if (authorizationCode.CreationTime.HasExceeded(authorizationCode.Lifetime, clock.GetUtcNow().DateTime))
        {
            logger.LogError(option, "Authorization code expired", context);
            context.Error(TokenErrors.InvalidGrant, "Authorization code expired");
            return;
        }

        // get the requested resources
        var scopes = authorizationCode.RequestedScopes;
        var resources = await resourceStore.FindResourcesByScopeAsync(scopes, true, ct);



        context.AuthorizationCode = authorizationCode;
        context.Resources = resources;
        context.Items.GetOrCreate<Asserts>().HasCode = true;

        await next();
    }
}
