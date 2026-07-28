using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RoyalIdentity.UserAccounts.Features.Accounts.Domain;
using RoyalIdentity.UserAccounts.PostgreSql;

namespace Tests.UserAccounts;

public class UserAccountsPostgreSqlRegistrationTests
{
    [Fact]
    public void NamedConnectionRegistration_BuildsWithScopeValidation()
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
        services.AddUserAccountsPostgreSql();

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true,
        });
        using var scope = provider.CreateScope();

        Assert.NotNull(scope.ServiceProvider.GetRequiredService<UserAccountsPostgreSqlDbContext>());
    }

    private sealed class FakePasswordHasher : IUserAccountPasswordHasher
    {
        public string Hash(string password) => password;

        public bool Verify(string password, string passwordHash) => password == passwordHash;
    }
}
