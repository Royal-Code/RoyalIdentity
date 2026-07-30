using RoyalIdentity.Storage.EntityFramework.Operational.Materialization;
using Tests.Storage.Operational.Support;

namespace Tests.Storage.Operational;

/// <summary>
/// Two things the durable backing must never do, neither of which the conflict scenarios can catch: report a
/// database failure as a replay, and change the persisted digest without anyone noticing.
/// </summary>
public class SqliteOperationalReplayProtectionFailureTests
{
    private static readonly DateTimeOffset Expiration =
        new(Tests.Storage.Support.StorageContractHarness.Start.AddMinutes(15), TimeSpan.Zero);

    private const string Purpose = "PrivateKeyJwtSecretEvaluator";
    private const string Issuer = "client-a";

    /// <summary>
    /// The store answers replay from a <c>DbUpdateException</c> only when the identity is genuinely present. A
    /// failure that is not a conflict has to reach the caller: reporting a database outage as a replay attack is
    /// both a lie and an alarm nobody can act on, and it would silently disable a security control for as long
    /// as the outage lasted.
    /// </summary>
    [Fact]
    public async Task AFailureThatIsNotAConflict_Propagates_InsteadOfAnsweringReplay()
    {
        var interceptor = new FailingInsertInterceptor("replay_handles");
        await using var database = await SqliteOperationalFileDatabase.CreateMigratedAsync(
            interceptor: interceptor);
        var realm = SqliteOperationalFileDatabase.NewRealm();

        await using var scope = database.CreateScope();
        var store = database.ReplayProtectionOf(scope);

        // Nothing was registered, so the confirmation query the store runs on failure finds nothing — the branch
        // that must rethrow rather than return false.
        interceptor.Arm();

        var error = await Assert.ThrowsAnyAsync<Exception>(() => store.TryAddAsync(
            realm.Id, Issuer, Purpose, "jti-1", Expiration, default));

        Assert.Contains(
            FailingInsertInterceptor.FailureMessage,
            error.ToString(),
            StringComparison.Ordinal);
        Assert.Equal(0, await database.CountAsync("replay_handles"));
    }

    /// <summary>
    /// <para>
    ///     Known-answer vector over the persisted format. The expected value was produced by an independent
    ///     implementation of the documented construction — <c>SHA-256</c> over, per field, a little-endian
    ///     <c>int32</c> length followed by the bytes; fields being the version as a little-endian <c>int32</c>,
    ///     the domain <c>replay_handles</c> in UTF-8, and the handle in UTF-8 — so it checks the construction and
    ///     not merely that the code agrees with itself.
    /// </para>
    /// <para>
    ///     Its standing job is narrower and just as important: the digest is written to the database, so any
    ///     later change to field order, endianness, encoding or the set of fields must fail here rather than
    ///     silently orphan every row already written.
    /// </para>
    /// </summary>
    [Fact]
    public void Digest_HasNotChangedShape()
    {
        Assert.Equal(1, ReplayHandleDigest.CurrentVersion);
        Assert.Equal(
            "c664482997eb0ca87c3e3e8edbf5734d0a59e88c0d77494b51aff9c3f653cd65",
            new ReplayHandleDigest().Compute("jti-reference-vector"));
    }

    /// <summary>
    /// The digest stands for the handle alone. Purpose separates rows as a column of the primary key, so folding
    /// it into the digest would add nothing to uniqueness while binding the persisted format to a value meant to
    /// stay queryable.
    /// </summary>
    [Fact]
    public void Digest_DependsOnTheHandleAlone()
    {
        var digest = new ReplayHandleDigest();

        Assert.Equal(digest.Compute("jti-1"), digest.Compute("jti-1"));
        Assert.NotEqual(digest.Compute("jti-1"), digest.Compute("jti-2"));
    }

    // Length-prefixing is what makes the encoding unambiguous: two handles that a naive concatenation could not
    // tell apart must still differ here.
    [Fact]
    public void Digest_SeparatesHandlesANaiveConcatenationWouldMerge()
    {
        var digest = new ReplayHandleDigest();

        Assert.NotEqual(digest.Compute("ab|c"), digest.Compute("ab|c "));
        Assert.NotEqual(digest.Compute("a|bc"), digest.Compute("ab|c"));
    }
}
