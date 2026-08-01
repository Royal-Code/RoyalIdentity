using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using RoyalIdentity.Contexts.Withs;
using RoyalIdentity.Contracts;
using RoyalIdentity.Contracts.Models;
using RoyalIdentity.Extensions;
using RoyalIdentity.Options;
using RoyalIdentity.Pipelines.Abstractions;

namespace RoyalIdentity.Contexts.Decorators;

public class EvaluateClient : IDecorator<IWithClient>
{
    /// <summary>
    /// The single answer to every client authentication failure. An unknown client, a wrong secret, a client
    /// requiring a secret that sent none and a client that is disabled are all refused with this exact text:
    /// telling them apart would let a caller enumerate which client identifiers exist (DF15).
    /// </summary>
    private const string AuthenticationFailed = "Client authentication failed";

    private readonly IClientSecretChecker clientSecretChecker;
    private readonly ILogger logger;

    public EvaluateClient(IClientSecretChecker clientSecretChecker, ILogger<EvaluateClient> logger)
    {
        this.clientSecretChecker = clientSecretChecker;
        this.logger = logger;
    }

    public async Task Decorate(IWithClient context, Func<Task> next, CancellationToken ct)
    {
        logger.LogDebug("Start client evaluation");

        var attempt = context.GetClientAuthenticationAttempt();

        var evaluatedClient = await clientSecretChecker.EvaluateClientAsync(context, ct);
        if (evaluatedClient is null)
        {
            logger.LogError("No client identifier found");

            Fail(context, attempt);
            return;
        }

        if (!evaluatedClient.Credential.IsValid)
        {
            logger.LogError("Client secret validation failed for client: {Name} ({Id}).",
                evaluatedClient.Client.Name,
                evaluatedClient.Client.Id);

            Fail(context, attempt);
            return;
        }

        if (evaluatedClient.Client.RequireClientSecret &&
            evaluatedClient.Credential.Type is Server.ParsedSecretTypes.NoSecret)
        {
            logger.LogError("Client secret not informed for client: {Name} ({Id})",
                evaluatedClient.Client.Name,
                evaluatedClient.Client.Id);

            Fail(context, attempt);
            return;
        }

        if (!evaluatedClient.Client.Enabled)
        {
            logger.LogError("Client not enabled: {Name} ({Id})",
                evaluatedClient.Client.Name,
                evaluatedClient.Client.Id);

            Fail(context, attempt);
            return;
        }

        context.ClientParameters.SetClientAndSecret(evaluatedClient.Client, evaluatedClient.Credential, evaluatedClient.AuthenticationMethod);

        await next();
    }

    /// <summary>
    /// RFC 6749 §5.2: when the client tried to authenticate through the <c>Authorization</c> header, the
    /// failure is HTTP 401 with a challenge naming the scheme; every other mechanism answers 400.
    /// </summary>
    private static void Fail(IWithClient context, ClientAuthenticationAttempt attempt)
    {
        if (!attempt.ViaAuthorizationHeader)
        {
            context.Error(Oidc.Token.Errors.InvalidClient, AuthenticationFailed);
            return;
        }

        context.Error(
            Oidc.Token.Errors.InvalidClient,
            AuthenticationFailed,
            StatusCodes.Status401Unauthorized,
            new Dictionary<string, string>
            {
                // The challenge carries only the scheme and the protection space, never anything the request
                // supplied: a header echoing input would be both a credential leak and an injection vector.
                ["WWW-Authenticate"] = $"Basic realm=\"{context.Realm.Path}\""
            });
    }
}
