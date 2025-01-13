using RoyalIdentity.Models.Tokens;
using System.Diagnostics.CodeAnalysis;

namespace RoyalIdentity.Contexts.Parameters;

public class CodeParameters
{
    public AuthorizationCode? AuthorizationCode { get; set; }

    [MemberNotNull(nameof(AuthorizationCode))]
    public void AssertHasCode()
    {
        if (AuthorizationCode is null)
            throw new InvalidOperationException("Code was required, but is missing");
    }
}
