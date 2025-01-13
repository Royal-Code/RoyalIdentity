using RoyalIdentity.Models.Tokens;
using System.Diagnostics.CodeAnalysis;

namespace RoyalIdentity.Contexts.Parameters;

public class RefreshParameters
{
    public RefreshToken? RefreshToken { get; private set; }

    public DateTime? TokenFirstConsumedAt { get; private set; }

    public void SetRefreshToken(RefreshToken refreshToken)
    {
        RefreshToken = refreshToken;
        TokenFirstConsumedAt = refreshToken.ConsumedTime;
    }

    [MemberNotNull(nameof(RefreshToken))]
    public void AssertHasRefreshToken()
    {
        if (RefreshToken is null)
            throw new InvalidOperationException("Refresh Token was required, but is missing");
    }
}
