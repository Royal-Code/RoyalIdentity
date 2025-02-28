using RoyalIdentity.Models;
using RoyalIdentity.Models.Tokens;

namespace RoyalIdentity.Contracts;

public interface IJwtFactory
{

    public Task CreateTokenAsync(Realm realm, TokenBase token, CancellationToken ct);
}
