using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using RoyalIdentity.Configuration;
using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Models.Scopes;
using RoyalIdentity.Storage.EntityFramework.Sqlite;
using RoyalIdentity.UserAccounts.Integration;
using RoyalIdentity.Users.Contracts;

namespace Tests.Integration.Prepare;

public class PersistentStorageCompositionTests
{
    [Fact]
    public async Task Host_UsesThreeRealBackings_AndDeterministicProviderNeutralHandles()
    {
        using var factory = new PersistentStorageAppFactory();
        _ = factory.CreateClient();
        using var scope = factory.Services.CreateScope();

        Assert.NotNull(scope.ServiceProvider.GetRequiredService<ConfigurationSqliteDbContext>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<OperationalSqliteDbContext>());
        var storages = scope.ServiceProvider.GetServices<IStorage>();
        var directories = scope.ServiceProvider.GetServices<IUserDirectory>();
        Assert.Single(storages);
        Assert.Single(directories);
        Assert.StartsWith(
            "RoyalIdentity.Storage.EntityFramework",
            storages.Single().GetType().FullName,
            StringComparison.Ordinal);
        Assert.IsType<UserAccountsUserDirectory>(directories.Single());
        Assert.Null(scope.ServiceProvider.GetService<RoyalIdentity.Storage.InMemory.MemoryStorage>());

        var demo = await factory.LoadRealmAsync(factory.Handles.Demo);
        var alice = await scope.ServiceProvider
            .GetRequiredService<IUserDirectory>()
            .GetSubjectStore(demo)
            .FindBySubjectIdAsync(factory.Handles.Alice.SubjectId);

        Assert.NotNull(alice);
        Assert.Equal(factory.Handles.Alice.SubjectId, alice.SubjectId);
        Type[] handleTypes =
        [
            typeof(PersistentStorageHandles),
            typeof(TestRealmHandle),
            typeof(TestClientHandle),
            typeof(TestSubjectHandle),
        ];
        Assert.All(
            handleTypes.SelectMany(type => type.GetProperties()),
            property => Assert.NotEqual(typeof(RoyalIdentity.Models.Realm), property.PropertyType));
    }

    [Fact]
    public async Task ClientWrite_IsVisibleAfterTheHelperRefreshesTheSnapshot()
    {
        using var factory = new PersistentStorageAppFactory();
        _ = factory.CreateClient();

        await factory.SaveClientAsync(
            factory.Handles.Demo,
            "phase4-client",
            client =>
            {
                client.Name = "Phase 4 Client";
                client.RequireClientSecret = false;
                client.AllowedGrantTypes.Add("client_credentials");
            });

        using var scope = factory.Services.CreateScope();
        var storage = scope.ServiceProvider.GetRequiredService<IStorage>();
        var realm = await storage.Realms.GetByIdAsync(factory.Handles.Demo.Id, default);
        var persisted = await storage
            .GetClientStore(realm!)
            .FindEnabledClientByIdAsync("phase4-client", default);

        Assert.NotNull(persisted);
        Assert.Equal("Phase 4 Client", persisted.Name);
        Assert.True(scope.ServiceProvider.GetRequiredService<IConfigurationSnapshot>().IsLoaded);
    }

    [Fact]
    public async Task ResourceHook_IsExplicitAndRealmBound()
    {
        using var factory = new PersistentStorageAppFactory();
        _ = factory.CreateClient();
        const string resourceName = "phase4-api";
        factory.Resources.SetResourceServer(
            factory.Handles.Demo.Id,
            new ResourceServer(ScopeVisibility.Public, resourceName, "Phase 4 API", "Test resource")
            {
                Scopes = [new Scope(ScopeVisibility.Public, "phase4.read", "Read", "Read access")],
            });

        using var scope = factory.Services.CreateScope();
        var storage = scope.ServiceProvider.GetRequiredService<IStorage>();
        var demo = await storage.Realms.GetByIdAsync(factory.Handles.Demo.Id, default);
        var server = await storage.Realms.GetByIdAsync(factory.Handles.Server.Id, default);

        var demoResources = await storage.GetResourceStore(demo!).GetAllResourcesAsync();
        var serverResources = await storage.GetResourceStore(server!).GetAllResourcesAsync();

        Assert.Contains(demoResources.ResourceServers, resource => resource.Name == resourceName);
        Assert.DoesNotContain(serverResources.ResourceServers, resource => resource.Name == resourceName);
    }

