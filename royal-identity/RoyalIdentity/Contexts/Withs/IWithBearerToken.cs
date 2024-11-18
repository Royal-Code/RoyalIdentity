using RoyalIdentity.Contracts.Models;
using RoyalIdentity.Endpoints.Abstractions;
using System.Diagnostics.CodeAnalysis;

namespace RoyalIdentity.Contexts.Withs;

public interface IWithBearerToken : IContextBase
{

    public string Token { get; }

    EvaluatedToken? EvaluatedToken { get; set; }

    [MemberNotNull(nameof(EvaluatedToken))]
    public void AssertHasToken();
}
