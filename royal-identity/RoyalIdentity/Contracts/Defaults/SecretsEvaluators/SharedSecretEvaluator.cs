using RoyalIdentity.Contexts;
using RoyalIdentity.Contracts.Models;
using RoyalIdentity.Extensions;
using static RoyalIdentity.Options.OidcConstants;
using Microsoft.Extensions.Logging;
using System.Text;
using RoyalIdentity.Options;
using RoyalIdentity.Contracts.Storage;
using System.Runtime.CompilerServices;

namespace RoyalIdentity.Contracts.Defaults.SecretsEvaluators;

public class SharedSecretEvaluator : IClientSecretsEvaluator
{
    private readonly IClientStore clientStore;
    private readonly ServerOptions options;
    private readonly TimeProvider clock;
    private readonly ILogger logger;



    public string AuthenticationMethod => EndpointAuthenticationMethods.BasicAuthentication;

    public async Task<EvaluatedClient?> EvaluateAsync(IEndpointContextBase context, CancellationToken ct)
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

        if (clientId.Length > options.InputLengthRestrictions.ClientId)
        {
            logger.LogError(context, "Client ID exceeds maximum length.");
            return null;
        }

        if (secret.Length > options.InputLengthRestrictions.ClientSecret)
        {
            logger.LogError(context, "Client secret exceeds maximum length.");
            return null;
        }

        // load client
        var client = await clientStore.FindEnabledClientByIdAsync(clientId, ct);
        if (client is null)
        {
            logger.LogError(context, $"No client with id '{clientId}' found. aborting");

            return null;
        }

        // get secrets
        var sharedSecrets = client.ClientSecrets
            .Where(s => s.Type == ServerConstants.SecretTypes.SharedSecret)
            .Where(s => !s.Expiration.HasExpired(clock.GetUtcNow().UtcDateTime))
            .ToList();

        // validate secret.
        foreach (var sharedSecret in sharedSecrets)
        {
            var secretDescription = string.IsNullOrEmpty(sharedSecret.Description) ? "no description" : sharedSecret.Description;

            bool isValid = false;
            byte[] secretBytes;

            try
            {
                secretBytes = Convert.FromBase64String(sharedSecret.Value);
            }
            catch (FormatException ex)
            {
                logger.LogError(context, ex, $"Secret: {secretDescription} uses invalid hashing algorithm.");
                return null; // retornar objeto com erro
            }
            catch (ArgumentNullException ex)
            {
                logger.LogError(context, ex, $"Secret: {secretDescription} is null.");
                return null; // retornar objeto com erro
            }

            if (secretBytes.Length == 32)
            {
                isValid = TimeConstantComparer.IsEqual(sharedSecret.Value, secret.Sha256());
            }
            else if (secretBytes.Length == 64)
            {
                isValid = TimeConstantComparer.IsEqual(sharedSecret.Value, secret.Sha512());
            }
            else
            {
                logger.LogError(context, $"Secret: {secretDescription} uses invalid hashing algorithm.");
                return null; // retornar objeto com erro
            }

            if (isValid)
            {
                return success;
            }
        }


        throw new NotImplementedException();
    }
}

/// <summary>
/// Helper class to do equality checks without leaking timing information
/// </summary>
public static class TimeConstantComparer
{
    /// <summary>
    /// Checks two strings for equality without leaking timing information.
    /// </summary>
    /// <param name="s1">string 1.</param>
    /// <param name="s2">string 2.</param>
    /// <returns>
    /// 	<c>true</c> if the specified strings are equal; otherwise, <c>false</c>.
    /// </returns>
    [MethodImpl(MethodImplOptions.NoOptimization)]
    public static bool IsEqual(string s1, string s2)
    {
        if (s1 == null && s2 == null)
        {
            return true;
        }

        if (s1 == null || s2 == null)
        {
            return false;
        }

        if (s1.Length != s2.Length)
        {
            return false;
        }

        var s1chars = s1.ToCharArray();
        var s2chars = s2.ToCharArray();

        int hits = 0;
        for (int i = 0; i < s1.Length; i++)
        {
            if (s1chars[i].Equals(s2chars[i]))
            {
                hits += 2;
            }
            else
            {
                hits += 1;
            }
        }

        bool same = (hits == s1.Length * 2);

        return same;
    }
}