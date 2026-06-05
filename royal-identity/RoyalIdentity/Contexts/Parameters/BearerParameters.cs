using RoyalIdentity.Contracts.Models;
using System.Diagnostics.CodeAnalysis;

namespace RoyalIdentity.Contexts.Parameters;

public class BearerParameters
{
    public EvaluatedToken? EvaluatedToken { get; private set; }

    public void SetToken(EvaluatedToken token)
    {
        EvaluatedToken = token;
    }

    [MemberNotNull(nameof(EvaluatedToken))]
    public void AssertHasToken()
    {
        if (EvaluatedToken is null)
            throw new InvalidOperationException("Bearer Token was required, but is missing");
    }
}
