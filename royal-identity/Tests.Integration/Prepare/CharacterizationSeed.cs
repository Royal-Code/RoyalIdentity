using RoyalIdentity.Models;
using RoyalIdentity.Users;
using RoyalIdentity.Users.Contracts;
using RoyalIdentity.Utils;
using System.Security.Claims;

namespace Tests.Integration.Prepare;

/// <summary>
/// Helpers for the Fase 2 characterization tests (plan-users-edge-session.md).
/// <para>
/// The in-memory alice/bob accounts are shared mutable state across the whole <see cref="AppFactory"/>
/// (singleton storage). Tests that mutate user state (failure counters, lockout, active flag) must NOT
/// touch alice/bob or they would contaminate other test classes. These helpers seed a fresh, uniquely
/// named user per call and let a test inspect the realm-scoped session store.
/// </para>
/// </summary>
internal static class CharacterizationSeed
{
    public const string DefaultPassword = "char-pass";

    /// <summary>Seeds a uniquely named user into the given realm's in-memory store.</summary>
    public static (string username, string password) SeedUser(
        MemoryStorage storage, RoyalIdentity.Models.Realm realm, bool active = true)
    {
        var username = $"char-{CryptoRandom.CreateUniqueId(8)}";
        storage.GetRealmMemoryStore(realm).UsersDetails[username] = new UserDetails
        {
            SubjectId = $"sub-{CryptoRandom.CreateUniqueId(16)}", // stable id, intentionally != username
            Username = username,
            PasswordHash = PasswordHash.Create(DefaultPassword),
            DisplayName = $"Char {username}",
            IsActive = active,
            Claims = [new Claim("email", $"{username}@example.com")]
        };
        return (username, DefaultPassword);
    }

    /// <summary>Reads back the mutable details record (failure counters, active flag, ...).</summary>
    public static UserDetails GetDetails(MemoryStorage storage, RoyalIdentity.Models.Realm realm, string username)
        => storage.GetRealmMemoryStore(realm).UsersDetails[username];

    /// <summary>Finds the (single) session created for the given user in the realm session store.</summary>
    public static UserSession? FindSession(MemoryStorage storage, RoyalIdentity.Models.Realm realm, string username)
    {
        var store = storage.GetRealmMemoryStore(realm);
        var details = store.UsersDetails.Values.FirstOrDefault(u => u.Username == username);
        return details is null
            ? null
            : store.UserSessions.Values.FirstOrDefault(s => s.SubjectId == details.SubjectId);
    }

    /// <summary>Posts the test-host login form and returns the raw response (does not throw on failure).</summary>
    public static Task<HttpResponseMessage> PostLoginAsync(
        HttpClient client, string username, string password, string realm = "demo")
        => client.PostAsync($"{realm}/test/account/login", new FormUrlEncodedContent(
            new Dictionary<string, string> { ["username"] = username, ["password"] = password }));
}
