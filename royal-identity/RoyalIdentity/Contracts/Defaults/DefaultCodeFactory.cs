﻿using Microsoft.Extensions.Logging;
using RoyalIdentity.Contexts;
using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Extensions;
using RoyalIdentity.Models.Tokens;

namespace RoyalIdentity.Contracts.Defaults;

public class DefaultCodeFactory : ICodeFactory
{
    private readonly TimeProvider time;
    private readonly ISessionStateGenerator sessionStateGenerator;
    private readonly IStorage storage;
    private readonly ILogger logger;

    public DefaultCodeFactory(
        TimeProvider time,
        ISessionStateGenerator sessionStateGenerator,
        IStorage storage,
        ILogger<DefaultCodeFactory> logger)
    {
        this.time = time;
        this.sessionStateGenerator = sessionStateGenerator;
        this.storage = storage;
        this.logger = logger;
    }

    public async Task<AuthorizationCode> CreateCodeAsync(AuthorizeContext context, CancellationToken ct)
    {
        logger.LogDebug("Creating Authorization Code.");

        context.ClientParameters.AssertHasClient();
        context.AssertHasRedirectUri();

        var sid = context.Subject.GetSessionId();

        var sessionState = sessionStateGenerator.GenerateSessionStateValue(context);

        var code = new AuthorizationCode(
            context.ClientParameters.Client.Id,
            context.Subject,
            sessionState,
            time.GetUtcNow().UtcDateTime,
            context.ClientParameters.Client.AuthorizationCodeLifetime,
            context.Resources,
            context.RedirectUri)
        {
            SessionId = context.SessionId,
            CodeChallenge = context.CodeChallenge.Sha256(),
            CodeChallengeMethod = context.CodeChallengeMethod,
            Nonce = context.Nonce,
            StateHash = context.StateHash,
        };

        await storage.AuthorizationCodes.StoreAuthorizationCodeAsync(code, ct);

        var userSessionStore = storage.GetUserSessionStore(context.Realm);
        await userSessionStore.AddClientIdAsync(sid, context.ClientId!, ct);

        logger.LogDebug("Code issued for {ClientId} / {SubjectId}: {Code}", context.ClientId, context.Identity?.Name, code.Code);

        return code;
    }
}
