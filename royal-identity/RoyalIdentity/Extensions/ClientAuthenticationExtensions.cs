using RoyalIdentity.Contracts.Models;
using RoyalIdentity.Pipelines.Abstractions;

namespace RoyalIdentity.Extensions;

internal static class ClientAuthenticationExtensions
{
    /// <summary>
    /// The client authentication mechanism decided by the preflight for this request.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// When the context reached client evaluation without the decision having been made. This is a composition
    /// bug, not a request the server should answer: silently assuming a mechanism would reintroduce exactly the
    /// guessing the preflight exists to remove.
    /// </exception>
    public static ClientAuthenticationAttempt GetClientAuthenticationAttempt(this IContextBase context)
    {
        if (context.Items.TryGet<ClientAuthenticationAttempt>(out var attempt))
            return attempt;

        throw new InvalidOperationException(
            $"No {nameof(ClientAuthenticationAttempt)} on the context. Every endpoint whose pipeline " +
            "authenticates a client must run the request preflight before creating the context.");
    }
}
