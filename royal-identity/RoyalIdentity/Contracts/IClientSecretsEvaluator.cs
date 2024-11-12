using RoyalIdentity.Contexts;

namespace RoyalIdentity.Contracts;

public interface IClientSecretsEvaluator
{
    /// <summary>
    /// Tries to find a secret on the context that can be used for authentication
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>
    /// A parsed secret
    /// </returns>
    Task<EvaluatedClient?> EvaluateAsync(IEndpointContextBase context, CancellationToken ct);

    /// <summary>
    /// Returns the authentication method name that this parser implements
    /// </summary>
    /// <value>
    /// The authentication method.
    /// </value>
    string AuthenticationMethod { get; }
}