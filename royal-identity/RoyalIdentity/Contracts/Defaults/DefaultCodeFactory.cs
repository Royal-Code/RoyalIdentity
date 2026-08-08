using Microsoft.Extensions.Logging;
using RoyalIdentity.Contexts;
using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Extensions;
using RoyalIdentity.Models.Tokens;
using RoyalIdentity.Utils;

namespace RoyalIdentity.Contracts.Defaults;

public class DefaultCodeFactory : ICodeFactory
{
    private readonly TimeProvider time;
    private readonly IStorage storage;
    private readonly ILogger logger;

    public DefaultCodeFactory(
        TimeProvider time,
        IStorage storage,
        ILogger<DefaultCodeFactory> logger)
    {
        this.time = time;
        this.storage = storage;
        this.logger = logger;
    }

    public async Task<AuthorizationCode> CreateCodeAsync(AuthorizeContext context, CancellationToken ct)
    {
        logger.LogDebug("Creating Authorization Code.");

        context.ClientParameters.AssertHasClient();
        context.AssertHasRedirectUri();

        var sid = context.Subject.GetSessionId();

        var code = new AuthorizationCode(
            context.ClientParameters.Client.Id,
            context.Subject,
            time.GetUtcNow().UtcDateTime,
            context.ClientParameters.Client.AuthorizationCodeLifetime,
            context.Scopes,
            context.RedirectUri)
        {
            SessionId = context.SessionId,
            CodeChallenge = PkceHelper.HashCodeChallengeForStorage(context.CodeChallenge),
            CodeChallengeMethod = context.CodeChallengeMethod,
            Nonce = context.Nonce,
            RealmId = context.Realm.Id,
        };

        await storage.GetAuthorizationCodeStore(context.Realm).StoreAuthorizationCodeAsync(code, ct);

        // Record the client on the session (dedup by client id). The store is realm-bound by the factory
        // (GetUserSessionStore(realm)), so the call carries no realm parameter (ADR-014 §2.5).
        var userSessionStore = storage.GetUserSessionStore(context.Realm);
        await userSessionStore.RecordClientAsync(sid, context.ClientId!, ct);

        // The code itself never goes to the log: it is a single-use credential, and a Debug line is still a
        // line someone can read later. The client and subject are enough to correlate the issuance.
        logger.LogDebug("Code issued for {ClientId} / {SubjectId}", context.ClientId, context.Identity?.Name);

        return code;
    }
}
