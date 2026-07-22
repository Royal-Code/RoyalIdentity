using System.Collections.Specialized;
using Tests.Storage.Support;

namespace Tests.Storage.Contracts;

/// <summary>
/// Contract of <c>IAuthorizeParametersStore</c> (matrix AP-01..AP-03): server-side operational state between
/// the authorize redirect and the callback (`preservar` — DF14). The store is global in the current contract
/// (no realm binding — Fase 2); partitioning/expiration decisions belong to Plano 3.
/// </summary>
public abstract class AuthorizeParametersStoreContractTests : StorageContractTests
{
	private static NameValueCollection NewParameters(string clientId)
		=> new()
		{
			["client_id"] = clientId,
			["response_type"] = "code",
			["scope"] = "openid profile",
		};

	// AP-01 + AP-02: writing returns a handle that reads back the stored parameters.
	[Fact]
	public async Task Write_ThenRead_ReturnsTheStoredParameters()
	{
		await using var harness = await CreateHarnessAsync();
		var store = harness.Storage.AuthorizeParameters;

		var handle = await store.WriteAsync(NewParameters("contract-client"), default);
		var read = await store.ReadAsync(handle, default);

		Assert.False(string.IsNullOrEmpty(handle));
		Assert.NotNull(read);
		Assert.Equal("contract-client", read["client_id"]);
		Assert.Equal("code", read["response_type"]);
		Assert.Equal("openid profile", read["scope"]);
	}

	// AP-02: reading does not consume — the authorize flow reads the handle more than once before the
	// callback deletes it (required by the current flow).
	[Fact]
	public async Task Read_DoesNotConsumeTheEntry()
	{
		await using var harness = await CreateHarnessAsync();
		var store = harness.Storage.AuthorizeParameters;
		var handle = await store.WriteAsync(NewParameters("contract-client"), default);

		var first = await store.ReadAsync(handle, default);
		var second = await store.ReadAsync(handle, default);

		Assert.NotNull(first);
		Assert.NotNull(second);
	}

	// AP-02: absent handle returns null (final absence semantics — Fase 5, DF25).
	[Fact]
	public async Task Read_UnknownHandle_ReturnsNull()
	{
		await using var harness = await CreateHarnessAsync();

		var read = await harness.Storage.AuthorizeParameters.ReadAsync("contract-unknown-handle", default);

		Assert.Null(read);
	}

	// AP-03 `preservar`: the callback cleanup removes the entry; deleting an absent handle completes.
	[Fact]
	public async Task Delete_ThenRead_ReturnsNull_AndDeletingAbsentCompletes()
	{
		await using var harness = await CreateHarnessAsync();
		var store = harness.Storage.AuthorizeParameters;
		var handle = await store.WriteAsync(NewParameters("contract-client"), default);

		await store.DeleteAsync(handle, default);
		await store.DeleteAsync(handle, default);

		Assert.Null(await store.ReadAsync(handle, default));
	}

	// AP-01: each write produces its own handle identifying its own entry.
	[Fact]
	public async Task Write_TwoEntries_ProducesDistinctHandles_EachReadingItsOwnEntry()
	{
		await using var harness = await CreateHarnessAsync();
		var store = harness.Storage.AuthorizeParameters;

		var handleOne = await store.WriteAsync(NewParameters("client-one"), default);
		var handleTwo = await store.WriteAsync(NewParameters("client-two"), default);

		Assert.NotEqual(handleOne, handleTwo);
		Assert.Equal("client-one", (await store.ReadAsync(handleOne, default))!["client_id"]);
		Assert.Equal("client-two", (await store.ReadAsync(handleTwo, default))!["client_id"]);
	}

	public sealed class InMemory : AuthorizeParametersStoreContractTests
	{
		protected override Task<StorageContractHarness> CreateHarnessAsync() => InMemoryStorageHarness.CreateAsync();
	}
}
