using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging.Abstractions;
using RoyalIdentity.Contracts.Defaults;
using RoyalIdentity.Contracts.Models.Messages;
using RoyalIdentity.Contracts.Storage;

namespace Tests.Storage.Contracts;

/// <summary>
/// Contract of <c>IMessageStore</c> (matrix MS-01..MS-03), adjacent infrastructure (DF14 — not `Data.*`).
/// Fase 5 closed the semantics: the id returned by write reads back the same message (roundtrip), an
/// unreadable/tampered id reads back as null (fail-closed — asserted below), and the delete effect is
/// implementation-defined (`descartar` as parity criterion; definitive semantics follow the future
/// persistent message store — PAR backlog).
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

	// MS-02 (Fase 5/DF25): an unreadable/tampered id reads back as null — fail-closed as absence is a
	// security rule of the message store contract.
	[Fact]
	public async Task Read_UnreadableId_ReturnsNull()
	{
		var store = CreateStore();

		var read = await store.ReadAsync<ContractPayload>("contract-not-a-valid-protected-payload", default);

		Assert.Null(read);
	}

	// MS-03 (Fase 5): deleting a previously written id completes without error. The delete effect is
	// implementation-defined (`descartar` as a parity criterion); the definitive semantics follow the
	// future persistent message store (PAR backlog).
	[Fact]
	public async Task Delete_OfWrittenId_CompletesWithoutError()
	{
		var store = CreateStore();
		var id = await store.WriteAsync(new Message<ContractPayload>(new ContractPayload("x", 1), 1L), default);

		await store.DeleteAsync(id, default);
	}

	public sealed class ProtectedData : MessageStoreContractTests
	{
		protected override IMessageStore CreateStore()
			=> new ProtectedDataMessageStore(
				new EphemeralDataProtectionProvider(),
				NullLogger<ProtectedDataMessageStore>.Instance);
	}
}
