namespace Tests.Integration.Prepare;

/// <summary>
/// Provider-neutral setup helpers for characterization tests running over
/// <see cref="PersistentStorageAppFactory"/>.
/// </summary>
internal static class CharacterizationSeed
{
    public const string DefaultPassword = "char-pass";

    public static async Task<TestSubjectHandle> SeedUserAsync(
        PersistentStorageAppFactory factory,
        TestRealmHandle realm,
        bool active = true,
        CancellationToken ct = default)
    {
        var suffix = CryptoRandom.CreateUniqueId(12);
        var subject = new TestSubjectHandle(
            $"sub-{suffix}",
            $"char-{suffix}",
            DefaultPassword);
        await factory.SeedAccountAsync(realm, subject, active, ct);
        return subject;
    }

    public static async Task<PersistentSessionState?> FindSessionAsync(
        PersistentStorageAppFactory factory,
        TestRealmHandle realm,
        TestSubjectHandle subject,
        CancellationToken ct = default)
    {
        var sessions = await factory.FindSessionsAsync(realm, subject, ct);
        return sessions.LastOrDefault();
    }

    /// <summary>Posts the test-host login form and returns the raw response (does not throw on failure).</summary>
    public static Task<HttpResponseMessage> PostLoginAsync(
        HttpClient client, string username, string password, string realm = "demo")
        => client.PostAsync($"{realm}/test/account/login", new FormUrlEncodedContent(
            new Dictionary<string, string> { ["username"] = username, ["password"] = password }));
}
