using System.Diagnostics.CodeAnalysis;

namespace RoyalIdentity.Contexts.Withs;

public interface IWithRedirectUri : IWithClient
{
    public string? RedirectUri { get; }

    [MemberNotNull(nameof(RedirectUri))]
    public void AssertHasRedirectUri();
}
