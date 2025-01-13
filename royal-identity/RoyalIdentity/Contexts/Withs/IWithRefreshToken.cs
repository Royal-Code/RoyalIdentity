using RoyalIdentity.Contexts.Parameters;

namespace RoyalIdentity.Contexts.Withs;

public interface IWithRefreshToken
{
    public string? Token { get; }

    public RefreshParameters RefreshParameters { get; }
}
