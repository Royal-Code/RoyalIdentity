using RoyalIdentity.Contexts;

namespace RoyalIdentity.Contracts;

/// <summary>
/// Parser for finding the best secret in an Enumerable List
/// </summary>
public interface IClientSecretChecker
{
    /// <summary>
    /// Tries to find the best secret on the context that can be used for authentication
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>
    /// A parsed secret
    /// </returns>
    [Redesign("Troca o tipo de retorno;")]
    Task<EvaluatedClient?> EvaluateClientAsync(IEndpointContextBase context, CancellationToken ct);

    /// <summary>
    /// Gets all available authentication methods.
    /// </summary>
    /// <returns></returns>
    IEnumerable<string> GetAvailableAuthenticationMethods();
}