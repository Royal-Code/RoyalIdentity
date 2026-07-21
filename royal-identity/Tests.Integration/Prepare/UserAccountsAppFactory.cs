using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using RoyalIdentity.Storage.InMemory;
using RoyalIdentity.UserAccounts.Infrastructure.Data;
using RoyalIdentity.UserAccounts.Integration;
using RoyalIdentity.UserAccounts.Options;
using RoyalIdentity.UserAccounts.Sqlite;
using Tests.UserAccounts;

namespace Tests.Integration.Prepare;

/// <summary>
/// Test host variant that keeps the IdP storage in-memory, but swaps the account edge ports to the
/// UserAccounts module via the opt-in integration adapter.
/// </summary>
public sealed class UserAccountsAppFactory : AppFactory
{
	protected override void ConfigureWebHost(IWebHostBuilder builder)
	{
		base.ConfigureWebHost(builder);

		builder.ConfigureServices(services =>
		{
			var options = CreateOptions();

			services.TryAddSingleton(TimeProvider.System);
			services.AddSingleton<IUserAccountsRealmOptionsResolver>(
				new DefaultUserAccountsRealmOptionsResolver(options));
			services.AddUserAccountsSqliteInMemory();
			services.AddUserAccountsForRoyalIdentity();
			services.AddHostedService<UserAccountsSeedHostedService>();
		});
	}

	private static UserAccountsRealmOptions CreateOptions()
	{
		var options = new UserAccountsRealmOptions
		{
			AllowProvidedSubjectId = true
		};
		options.PasswordOptions.MinimumLength = 1;
		options.PasswordOptions.RequireSpecialCharacters = false;
		options.PasswordOptions.RequireDigit = false;
		options.PasswordOptions.RequireUppercase = false;
		options.PasswordOptions.RequireLowercase = false;
		options.PasswordOptions.MinimumUniqueCharacters = 0;
		options.PasswordOptions.DisallowUsernameInPassword = false;
		return options;
	}

	private sealed class UserAccountsSeedHostedService(
		IServiceScopeFactory scopeFactory,
		IUserAccountsRealmOptionsResolver optionsResolver) : IHostedService
	{
		public async Task StartAsync(CancellationToken cancellationToken)
		{
			using var scope = scopeFactory.CreateScope();
			var db = scope.ServiceProvider.GetRequiredService<UserAccountsDbContext>();
			var realmId = MemoryStorage.DemoRealm.Id;
			var now = DateTimeOffset.UtcNow;

			await UserAccountsModuleSeed.SeedDefaultScopesAsync(db, realmId, now, cancellationToken);
			await UserAccountsModuleSeed.SeedDefaultAccountsAsync(
				scope.ServiceProvider, realmId, optionsResolver.Resolve(realmId), now, cancellationToken);
		}

		public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
	}
}
