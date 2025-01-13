using RoyalIdentity.Contexts.Parameters;

namespace RoyalIdentity.Contexts.Withs;

public interface IWithAuthorizationCode
{
    public string? Code { get; }

    public string? CodeVerifier { get; }

    public CodeParameters CodeParameters { get; }
}
