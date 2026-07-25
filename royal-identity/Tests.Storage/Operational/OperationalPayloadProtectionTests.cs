using System.Security.Cryptography;
using Microsoft.Extensions.Logging.Abstractions;
using RoyalIdentity.Models;
using RoyalIdentity.Options;
using RoyalIdentity.Storage.EntityFramework.Operational.Materialization;
using RoyalIdentity.Storage.EntityFramework.Operational.Protection;

namespace Tests.Storage.Operational;

/// <summary>
/// Realm-selected operational payload protection (plan-data-operational-storage DF30): a realm names a
/// profile, the envelope records the profile that actually wrote the record, a rotation only needs the
/// previous profile to stay registered as a reader, and every miss — unregistered profile, tampered envelope,
/// context mismatch, missing reader — fails closed without ever falling back to unprotected storage.
/// </summary>
public class OperationalPayloadProtectionTests
{
    private const string Payload = """{"ClientId":"client-one"}""";

    private static readonly byte[] KeyOne = RandomNumberGenerator.GetBytes(32);
    private static readonly byte[] KeyTwo = RandomNumberGenerator.GetBytes(32);

    private static Realm NewRealm(string id, string profileId)
    {
        var realm = new Realm(id, $"{id}.test", id, id, false, new RealmOptions(new ServerOptions()));
        realm.Options.OperationalStorage.PayloadProtectionProfile = profileId;

        return realm;
    }

    private static OperationalProtectionContext NewContext(
        string realmId = "realm-a",
        string recordType = OperationalRecordTypes.RefreshToken,
        string lookupKey = "digest-one",
        int payloadVersion = 1)
        => new(realmId, recordType, lookupKey, payloadVersion);

    private static OperationalPayloadProtection NewProtection(params IOperationalPayloadProtector[] profiles)
        => new(new OperationalPayloadProtectorResolver(profiles));

    [Fact]
    public async Task Protect_ThenUnprotect_RoundTripsThroughTheRealmProfile()
    {
        var protection = NewProtection(new AesGcmOperationalPayloadProtector("default", KeyOne));
        var realm = NewRealm("realm-a", "default");
        var context = NewContext();

        var persisted = await protection.ProtectAsync(realm, Payload, context);
        var restored = await protection.UnprotectAsync(persisted, context);

        Assert.Equal(Payload, restored);
        Assert.DoesNotContain("client-one", persisted, StringComparison.Ordinal);
        Assert.StartsWith("v1:default:", persisted, StringComparison.Ordinal);
    }

    // Two realms, two profiles: neither can read the other's records, so a profile is never shared implicitly.
    [Fact]
    public async Task TwoRealms_WithDifferentProfiles_DoNotShareKeys()
    {
        var protection = NewProtection(
            new AesGcmOperationalPayloadProtector("profile-a", KeyOne),
            new AesGcmOperationalPayloadProtector("profile-b", KeyTwo));

        var realmA = NewRealm("realm-a", "profile-a");
        var realmB = NewRealm("realm-b", "profile-b");

        var persistedA = await protection.ProtectAsync(realmA, Payload, NewContext("realm-a"));
        var persistedB = await protection.ProtectAsync(realmB, Payload, NewContext("realm-b"));

        Assert.StartsWith("v1:profile-a:", persistedA, StringComparison.Ordinal);
        Assert.StartsWith("v1:profile-b:", persistedB, StringComparison.Ordinal);
        Assert.Equal(Payload, await protection.UnprotectAsync(persistedA, NewContext("realm-a")));
        Assert.Equal(Payload, await protection.UnprotectAsync(persistedB, NewContext("realm-b")));
    }

