using RoyalIdentity.Contracts.Models;
using System.Diagnostics.CodeAnalysis;

namespace RoyalIdentity.Contexts.Parameters;

/// <summary>
/// Evaluated bearer token loaded by a decorator during the pipeline.
/// </summary>
/// <remarks>
/// Follows the Parameters/* convention (private setters, mutated via <c>Set*()</c>, guarded by
/// <c>Assert*()</c> with <see cref="System.Diagnostics.CodeAnalysis.MemberNotNullAttribute"/>) —
/// see <see cref="ClientParameters"/> for the full description.
/// </remarks>
public class BearerParameters
{
    /// <summary>
    /// Gets the evaluated bearer token loaded by the pipeline.
    /// </summary>
    public EvaluatedToken? EvaluatedToken { get; private set; }

    /// <summary>
    /// Stores the evaluated bearer token for later pipeline steps.
    /// </summary>
    /// <param name="token">The evaluated bearer token.</param>
    public void SetToken(EvaluatedToken token)
    {
        EvaluatedToken = token;
    }

    /// <summary>
    /// Ensures that an evaluated bearer token has been loaded.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when no bearer token has been loaded.</exception>
    [MemberNotNull(nameof(EvaluatedToken))]
    public void AssertHasToken()
    {
        if (EvaluatedToken is null)
            throw new InvalidOperationException("Bearer Token was required, but is missing");
    }
}
