using System.Collections.Specialized;
using Microsoft.EntityFrameworkCore;
using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Data.Operational.Entities;
using RoyalIdentity.Models;
using RoyalIdentity.Storage.EntityFramework.Operational.Materialization;
using RoyalIdentity.Storage.EntityFramework.Operational.Stores;
using Tests.Storage.Operational.Support;
using RoyalIdentity.Storage.EntityFramework.Operational.Maintenance;

namespace Tests.Storage.Operational;

/// <summary>
/// Acceptances only the EF provider can satisfy for authorize parameters (plan Fase 6, MP-5/DF16/DF38): the
/// absolute TTL written at store time, the fail-closed read, realm isolation, handle entropy and the internal
/// regeneration on collision. The shared semantics live in the provider-neutral
/// <c>AuthorizeParametersStoreContractTests</c>.
/// </summary>
public abstract class OperationalAuthorizeParametersTests : OperationalParitySuite
{
    private static readonly OperationalLookupDigest Digest = new();

    private static NameValueCollection NewParameters(string clientId = "client-a")
        => new()
        {
            ["client_id"] = clientId,
            ["response_type"] = "code",
            ["scope"] = "openid profile",
        };

    private static async Task<List<AuthorizeParametersEntity>> RowsAsync(IOperationalParityHarness harness)
    {
        await using var context = harness.NewOperationalContext();

        return await context.Set<AuthorizeParametersEntity>().AsNoTracking().ToListAsync();
    }

    // DF16/DF40: the expiration is absolute, computed from the realm's interaction lifetime, in seconds.
    [Fact]
    public async Task Write_StoresAnAbsoluteExpiration_FromTheRealmLifetime()
    {
        await using var harness = await CreateHarnessAsync();
        harness.RealmA.Options.Authentication.AuthorizationInteractionLifetime = 120;
        var now = harness.Clock.GetUtcNow().UtcDateTime;

        await harness.Storage.GetAuthorizeParametersStore(harness.RealmA).WriteAsync(NewParameters(), default);

        var row = Assert.Single(await RowsAsync(harness));
        Assert.Equal(now, row.CreatedAtUtc);
        Assert.Equal(now.AddSeconds(120), row.ExpiresAtUtc);
    }

    // DF16: changing the option afterwards never reinterprets a record that already exists.
    [Fact]
    public async Task ChangingTheLifetime_DoesNotMoveAnAlreadyStoredExpiration()
    {
        await using var harness = await CreateHarnessAsync();
        harness.RealmA.Options.Authentication.AuthorizationInteractionLifetime = 60;
        var handle = await harness.Storage.GetAuthorizeParametersStore(harness.RealmA)
            .WriteAsync(NewParameters(), default);
        var expiresAt = Assert.Single(await RowsAsync(harness)).ExpiresAtUtc;

        harness.RealmA.Options.Authentication.AuthorizationInteractionLifetime = 86400;

        Assert.Equal(expiresAt, Assert.Single(await RowsAsync(harness)).ExpiresAtUtc);
        // Still readable inside the original window.
        Assert.NotNull(await harness.Storage.GetAuthorizeParametersStore(harness.RealmA).ReadAsync(handle, default));
    }

    // AP-02 is the one fail-closed read of the family: past the window the record is absent, and the lazy
    // cleanup removes it — but the answer would have been null either way.
    [Fact]
    public async Task Read_AfterTheWindow_IsFailClosed_AndRemovesTheRecord()
    {
        await using var harness = await CreateHarnessAsync();
        harness.RealmA.Options.Authentication.AuthorizationInteractionLifetime = 60;
        var store = harness.Storage.GetAuthorizeParametersStore(harness.RealmA);
        var handle = await store.WriteAsync(NewParameters(), default);

        Assert.NotNull(await store.ReadAsync(handle, default));

        harness.Clock.Advance(TimeSpan.FromSeconds(61));

        Assert.Null(await store.ReadAsync(handle, default));
        Assert.Empty(await RowsAsync(harness));
    }

    // AP-02: within the window the read is repeatable — the flow reads the handle more than once before the
    // callback deletes it.
    [Fact]
    public async Task Read_WithinTheWindow_IsRepeatable()
    {
        await using var harness = await CreateHarnessAsync();
        harness.RealmA.Options.Authentication.AuthorizationInteractionLifetime = 600;
        var store = harness.Storage.GetAuthorizeParametersStore(harness.RealmA);
        var handle = await store.WriteAsync(NewParameters(), default);

        Assert.NotNull(await store.ReadAsync(handle, default));
        harness.Clock.Advance(TimeSpan.FromSeconds(300));
        Assert.NotNull(await store.ReadAsync(handle, default));
        Assert.Single(await RowsAsync(harness));
    }

    // DF38: only the digest is persisted; the handle itself lives in the redirect URL and nowhere else.
    [Fact]
    public async Task Write_PersistsOnlyTheDigestOfTheHandle()
    {
        await using var harness = await CreateHarnessAsync();

        var handle = await harness.Storage.GetAuthorizeParametersStore(harness.RealmA)
            .WriteAsync(NewParameters("secret-client"), default);

        var row = Assert.Single(await RowsAsync(harness));
        Assert.Equal(Digest.Compute(OperationalRecordTypes.AuthorizeParameters, handle), row.HandleDigest);
        Assert.DoesNotContain(handle, row.HandleDigest, StringComparison.Ordinal);
        // The parameters themselves are inside the protected payload, not in a readable column.
        Assert.DoesNotContain("secret-client", row.ProtectedPayload, StringComparison.Ordinal);
    }

