using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RoyalIdentity.UserAccounts.Features.Accounts.Domain;
using RoyalIdentity.UserAccounts.PostgreSql;
using RoyalIdentity.Users;
using RoyalIdentity.Users.Contracts;

namespace Tests.UserAccounts;

public class UserAccountsPostgreSqlRegistrationTests
{
    [Fact]
    public void NamedConnectionAndIntegration_BuildCompleteGraphWithScopeValidation()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:UserAccounts"] =
                    "Host=localhost;Database=royalidentity;Username=postgres;Password=postgres",
            })
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton<IUserAccountPasswordHasher, FakePasswordHasher>();
        services.AddSingleton<ISessionRevocationService, NoopSessionRevocationService>();
        services.AddUserAccountsPostgreSql();
        services.AddUserAccountsForRoyalIdentity();

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true,
        });
        using var scope = provider.CreateScope();

        Assert.NotNull(scope.ServiceProvider.GetRequiredService<UserAccountsPostgreSqlDbContext>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IUserDirectory>());
    }

    private sealed class FakePasswordHasher : IUserAccountPasswordHasher
    {
        public string Hash(string password) => password;

        public bool Verify(string password, string passwordHash) => password == passwordHash;
    }

    private sealed class NoopSessionRevocationService : ISessionRevocationService
    {
        public Task RevokeAsync(
            string subjectId,
            SessionRevocation revocation,
            string? currentSessionId,
            CancellationToken ct = default)
            => Task.CompletedTask;
    }
}
