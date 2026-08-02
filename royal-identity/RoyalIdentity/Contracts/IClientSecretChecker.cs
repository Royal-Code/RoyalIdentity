using RoyalIdentity.Contexts;
using RoyalIdentity.Contracts.Models;

namespace RoyalIdentity.Contracts;

/// <summary>
/// Evaluates the client credentials presented by an endpoint request.
/// </summary>
public interface IClientSecretChecker
{
    /// <summary>
    /// Evaluates the request authentication mechanism and resolves the authenticated client.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>
    /// The evaluated client, or <see langword="null"/> when the credentials cannot authenticate a client.
    /// </returns>
    Task<EvaluatedClient?> EvaluateClientAsync(IEndpointContextBase context, CancellationToken ct);

    /// <summary>
    /// Gets all available authentication methods.
    /// </summary>
    /// <returns></returns>
    IEnumerable<string> GetAvailableAuthenticationMethods();
}
