using Microsoft.EntityFrameworkCore;
using RoyalIdentity.Data.Configuration.Entities;
using RoyalIdentity.Options;
using RoyalIdentity.Storage.EntityFramework.Configuration.Materialization;

namespace RoyalIdentity.Storage.EntityFramework.Configuration.Stores;

internal sealed class ConfigurationServerOptionsReader(
	IConfigurationDbContextAccessor accessor,
	ServerOptionsPayloadSerializer serializer)
{
	public async Task<ServerOptions> ReadAsync(CancellationToken ct)
	{
		var row = await accessor.DbContext.Set<ServerOptionsEntity>()
			.AsNoTracking()
			.SingleOrDefaultAsync(entity => entity.Id == ServerOptionsEntity.SingletonId, ct)
			?? throw new InvalidOperationException(
				"The Configuration store has no server_options row. Run the migrations and seed before reading it.");

		return serializer.Deserialize(row.PayloadVersion, row.PayloadJson);
	}
}
