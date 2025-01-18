using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RoyalIdentity.Contexts;
using RoyalIdentity.Contracts.Models;
using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Extensions;
using RoyalIdentity.Options;
using RoyalIdentity.Utils;

namespace RoyalIdentity.Contracts.Defaults.SecretsEvaluators;

public class TlsClientAuthSecretEvaluator : SecretEvaluatorBase
{
    private static readonly EvaluatedCredential X509InvalidCredentials =
        new(ServerConstants.ParsedSecretTypes.X509Certificate, false);

    public TlsClientAuthSecretEvaluator(
        IClientStore clientStore,
        IOptions<ServerOptions> options,
        TimeProvider clock,
        ILogger<TlsClientAuthSecretEvaluator> logger) : base(clientStore, options.Value, clock, logger)
    { }


    /// <summary>
    /// Name of authentication method (blank to suppress in discovery since we do special handling)
    /// </summary>
    public override string AuthenticationMethod => string.Empty;

    protected override EvaluatedCredential InvalidCredentials => X509InvalidCredentials;

    public override async Task<EvaluatedClient?> EvaluateAsync(IEndpointContextBase context, CancellationToken ct)
    {
        logger.LogDebug("Start parsing and evaluate TLS client authentication secret");

        var clientCertificate = await context.HttpContext.Connection.GetClientCertificateAsync(ct);
        if (clientCertificate is null)
        {
            logger.LogDebug("Client certificate not present");
            return null;
        }

        // when there is a secret, assertion, or authorization header, the client will not be evaluated
        if (context.Raw.TryGet(OidcConstants.TokenRequest.ClientAssertion, out _) ||
            context.Raw.TryGet(OidcConstants.TokenRequest.ClientSecret, out _) ||
            context.HttpContext.Request.Headers.Authorization.FirstOrDefault().IsPresent())
        {
            logger.LogDebug("Client assertion, or secret, or authorization header found in post body, aborting client evaluation");
            return null;
        }

        var hasClientId = context.Raw.TryGet(OidcConstants.TokenRequest.ClientId, out var clientId);
        if (!hasClientId)
        {
            logger.LogDebug("Client id not found in post body");
            return null;
        }

        if (clientId!.Length > options.InputLengthRestrictions.ClientId)
        {
            logger.LogError("Client ID exceeds maximum length.");
            return null;
        }

        // load client
        var client = await clientStore.FindEnabledClientByIdAsync(clientId, ct);
        if (client is null)
        {
            logger.LogError(context, $"No client with id '{clientId}' found. aborting client evaluation");
            return null;
        }

        // check client secret by certificate subject (name)
        var name = clientCertificate.Subject;
        if (name.IsPresent())
        {
            var result = client.ClientSecrets.Where(s => s.Type == ServerConstants.SecretTypes.X509CertificateName)
                .Where(s => name.Equals(s.Value, StringComparison.Ordinal))
                .Select(s => new EvaluatedClient(
                    client,
                    new EvaluatedCredential(ServerConstants.ParsedSecretTypes.X509Certificate, true, s),
                    AuthenticationMethod))
                .FirstOrDefault();

            if (result is not null)
                return result;
        }

        var thumbprint = clientCertificate.Thumbprint;
        if (thumbprint.IsPresent())
        {
            var result = client.ClientSecrets.Where(s => s.Type == ServerConstants.SecretTypes.X509CertificateThumbprint)
                .Where(s => thumbprint.Equals(s.Value, StringComparison.OrdinalIgnoreCase))
                .Select(secret => new EvaluatedClient(
                    client,
                    new EvaluatedCredential(
                        ServerConstants.ParsedSecretTypes.X509Certificate,
                        true,
                        secret,
                        clientCertificate.CreateThumbprintCnf()),
                    AuthenticationMethod))
                .FirstOrDefault();

            if (result is not null)
                return result;
        }

        logger.LogDebug("No x509 name secrets configured for client.");
        logger.LogWarning("No thumbprint found in X509 certificate.");
        return new EvaluatedClient(client, InvalidCredentials, AuthenticationMethod);
    }

}