    // Rotation: the realm starts writing with the new profile, and records written before stay readable
    // because the previous profile is still registered.
    [Fact]
    public async Task Rotation_KeepsPreviousRecordsReadable_AndWritesWithTheNewProfile()
    {
        var protection = NewProtection(
            new AesGcmOperationalPayloadProtector("profile-old", KeyOne),
            new AesGcmOperationalPayloadProtector("profile-new", KeyTwo));

        var realm = NewRealm("realm-a", "profile-old");
        var context = NewContext();
        var beforeRotation = await protection.ProtectAsync(realm, Payload, context);

        realm.Options.OperationalStorage.PayloadProtectionProfile = "profile-new";
        var afterRotation = await protection.ProtectAsync(realm, Payload, context);

        Assert.StartsWith("v1:profile-old:", beforeRotation, StringComparison.Ordinal);
        Assert.StartsWith("v1:profile-new:", afterRotation, StringComparison.Ordinal);
        Assert.Equal(Payload, await protection.UnprotectAsync(beforeRotation, context));
        Assert.Equal(Payload, await protection.UnprotectAsync(afterRotation, context));
    }

    // A profile the composition did not register fails closed — it never silently falls back to Plain.
    [Fact]
    public async Task UnregisteredProfile_FailsClosed()
    {
        var protection = NewProtection(
            new AesGcmOperationalPayloadProtector("default", KeyOne),
            new PlainOperationalPayloadProtector("plain", NullLogger<PlainOperationalPayloadProtector>.Instance));

        var realm = NewRealm("realm-a", "missing-profile");

        await Assert.ThrowsAsync<OperationalPayloadProtectionException>(
            async () => await protection.ProtectAsync(realm, Payload, NewContext()));
    }

    // Dropping the profile that wrote a record makes it unreadable — loudly, not silently.
    [Fact]
    public async Task RemovingTheWritingProfile_MakesTheRecordFailClosed()
    {
        var realm = NewRealm("realm-a", "profile-old");
        var context = NewContext();
        var persisted = await NewProtection(new AesGcmOperationalPayloadProtector("profile-old", KeyOne))
            .ProtectAsync(realm, Payload, context);

        var afterDrop = NewProtection(new AesGcmOperationalPayloadProtector("profile-new", KeyTwo));

        await Assert.ThrowsAsync<OperationalPayloadProtectionException>(
            async () => await afterDrop.UnprotectAsync(persisted, context));
    }

    [Fact]
    public async Task TamperedPayload_FailsClosed()
    {
        var protection = NewProtection(new AesGcmOperationalPayloadProtector("default", KeyOne));
        var realm = NewRealm("realm-a", "default");
        var context = NewContext();

        var persisted = await protection.ProtectAsync(realm, Payload, context);
        var envelope = OperationalPayloadEnvelope.Parse(persisted);
        var tamperedBody = envelope.Payload[..^4] + (envelope.Payload.EndsWith("AAAA", StringComparison.Ordinal) ? "BBBB" : "AAAA");
        var tampered = new OperationalPayloadEnvelope(envelope.ProtectorId, tamperedBody).ToPersistedValue();

        await Assert.ThrowsAsync<OperationalPayloadProtectionException>(
            async () => await protection.UnprotectAsync(tampered, context));
    }

    // The authenticated context is realm + record type + lookup key + payload version: changing any of them
    // makes the record unreadable, so a payload cannot be replayed into another row.
    public static TheoryData<string, OperationalProtectionContext> MismatchedContexts() => new()
    {
        { "realm", new OperationalProtectionContext("realm-b", OperationalRecordTypes.RefreshToken, "digest-one", 1) },
        { "record type", new OperationalProtectionContext("realm-a", OperationalRecordTypes.AccessToken, "digest-one", 1) },
        { "lookup key", new OperationalProtectionContext("realm-a", OperationalRecordTypes.RefreshToken, "digest-two", 1) },
        { "payload version", new OperationalProtectionContext("realm-a", OperationalRecordTypes.RefreshToken, "digest-one", 2) },
    };

    [Theory]
    [MemberData(nameof(MismatchedContexts))]
    public async Task MismatchedContext_FailsClosed(string _, OperationalProtectionContext other)
    {
        var protection = NewProtection(new AesGcmOperationalPayloadProtector("default", KeyOne));
        var realm = NewRealm("realm-a", "default");

        var persisted = await protection.ProtectAsync(realm, Payload, NewContext());

        await Assert.ThrowsAsync<OperationalPayloadProtectionException>(
            async () => await protection.UnprotectAsync(persisted, other));
    }

