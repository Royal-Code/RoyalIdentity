using RoyalIdentity.Contexts;
using RoyalIdentity.Extensions;
using RoyalIdentity.Models;
using RoyalIdentity.Utils;

namespace RoyalIdentity.Contracts.Defaults;

public class DefaultCodeFactory : ICodeFactory
{
    public readonly TimeProvider time;
    public readonly IKeyManager keyManager;


    public async Task<AuthorizationCode> CreateCodeAsync(AuthorizeContext context, CancellationToken ct)
    {
        context.AssertHasClient();

        string? stateHash = null;
        if (context.State.IsPresent())
        {
            var credential = await keyManager.GetSigningCredentialsAsync(context.Client.AllowedIdentityTokenSigningAlgorithms, ct)
                    ?? throw new InvalidOperationException("No signing credential is configured.");

            var algorithm = credential.Algorithm;
            stateHash = CryptoHelper.CreateHashClaimValue(context.State, algorithm);
        }

        var code = new AuthorizationCode
        {
            CreationTime = time.GetUtcNow().UtcDateTime,
            ClientId = context.Client.Id,
            Lifetime = context.Client.AuthorizationCodeLifetime,
            Subject = context.Subject,
            SessionId = context.SessionId,
            CodeChallenge = context.CodeChallenge.Sha256(),
            CodeChallengeMethod = context.CodeChallengeMethod,

            IsOpenId = context.IsOpenIdRequest,
            RequestedScopes = context.RequestedScopes,
            RedirectUri = context.RedirectUri,
            Nonce = context.Nonce,
            StateHash = stateHash,
        };

        return code;
    }
}
