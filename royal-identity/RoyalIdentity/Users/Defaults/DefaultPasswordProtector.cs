using RoyalIdentity.Security.Passwords;
using RoyalIdentity.Users.Contracts;

namespace RoyalIdentity.Users.Defaults;

public class DefaultPasswordProtector : IPasswordProtector
{
    public ValueTask<string> HashPasswordAsync(string password, CancellationToken ct = default)
    {
        return ValueTask.FromResult(PasswordHash.Create(password));
    }

    public ValueTask<bool> VerifyPasswordAsync(string password, string hash, CancellationToken ct = default)
    {
        var result = PasswordHash.Verify(password, hash);
        return ValueTask.FromResult(result is PasswordVerificationResult.Success or PasswordVerificationResult.SuccessRehashNeeded);
    }
}
