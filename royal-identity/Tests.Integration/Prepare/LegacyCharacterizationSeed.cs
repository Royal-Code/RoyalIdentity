using RoyalIdentity.Security.Passwords;
using RoyalIdentity.Users;
using System.Security.Claims;

namespace Tests.Integration.Prepare;

/// <summary>
/// Transitional fake-backed setup retained only for characterization groups scheduled for migration in Fase 6.
/// Delete this helper together with the default in-memory composition.
/// </summary>
internal static class LegacyCharacterizationSeed
{
    public static (string username, string password) SeedUser(
        MemoryStorage storage,
        RoyalIdentity.Models.Realm realm,
        bool active = true)
    {
        var username = $"char-{CryptoRandom.CreateUniqueId(8)}";
        storage.GetRealmMemoryStore(realm).UserAccounts[username] = new MemoryUserAccount
        {
            SubjectId = $"sub-{CryptoRandom.CreateUniqueId(16)}",
            Username = username,
            PasswordHash = PasswordHash.Create(CharacterizationSeed.DefaultPassword),
            DisplayName = $"Char {username}",
            IsActive = active,
            Claims = [new Claim("email", $"{username}@example.com")]
        };
        return (username, CharacterizationSeed.DefaultPassword);
    }

    public static UserSession? FindSession(
        MemoryStorage storage,
        RoyalIdentity.Models.Realm realm,
        string username)
    {
        var store = storage.GetRealmMemoryStore(realm);
        var details = store.UserAccounts.Values.FirstOrDefault(u => u.Username == username);
        return details is null
            ? null
            : store.UserSessions.Values.FirstOrDefault(s => s.SubjectId == details.SubjectId);
    }
}
