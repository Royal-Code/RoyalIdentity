using RoyalIdentity.Models.Tokens;
using System.Diagnostics.CodeAnalysis;

namespace RoyalIdentity.Contexts.Parameters;

/// <summary>
/// Authorization code loaded from the store by a decorator during the pipeline.
/// </summary>
/// <remarks>
/// Follows the Parameters/* convention (private setters, mutated via <c>Set*()</c>, guarded by
/// <c>Assert*()</c> with <see cref="System.Diagnostics.CodeAnalysis.MemberNotNullAttribute"/>) —
/// see <see cref="ClientParameters"/> for the full description.
/// </remarks>
public class CodeParameters
{
    /// <summary>
    /// Gets the authorization code loaded by the pipeline.
    /// </summary>
    public AuthorizationCode? AuthorizationCode { get; private set; }

    /// <summary>
    /// Stores the authorization code for later pipeline steps.
    /// </summary>
    /// <param name="code">The authorization code loaded from storage.</param>
    public void SetCode(AuthorizationCode code)
    {
        AuthorizationCode = code;
    }

    /// <summary>
    /// Ensures that an authorization code has been loaded.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when no authorization code has been loaded.</exception>
    [MemberNotNull(nameof(AuthorizationCode))]
    public void AssertHasCode()
    {
        if (AuthorizationCode is null)
            throw new InvalidOperationException("Code was required, but is missing");
    }
}
