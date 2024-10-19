using RoyalIdentity.Models.Tokens;

namespace RoyalIdentity.Contracts;

public interface IJwtFactory
{

    public Task CreateTokenAsync(TokenBase token, CancellationToken ct);
}
