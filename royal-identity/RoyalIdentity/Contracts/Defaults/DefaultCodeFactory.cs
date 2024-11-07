using Microsoft.Extensions.Logging;
using RoyalIdentity.Contexts;
using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Extensions;
using RoyalIdentity.Models.Tokens;
using RoyalIdentity.Users;
using RoyalIdentity.Users.Contracts;

namespace RoyalIdentity.Contracts.Defaults;

public class DefaultCodeFactory : ICodeFactory
{
    private readonly TimeProvider time;
    private readonly ISessionStateGenerator sessionStateGenerator;
    private readonly IAuthorizationCodeStore codeStore;
    private readonly IUserSessionStore userSessionStore;
    private readonly ILogger logger;

    public DefaultCodeFactory(
        TimeProvider time,
        ISessionStateGenerator sessionStateGenerator,
        IAuthorizationCodeStore codeStore,
        IUserSessionStore userSessionStore,
        ILogger<DefaultCodeFactory> logger)
    {
        this.time = time;
        this.sessionStateGenerator = sessionStateGenerator;
        this.codeStore = codeStore;
        this.userSessionStore = userSessionStore;
        this.logger = logger;
    }

    public async Task<AuthorizationCode> CreateCodeAsync(AuthorizeContext context, CancellationToken ct)
    {
        logger.LogDebug("Creating Authorization Code.");

        context.AssertHasClient();
        context.AssertHasRedirectUri();

        var sid = context.Subject.GetSessionId();

        var sessionState = sessionStateGenerator.GenerateSessionStateValue(context);

        var code = new AuthorizationCode(
            context.Client.Id,
            context.Subject,
            sessionState,
            time.GetUtcNow().UtcDateTime,
            context.Client.AuthorizationCodeLifetime,
            context.IsOpenIdRequest,
            context.RequestedScopes,
            context.RedirectUri)
        {
            SessionId = context.SessionId,
            CodeChallenge = context.CodeChallenge.Sha256(),
            CodeChallengeMethod = context.CodeChallengeMethod,
            Nonce = context.Nonce,
            StateHash = context.StateHash,
        };

        await codeStore.StoreAuthorizationCodeAsync(code, ct);
        await userSessionStore.AddClientIdAsync(sid, context.ClientId!, ct);

        logger.LogDebug("Code issued for {ClientId} / {SubjectId}: {Code}", context.ClientId, context.Identity?.Name, code.Code);

        return code;
    }
}
