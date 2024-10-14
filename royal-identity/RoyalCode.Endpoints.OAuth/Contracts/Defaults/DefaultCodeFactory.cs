using RoyalIdentity.Contexts;
using RoyalIdentity.Extensions;
using RoyalIdentity.Models;
using RoyalIdentity.Utils;

namespace RoyalIdentity.Contracts.Defaults;

public class DefaultCodeFactory : ICodeFactory
{
    private readonly TimeProvider time;
    private readonly IKeyManager keyManager;
    private readonly ISessionStateGenerator sessionStateGenerator;

    public DefaultCodeFactory(TimeProvider time, IKeyManager keyManager, ISessionStateGenerator sessionStateGenerator)
    {
        this.time = time;
        this.keyManager = keyManager;
        this.sessionStateGenerator = sessionStateGenerator;
    }

    public async Task<AuthorizationCode> CreateCodeAsync(AuthorizeContext context, CancellationToken ct)
    {
        context.AssertHasClient();
        context.AssertHasRedirectUri();

        string? stateHash = null;
        if (context.State.IsPresent())
        {
            var credential = await keyManager.GetSigningCredentialsAsync(context.Client.AllowedIdentityTokenSigningAlgorithms, ct)
                    ?? throw new InvalidOperationException("No signing credential is configured.");

            var algorithm = credential.Algorithm;
            stateHash = CryptoHelper.CreateHashClaimValue(context.State, algorithm);
        }

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
            StateHash = stateHash,
        };

        return code;
    }
}
