using RoyalIdentity.Models.Tokens;
using System.Diagnostics.CodeAnalysis;

namespace RoyalIdentity.Contexts.Parameters;

/// <summary>
/// Refresh token loaded from the store by a decorator during the pipeline.
/// </summary>
/// <remarks>
/// Follows the Parameters/* convention (private setters, mutated via <c>Set*()</c>, guarded by
/// <c>Assert*()</c> with <see cref="System.Diagnostics.CodeAnalysis.MemberNotNullAttribute"/>) —
/// see <see cref="ClientParameters"/> for the full description.
/// </remarks>
public class RefreshParameters
{
    /// <summary>
    /// Gets the refresh token loaded by the pipeline.
    /// </summary>
    public RefreshToken? RefreshToken { get; private set; }

    /// <summary>
    /// Gets the first time the refresh token was consumed, when available.
    /// </summary>
    public DateTime? TokenFirstConsumedAt { get; private set; }

    /// <summary>
    /// Stores the refresh token and captures its consumed timestamp.
    /// </summary>
    /// <param name="refreshToken">The refresh token loaded from storage.</param>
    public void SetRefreshToken(RefreshToken refreshToken)
    {
        RefreshToken = refreshToken;
        TokenFirstConsumedAt = refreshToken.ConsumedTime;
    }

    /// <summary>
    /// Ensures that a refresh token has been loaded.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when no refresh token has been loaded.</exception>
    [MemberNotNull(nameof(RefreshToken))]
    public void AssertHasRefreshToken()
    {
        if (RefreshToken is null)
            throw new InvalidOperationException("Refresh Token was required, but is missing");
    }
}