    [Fact]
    public async Task MalformedEnvelope_FailsClosed()
    {
        var protection = NewProtection(new AesGcmOperationalPayloadProtector("default", KeyOne));

        await Assert.ThrowsAsync<OperationalPayloadProtectionException>(
            async () => await protection.UnprotectAsync("not-an-envelope", NewContext()));
    }

    [Fact]
    public async Task UnknownEnvelopeVersion_FailsClosed()
    {
        var protection = NewProtection(new AesGcmOperationalPayloadProtector("default", KeyOne));

        await Assert.ThrowsAsync<OperationalPayloadProtectionException>(
            async () => await protection.UnprotectAsync("v99:default:body", NewContext()));
    }

    // Plain exists only when the composition registers it AND a realm selects it — two deliberate opt-ins.
    [Fact]
    public async Task Plain_WorksOnlyWhenExplicitlyRegisteredAndSelected()
    {
        var protection = NewProtection(
            new AesGcmOperationalPayloadProtector("default", KeyOne),
            new PlainOperationalPayloadProtector("plain", NullLogger<PlainOperationalPayloadProtector>.Instance));

        var realm = NewRealm("realm-a", "plain");
        var context = NewContext();

        var persisted = await protection.ProtectAsync(realm, Payload, context);

        Assert.Equal($"v1:plain:{Payload}", persisted);
        Assert.Equal(Payload, await protection.UnprotectAsync(persisted, context));
    }

    // A record written by an AES profile is never opened by Plain just because Plain happens to be registered.
    [Fact]
    public async Task Plain_DoesNotOpenRecordsOfAnotherProfile()
    {
        var realm = NewRealm("realm-a", "default");
        var context = NewContext();
        var persisted = await NewProtection(new AesGcmOperationalPayloadProtector("default", KeyOne))
            .ProtectAsync(realm, Payload, context);

        var plainOnly = NewProtection(
            new PlainOperationalPayloadProtector("plain", NullLogger<PlainOperationalPayloadProtector>.Instance));

        await Assert.ThrowsAsync<OperationalPayloadProtectionException>(
            async () => await plainOnly.UnprotectAsync(persisted, context));
    }

    [Fact]
    public void DuplicateProfileIds_FailTheComposition()
        => Assert.Throws<OperationalPayloadProtectionException>(() => new OperationalPayloadProtectorResolver(
            [
                new AesGcmOperationalPayloadProtector("default", KeyOne),
                new AesGcmOperationalPayloadProtector("default", KeyTwo),
            ]));

    // DF28: the envelope's own diagnostics never expose the payload.
    [Fact]
    public void EnvelopeToString_RedactsThePayload()
    {
        var envelope = new OperationalPayloadEnvelope("default", "secret-body");

        Assert.DoesNotContain("secret-body", envelope.ToString(), StringComparison.Ordinal);
        Assert.Contains("REDACTED", envelope.ToString(), StringComparison.Ordinal);
    }

    // DF38: the lookup digest is domain-separated by record type and never carries the raw value.
    [Fact]
    public void LookupDigest_IsDeterministicAndDomainSeparated()
    {
        var digest = new OperationalLookupDigest();

        var refreshDigest = digest.Compute(OperationalRecordTypes.RefreshToken, "handle-value");
        var codeDigest = digest.Compute(OperationalRecordTypes.AuthorizationCode, "handle-value");

        Assert.Equal(refreshDigest, digest.Compute(OperationalRecordTypes.RefreshToken, "handle-value"));
        Assert.NotEqual(refreshDigest, codeDigest);
        Assert.DoesNotContain("handle-value", refreshDigest, StringComparison.Ordinal);
        Assert.Equal(64, refreshDigest.Length);
        // Ordinal: a different casing is a different handle.
        Assert.NotEqual(refreshDigest, digest.Compute(OperationalRecordTypes.RefreshToken, "Handle-Value"));
    }
}
