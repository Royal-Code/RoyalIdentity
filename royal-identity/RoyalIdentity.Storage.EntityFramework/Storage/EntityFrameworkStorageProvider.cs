using Microsoft.Extensions.DependencyInjection;
using RoyalIdentity.Contracts.Storage;

namespace RoyalIdentity.Storage.EntityFramework.Storage;

/// <summary>
/// Creates a storage lifetime for a caller outside a request — a hosted worker, a background job. Each session
/// is a real DI scope, which is what gives it its own <c>DbContext</c> per family (plan DF3).
/// </summary>
internal sealed class EntityFrameworkStorageProvider(IServiceScopeFactory scopeFactory) : IStorageProvider
{
    public IStorageSession CreateSession() => new EntityFrameworkStorageSession(scopeFactory.CreateScope());
}

/// <summary>
/// A storage lifetime, <b>not</b> a global unit of work (plan DF3/DF21). Disposing it disposes the scope, and
/// with it both families' contexts and connections. There is deliberately no commit here: Configuration and
/// Operational may live in different databases, so a transaction spanning them cannot exist.
/// </summary>
internal sealed class EntityFrameworkStorageSession(IServiceScope scope) : IStorageSession
{
    public IStorage GetStorage() => scope.ServiceProvider.GetRequiredService<IStorage>();

    public void Dispose() => scope.Dispose();
}
