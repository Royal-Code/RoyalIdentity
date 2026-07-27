using RoyalIdentity.Configuration;
using RoyalIdentity.Models;
using RoyalIdentity.Options;
using RoyalIdentity.Storage.EntityFramework.Operational.Protection;

namespace Tests.Storage.Operational;

public class OperationalPayloadProfilesStartupValidatorTests
{
    [Fact]
    public async Task StartAsync_RejectsAnUnavailableProfileSelectedByAnEnabledRealm()
    {
        var realm = NewRealm("missing-profile");
        var validator = new OperationalPayloadProfilesStartupValidator(
            new Snapshot(isLoaded: true, [realm]),
            new OperationalPayloadProtectorResolver([]));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => validator.StartAsync(default));

        Assert.Contains(realm.Id, exception.Message, StringComparison.Ordinal);
        Assert.Contains("missing-profile", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task StartAsync_AcceptsEveryRegisteredProfile_AndIgnoresDisabledRealms()
    {
        var enabled = NewRealm("registered-profile");
        var disabled = NewRealm("unavailable-but-disabled");
        disabled.Enabled = false;
        var validator = new OperationalPayloadProfilesStartupValidator(
            new Snapshot(isLoaded: true, [enabled, disabled]),
            new OperationalPayloadProtectorResolver([new StubProtector("registered-profile")]));

        await validator.StartAsync(default);
    }

    [Fact]
    public async Task StartAsync_RequiresTheSnapshotToBeLoadedFirst()
    {
        var validator = new OperationalPayloadProfilesStartupValidator(
            new Snapshot(isLoaded: false, []),
            new OperationalPayloadProtectorResolver([]));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => validator.StartAsync(default));

        Assert.Contains("must be loaded", exception.Message, StringComparison.Ordinal);
    }

    private static Realm NewRealm(string profileId)
    {
        var options = new RealmOptions(new ServerOptions());
        options.OperationalStorage.PayloadProtectionProfile = profileId;
        return new Realm($"realm-{profileId}", "realm.example", profileId, "Realm", false, options);
    }

    private sealed class Snapshot(bool isLoaded, IReadOnlyCollection<Realm> realms) : IConfigurationSnapshot
    {
        public bool IsLoaded => isLoaded;

        public ServerOptions ServerOptions => new();

        public Realm? FindRealmByPath(string path) => realms.SingleOrDefault(realm => realm.Path == path);

        public IReadOnlyCollection<string> RealmPaths => realms.Select(realm => realm.Path).ToArray();

        public DateTimeOffset LoadedAtUtc => default;

        public DateTimeOffset? LastRefreshFailureUtc => null;
    }

    private sealed class StubProtector(string profileId) : IOperationalPayloadProtector
    {
        public string ProfileId => profileId;

        public ValueTask<string> ProtectAsync(
            string payload,
            OperationalProtectionContext context,
            CancellationToken ct = default)
            => ValueTask.FromResult(payload);

        public ValueTask<string> UnprotectAsync(
            string protectedPayload,
            OperationalProtectionContext context,
            CancellationToken ct = default)
            => ValueTask.FromResult(protectedPayload);
    }
}