    // DF16: at least 128 bits of entropy, so the handle is not guessable.
    [Fact]
    public async Task Write_ProducesAHandleWithAtLeast128Bits()
    {
        await using var harness = await CreateHarnessAsync();
        var store = harness.Storage.GetAuthorizeParametersStore(harness.RealmA);

        var handles = new List<string>();
        for (var index = 0; index < 20; index++)
            handles.Add(await store.WriteAsync(NewParameters(), default));

        Assert.Equal(handles.Count, handles.Distinct(StringComparer.Ordinal).Count());
        // base64url of 16 bytes: 22 characters, i.e. 128 bits of entropy.
        Assert.All(handles, handle => Assert.True(handle.Length >= 22, $"handle too short: {handle.Length}"));
    }

    // DF5: a handle of one realm never resolves in another.
    [Fact]
    public async Task AHandleOfOneRealm_DoesNotResolveInAnother()
    {
        await using var harness = await CreateHarnessAsync();
        var handle = await harness.Storage.GetAuthorizeParametersStore(harness.RealmA)
            .WriteAsync(NewParameters(), default);

        Assert.Null(await harness.Storage.GetAuthorizeParametersStore(harness.RealmB).ReadAsync(handle, default));
        Assert.NotNull(await harness.Storage.GetAuthorizeParametersStore(harness.RealmA).ReadAsync(handle, default));
    }

    // AP-01: a colliding handle is regenerated internally — never an overwrite, never a random failure.
    [Fact]
    public async Task Write_OnCollision_RegeneratesInsteadOfOverwritingOrFailing()
    {
        var generator = new ScriptedHandleGenerator(["collision", "collision", "unique"]);
        await using var harness = await CreateHarnessAsync(generator);
        var store = harness.Storage.GetAuthorizeParametersStore(harness.RealmA);

        var first = await store.WriteAsync(NewParameters("first"), default);
        var second = await store.WriteAsync(NewParameters("second"), default);

        Assert.Equal("collision", first);
        Assert.Equal("unique", second);
        Assert.Equal(2, (await RowsAsync(harness)).Count);
        // The record it collided with is intact.
        Assert.Equal("first", (await store.ReadAsync(first, default))!["client_id"]);
        Assert.Equal("second", (await store.ReadAsync(second, default))!["client_id"]);
    }

    // DF9: repeated keys and the whole collection survive the round-trip.
    [Fact]
    public async Task Write_RoundTripsRepeatedKeys()
    {
        await using var harness = await CreateHarnessAsync();
        var parameters = NewParameters();
        parameters.Add("resource", "https://api.example/orders");
        parameters.Add("resource", "https://api.example/invoices");
        var store = harness.Storage.GetAuthorizeParametersStore(harness.RealmA);

        var handle = await store.WriteAsync(parameters, default);
        var restored = await store.ReadAsync(handle, default);

        Assert.NotNull(restored);
        Assert.Equal(parameters.Count, restored.Count);
        Assert.Equal(
            ["https://api.example/orders", "https://api.example/invoices"],
            restored.GetValues("resource")!);
    }

    // AP-03: the callback delete is idempotent.
    [Fact]
    public async Task Delete_IsIdempotent()
    {
        await using var harness = await CreateHarnessAsync();
        var store = harness.Storage.GetAuthorizeParametersStore(harness.RealmA);
        var handle = await store.WriteAsync(NewParameters(), default);

        await store.DeleteAsync(handle, default);
        await store.DeleteAsync(handle, default);
        await store.DeleteAsync("never-issued", default);

        Assert.Null(await store.ReadAsync(handle, default));
        Assert.Empty(await RowsAsync(harness));
    }

    /// <summary>A generator that hands out scripted handles, so a collision is staged rather than waited for.</summary>
    private sealed class ScriptedHandleGenerator(IReadOnlyList<string> handles) : IAuthorizeParametersHandleGenerator
    {
        private int index;

        public string Generate() => handles[Math.Min(index++, handles.Count - 1)];
    }

    /// <summary>SQLite runs this suite unconditionally; it is the baseline the other provider must match.</summary>
    public sealed class Sqlite : OperationalAuthorizeParametersTests
    {
        private protected override Task<IOperationalParityHarness> CreateHarnessAsync(
            IAuthorizeParametersHandleGenerator? handleGenerator = null,
            Action<OperationalCleanupOptions>? cleanup = null)
            => SqliteParityHarness.CreateAsync(handleGenerator, cleanup);
    }
}

/// <summary>
/// The same suite over PostgreSQL. The concrete suite stays private so xUnit does not discover its scenarios
/// when the opt-in connection is unavailable.
/// </summary>
public class PostgreSqlAuthorizeParametersTests
{
    [Tests.Storage.Configuration.StoragePostgreSqlFact]
    [Trait("Category", "PostgreSql")]
    public Task AuthorizeParameters()
        => Tests.Storage.Configuration.Support.ProviderFactRunner.RunAsync(new PostgreSqlSuite());

    private sealed class PostgreSqlSuite : OperationalAuthorizeParametersTests
    {
        private protected override Task<IOperationalParityHarness> CreateHarnessAsync(
            IAuthorizeParametersHandleGenerator? handleGenerator = null,
            Action<OperationalCleanupOptions>? cleanup = null)
            => PostgreSqlParityHarness.CreateAsync(handleGenerator, cleanup);
    }
}
