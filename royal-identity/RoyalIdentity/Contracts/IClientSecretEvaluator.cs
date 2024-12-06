using RoyalIdentity.Contexts;
using RoyalIdentity.Contracts.Models;

namespace RoyalIdentity.Contracts;

public interface IClientSecretEvaluator
{
    public static readonly Task<EvaluatedClient?> NotFound = Task.FromResult<EvaluatedClient?>(null);

    /// <summary>
    /// Tries to find a secret on the context that can be used for authentication.
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