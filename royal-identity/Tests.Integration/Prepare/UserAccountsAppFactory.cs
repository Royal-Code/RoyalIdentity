using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using RoyalIdentity.Storage.InMemory;
using RoyalIdentity.UserAccounts.Features.Accounts.UseCases;
using RoyalIdentity.UserAccounts.Features.ScopeProperties.Domain;
using RoyalIdentity.UserAccounts.Infrastructure.Data;
using RoyalIdentity.UserAccounts.Integration;
using RoyalIdentity.UserAccounts.Options;
using RoyalIdentity.UserAccounts.Sqlite;

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

			await SeedScopeAsync(db, realmId, "profile", cancellationToken);
			await SeedScopeAsync(db, realmId, "email", cancellationToken);

			await SeedAccountAsync(
				scope.ServiceProvider,
				realmId,
				MemoryStorage.AliceSubjectId,
				"alice",
				"Alice",
				"Alice@example.com",
				"alice",
				["admin"],
				cancellationToken);
			await SeedAccountAsync(
				scope.ServiceProvider,
				realmId,
				MemoryStorage.BobSubjectId,
				"bob",
				"Bob",
				"bob@example.com",
				"bob",
				["admin"],
				cancellationToken);
		}

		public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

		private async Task SeedAccountAsync(
			IServiceProvider services,
			string realmId,
			string subjectId,
			string username,
			string displayName,
			string email,
			string password,
			IReadOnlyList<string> roles,
			CancellationToken cancellationToken)
		{
			var db = services.GetRequiredService<UserAccountsDbContext>();
			if (await db.UserAccounts.AnyAsync(a => a.RealmId == realmId && a.SubjectId == subjectId, cancellationToken))
			{
				return;
			}

			var handler = services.GetRequiredService<ICreateUserAccountHandler>();
			var result = await handler.HandleAsync(new CreateUserAccount
			{
				RealmId = realmId,
				Options = optionsResolver.Resolve(realmId),
				Username = username,
				DisplayName = displayName,
				Email = email,
				EmailVerified = true,
				Password = password,
				SubjectId = subjectId,
				Roles = roles
			}, cancellationToken);

			if (!result.IsSuccess)
			{
				throw new InvalidOperationException($"Could not seed UserAccounts user '{username}'.");
			}
		}

		private static async Task SeedScopeAsync(
			UserAccountsDbContext db,
			string realmId,
			string scopeName,
			CancellationToken cancellationToken)
		{
			if (await db.PropertyScopes.AnyAsync(s => s.RealmId == realmId && s.Name == scopeName, cancellationToken))
			{
				return;
			}

			var propertyScope = new PropertyScope(realmId, scopeName, scopeName, DateTimeOffset.UtcNow);
			var version = propertyScope.Versions.Single();
			var result = propertyScope.ApproveVersion(version, DateTimeOffset.UtcNow);
			if (!result.IsSuccess)
			{
				throw new InvalidOperationException($"Could not seed UserAccounts property scope '{scopeName}'.");
			}

			db.PropertyScopes.Add(propertyScope);
			await db.SaveChangesAsync(cancellationToken);
		}
	}
}
