using RoyalIdentity.Contracts.Models;
using System.Diagnostics.CodeAnalysis;

namespace RoyalIdentity.Contexts.Parameters;

public class BearerParameters
{
    public EvaluatedToken? EvaluatedToken { get; set; }

    [MemberNotNull(nameof(EvaluatedToken))]
    public void AssertHasToken()
    {
        if (EvaluatedToken is null)
            throw new InvalidOperationException("Bearer Token was required, but is missing");
    }
}
