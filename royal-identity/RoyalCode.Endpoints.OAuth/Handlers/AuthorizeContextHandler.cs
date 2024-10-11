using Microsoft.Extensions.Logging;
using RoyalIdentity.Contexts;
using RoyalIdentity.Contracts;
using RoyalIdentity.Extensions;
using RoyalIdentity.Models;
using RoyalIdentity.Pipelines.Abstractions;
using System.Text;
using static RoyalIdentity.Options.OidcConstants;

namespace RoyalIdentity.Handlers;

public class AuthorizeContextHandler : IHandler<AuthorizeContext>
{
    private readonly ILogger logger;
    private readonly ICodeFactory codeFactory;
    private readonly IAuthorizationCodeStore codeStore;

    public async Task Handle(AuthorizeContext context, CancellationToken ct)
    {
        if (context.GrantType == GrantType.AuthorizationCode)
        {
            await HandleCodeFlow(context, ct);
        }
        if (context.GrantType == GrantType.Implicit)
        {
            await HandleImplicitFlow(context, ct);
        }
        if (context.GrantType == GrantType.Hybrid)
        {
            await HandleHybridFlow(context, ct);
        }

        logger.LogError("Unsupported grant type: {GrantType}", context.GrantType);
        throw new InvalidOperationException("invalid grant type: " + context.GrantType);
    }

    private async Task HandleHybridFlow(AuthorizeContext context, CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    private async Task HandleImplicitFlow(AuthorizeContext context, CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    private async Task HandleCodeFlow(AuthorizeContext context, CancellationToken ct)
    {
        logger.LogDebug("Creating Authorization Code Flow response.");

        var code = await codeFactory.CreateCodeAsync(context, ct);
        var codeValue = await codeStore.StoreAuthorizationCodeAsync(code);

        context.Response = new AuthorizationCodeResponse(context)
        {
            Code = codeValue,
            SessionState = context.GenerateSessionStateValue()
        };
    }

    /// <summary>
    /// Creates an authorization code
    /// </summary>
    /// <param name="request"></param>
    /// <returns></returns>
    protected virtual async Task<AuthorizationCode> CreateCodeAsync(AuthorizeContext request)
    {
        string? stateHash = null;
        if (request.State.IsPresent())
        {
            var credential = await KeyMaterialService.GetSigningCredentialsAsync(request.Client.AllowedIdentityTokenSigningAlgorithms);
            if (credential == null)
            {
                throw new InvalidOperationException("No signing credential is configured.");
            }

            var algorithm = credential.Algorithm;
            stateHash = CryptoHelper.CreateHashClaimValue(request.State, algorithm);
        }

        var code = new AuthorizationCode
        {
            CreationTime = Clock.UtcNow.UtcDateTime,
            ClientId = request.Client.Id,
            Lifetime = request.Client.AuthorizationCodeLifetime,
            Subject = request.Subject,
            SessionId = request.SessionId,
            CodeChallenge = request.CodeChallenge.Sha256(),
            CodeChallengeMethod = request.CodeChallengeMethod,

            IsOpenId = request.IsOpenIdRequest,
            RequestedScopes = request.RequestedScopes,
            RedirectUri = request.RedirectUri,
            Nonce = request.Nonce,
            StateHash = stateHash,
        };

        return code;
    }
}
