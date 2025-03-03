using RoyalIdentity.Users.Contracts;
using RoyalIdentity.Utils;

namespace RoyalIdentity.Users.Defaults;

public class DefaultPasswordProtector : IPasswordProtector
{
    public ValueTask<string> HashPasswordAsync(string password, CancellationToken ct = default)
    {
        return ValueTask.FromResult(PasswordHash.Create(password));
    }

    public ValueTask<bool> VerifyPasswordAsync(string password, string hash, CancellationToken ct = default)
    {
        return ValueTask.FromResult(PasswordHash.Verify(password, hash));
    }
}