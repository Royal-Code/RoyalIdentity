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

        var options = serializer.Deserialize(row.PayloadVersion, row.PayloadJson);
        var redirectErrors = options.RedirectUriValidation?.Validate()
            ?? ["RedirectUriValidation must not be null."];
        if (redirectErrors.Count is not 0)
        {
            throw new InvalidOperationException(
                $"The persisted server options have invalid redirect URI validation options: {string.Join(" ", redirectErrors)}");
        }

        return options;
    }
}
