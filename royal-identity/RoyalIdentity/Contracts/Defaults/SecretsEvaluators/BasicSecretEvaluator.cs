using RoyalIdentity.Contexts;
using RoyalIdentity.Contracts.Models;
using RoyalIdentity.Extensions;
using static RoyalIdentity.Options.OidcConstants;
using Microsoft.Extensions.Logging;
using System.Text;
using RoyalIdentity.Options;
using RoyalIdentity.Contracts.Storage;
using Microsoft.Extensions.Options;

namespace RoyalIdentity.Contracts.Defaults.SecretsEvaluators;

public class BasicSecretEvaluator : SecretEvaluatorBase
{
    private static readonly EvaluatedCredential BasicInvalidCredentials =
        new (ServerConstants.ParsedSecretTypes.SharedSecret, false);

    public BasicSecretEvaluator(IClientStore clientStore,
        IOptions<ServerOptions> options,
        TimeProvider clock,
        ILogger<BasicSecretEvaluator> logger) : base(clientStore, options.Value, clock, logger)
    { }

    protected override EvaluatedCredential InvalidCredentials => BasicInvalidCredentials;

    public override string AuthenticationMethod => EndpointAuthenticationMethods.BasicAuthentication;

    public override async Task<EvaluatedClient?> EvaluateAsync(IEndpointContextBase context, CancellationToken ct)
    {
        logger.LogDebug("Start parsing and evaluate Basic Authentication secret");

        var authorization = context.HttpContext.Request.Headers.Authorization.FirstOrDefault();
        if (authorization.IsMissing())
        {
            logger.LogDebug("Authorization header not found");
            return null;
        }

        if (!authorization.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
        {
            logger.LogDebug("Authorization header is not Basic");
            return null;
        }

        string pair;
        try
        {
            pair = Encoding.UTF8.GetString(Convert.FromBase64String(authorization[6..]));
        }
        catch (FormatException ex)
        {
            logger.LogWarning(ex, "Malformed Basic Authentication credential.");
            return null;
        }
        catch (ArgumentException ex)
        {
            logger.LogWarning(ex, "Malformed Basic Authentication credential.");
            return null;
        }

        var ix = pair.IndexOf(':');
        if (ix <= 0 || pair.Length <= (ix + 1))
        {
            logger.LogError(context, "Malformed Basic Authentication credential.");
            return null;
        }

        var clientId = pair[..ix];
        var secret = pair[(ix + 1)..];

        return await EvaluateAsync(context, clientId, secret, ServerConstants.ParsedSecretTypes.SharedSecret, ct);
    }
}