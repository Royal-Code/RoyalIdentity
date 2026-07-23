using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RoyalIdentity.Data.Configuration;

namespace Tests.Storage.Configuration.Support;

internal interface IConfigurationTestDatabase<out TContext> : IAsyncDisposable
	where TContext : ConfigurationDbContext
{
	TContext NewContext();

	void AddStorage(ServiceCollection services);
}
