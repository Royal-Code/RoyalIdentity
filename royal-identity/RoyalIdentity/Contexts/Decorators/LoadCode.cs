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
        context.ClientParameters.AssertHasClient();

        var restrictions = context.Options.InputLengthRestrictions;
        var code = context.Code;

        // A required parameter that is absent or malformed never became a grant, so it is a malformed request
        // and not an invalid one (DF9). Nothing was presented that the server could refuse as a credential —
        // which is also why these two carry a description of their own instead of the generic refusal below.
        if (code.IsMissing())
        {
            logger.LogError(context, "Authorization code is missing");
            context.Error(Oidc.Token.Errors.InvalidRequest, "Authorization code is missing");
            return;
        }

        if (code.Length > restrictions.AuthorizationCode)
        {
            logger.LogError(context, "Authorization code is too long");
            context.Error(Oidc.Token.Errors.InvalidRequest, "Authorization code is too long");
            return;
        }

        // Single-use consumption: the expected client and redirect URI are part of the operation, so exactly
        // one concurrent caller obtains the code and a request whose binding does not match consumes nothing
        // (MP-2 / plan-data-operational-storage DF11).
        var authorizationCode = await storage.GetAuthorizationCodeStore(context.Realm)
            .ConsumeAuthorizationCodeAsync(code, context.ClientParameters.Client.Id, context.RedirectUri, ct);

        if (authorizationCode is null)
        {
            // Absent, already consumed and mismatched binding are deliberately indistinguishable: the response
            // must not become an oracle about which codes exist or who they belong to. The OAuth code stays
            // invalid_grant; only the description is now this single generic one (DF11).
            logger.LogError(context, "Authorization code is invalid");
            context.Error(Oidc.Token.Errors.InvalidGrant, AuthorizationCodeRefusal.Description);
            return;
        }

        // Expiration and PKCE are checked after the consumption, so an attempt that won the code and then fails
        // them does not make it reusable.
        if (authorizationCode.CreationTime.HasExceeded(authorizationCode.Lifetime, clock.GetUtcNow().DateTime))
        {
            logger.LogError(context, "Authorization code expired");
            context.Error(Oidc.Token.Errors.InvalidGrant, "Authorization code expired");
            return;
        }

        context.CodeParameters.SetCode(authorizationCode);

        await next();
    }
}
