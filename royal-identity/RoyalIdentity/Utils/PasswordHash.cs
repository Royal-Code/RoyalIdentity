using RoyalIdentity.Security.Passwords;

namespace RoyalIdentity.Utils;

/// <summary>
/// Delegate wrapper kept for backward compatibility until Phase 7.
/// All members delegate to <see cref="RoyalIdentity.Security.Passwords.PasswordHash"/>.
/// </summary>
public static class PasswordHash
{
    public static string Create(string password)
        => RoyalIdentity.Security.Passwords.PasswordHash.Create(password);

    public static bool Verify(string password, string hash)
    {
        var result = RoyalIdentity.Security.Passwords.PasswordHash.Verify(password, hash);
        return result is PasswordVerificationResult.Success or PasswordVerificationResult.SuccessRehashNeeded;
    }
}
