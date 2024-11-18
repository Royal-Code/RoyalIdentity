using System.Diagnostics.CodeAnalysis;

namespace RoyalIdentity.Contracts.Models;

public class BearerTokenResult
{
    [MemberNotNullWhen(true, nameof(Token))]
    public bool TokenFound { get; init; }

    
    public string? Token { get; init; }

    public BearerTokenLocation UsageType { get; init; }

    public enum BearerTokenLocation
    {
        None = 0,
        AuthorizationHeader = 1,
        PostBody = 2,
        QueryString = 3
    }
}
