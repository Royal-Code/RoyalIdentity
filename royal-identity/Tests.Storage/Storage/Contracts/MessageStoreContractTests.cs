using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging.Abstractions;
using RoyalIdentity.Contracts.Defaults;
using RoyalIdentity.Contracts.Models.Messages;
using RoyalIdentity.Contracts.Storage;

namespace Tests.Storage.Contracts;

/// <summary>
/// Contract of <c>IMessageStore</c> (matrix MS-01..MS-03), adjacent infrastructure (DF14 — not `Data.*`).
/// Only the identifier roundtrip is locked: whatever the implementation (self-contained protected payload
/// today, a possible persistent store later), the id returned by write must read back the same message.
/// The current no-op delete and fail-closed read remain `avaliar` (Fase 5) and are not asserted as contract.
/// </summary>
public abstract class MessageStoreContractTests
{
	protected abstract IMessageStore CreateStore();

	protected sealed record ContractPayload(string Name, int Value);

	// MS-01 + MS-02: the id returned by write reads back an equal message.
	[Fact]
	public async Task Write_ThenRead_RoundtripsTheMessage()
	{
		var store = CreateStore();
		var message = new Message<ContractPayload>(new ContractPayload("logout", 42), 638_000_000_000_000_000L);

		var id = await store.WriteAsync(message, default);
		var read = await store.ReadAsync<ContractPayload>(id, default);

		Assert.False(string.IsNullOrEmpty(id));
		Assert.NotNull(read);
		Assert.Equal(message.Created, read.Created);
		Assert.Equal(message.Data, read.Data);
	}

	// MS-03: delete completes without error for any id (no-op today; persistent semantics — Fase 5/backlog).
	[Fact]
	public async Task Delete_CompletesWithoutError()
	{
		var store = CreateStore();
		var id = await store.WriteAsync(new Message<ContractPayload>(new ContractPayload("x", 1), 1L), default);

		await store.DeleteAsync(id, default);
		await store.DeleteAsync("contract-unknown-id", default);
	}

	public sealed class ProtectedData : MessageStoreContractTests
	{
		protected override IMessageStore CreateStore()
			=> new ProtectedDataMessageStore(
				new EphemeralDataProtectionProvider(),
				NullLogger<ProtectedDataMessageStore>.Instance);
	}
}