    [Fact]
    public async Task AccountStateSetup_UsesTheRealModuleAggregate()
    {
        using var factory = new PersistentStorageAppFactory();
        _ = factory.CreateClient();

        await factory.SetAccountActiveAsync(factory.Handles.Demo, factory.Handles.Alice, active: false);

        using (var scope = factory.Services.CreateScope())
        {
            var demo = await factory.LoadRealmAsync(factory.Handles.Demo);
            var subjects = scope.ServiceProvider
                .GetRequiredService<IUserDirectory>()
                .GetSubjectStore(demo);
            Assert.False(await subjects.IsActiveAsync(factory.Handles.Alice.SubjectId));
        }

        await factory.SetAccountActiveAsync(factory.Handles.Demo, factory.Handles.Alice, active: true);

        using var activeScope = factory.Services.CreateScope();
        var activeDemo = await factory.LoadRealmAsync(factory.Handles.Demo);
        Assert.True(await activeScope.ServiceProvider
            .GetRequiredService<IUserDirectory>()
            .GetSubjectStore(activeDemo)
            .IsActiveAsync(factory.Handles.Alice.SubjectId));
    }

    [Fact]
    public async Task TwoParallelFactories_DoNotShareDatabaseOrMutableSetup()
    {
        using var first = new PersistentStorageAppFactory();
        using var second = new PersistentStorageAppFactory();
        await Task.WhenAll(
            Task.Run(() => first.CreateClient()),
            Task.Run(() => second.CreateClient()));

        Assert.NotEqual(first.IdpConnectionString, second.IdpConnectionString);
        Assert.NotEqual(first.UserAccountsConnectionString, second.UserAccountsConnectionString);
        Assert.NotSame(first.Handles, second.Handles);
        Assert.NotSame(first.Resources, second.Resources);

        await first.SaveClientAsync(
            first.Handles.Demo,
            "first-only",
            client =>
            {
                client.RequireClientSecret = false;
                client.AllowedGrantTypes.Add("client_credentials");
            });

        using var secondScope = second.Services.CreateScope();
        var secondStorage = secondScope.ServiceProvider.GetRequiredService<IStorage>();
        var secondDemo = await secondStorage.Realms.GetByIdAsync(second.Handles.Demo.Id, default);
        var leaked = await secondStorage
            .GetClientStore(secondDemo!)
            .FindClientByIdAsync("first-only", default);

        Assert.Null(leaked);
    }

    [Fact]
    public void CaptiveDependency_FailsDuringFactoryStartup()
    {
        using var factory = new CaptiveDependencyAppFactory();

        var exception = Assert.ThrowsAny<Exception>(factory.CreateClient);

        Assert.Contains("scoped", exception.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains(nameof(IStorage), exception.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Dispose_RemovesKeyRingAndReleasesNamedInMemoryDatabases()
    {
        var factory = new PersistentStorageAppFactory();
        _ = factory.CreateClient();
        var keyRingPath = factory.DataProtectionKeyRingPath;
        var idpConnectionString = factory.IdpConnectionString;

        Assert.True(Directory.Exists(keyRingPath));
        factory.Dispose();

        Assert.False(Directory.Exists(keyRingPath));
        await using var connection = new SqliteConnection(idpConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'realms';";
        Assert.Equal(0L, await command.ExecuteScalarAsync());
    }

    private sealed class CaptiveDependencyAppFactory : PersistentStorageAppFactory
    {
        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureServices(services => services.AddSingleton<CaptiveStorage>());
        }
    }

    private sealed class CaptiveStorage(IStorage storage)
    {
        public IStorage Storage { get; } = storage;
    }
}
