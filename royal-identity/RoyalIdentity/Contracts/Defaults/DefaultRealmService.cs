// Ignore Spelling: Accessor

using Microsoft.AspNetCore.Http;
using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Extensions;
using RoyalIdentity.Options;

namespace RoyalIdentity.Contracts.Defaults;

public class DefaultRealmService : IRealmService
{
    private readonly IHttpContextAccessor httpContextAccessor;
    private readonly IRealmStore realmStore;

    public DefaultRealmService(IHttpContextAccessor httpContextAccessor, IRealmStore realmStore)
    {
        this.httpContextAccessor = httpContextAccessor;
        this.realmStore = realmStore;
    }

    public async ValueTask<RealmOptions> GetOptionsAsync()
    {
        var context = httpContextAccessor.HttpContext
            ?? throw new InvalidOperationException("HttpContext is not available.");

        if (context.Items.TryGetValue(Constants.RealmOptionsKey, out var item) && item is RealmOptions options)
        {
            return options;
        }

        var realmPath = context.GetRealmPath()
            ?? throw new InvalidOperationException("Realm path is not available.");

        options = await GetOptionsAsync(realmPath, context.RequestAborted);
        context.Items.Add(Constants.RealmOptionsKey, options);

        return options;
    }

    public async ValueTask<RealmOptions> GetOptionsAsync(string realmPath, CancellationToken ct)
    {
        var realm = await realmStore.GetByPathAsync(realmPath, ct)
            ?? throw new InvalidOperationException($"Realm not found: {realmPath}");

        return realm.Options;
    }
}
