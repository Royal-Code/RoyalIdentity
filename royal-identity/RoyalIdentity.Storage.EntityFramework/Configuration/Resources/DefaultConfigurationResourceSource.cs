using Microsoft.Extensions.Options;
using RoyalIdentity.Models.Scopes;

namespace RoyalIdentity.Storage.EntityFramework.Configuration.Resources;

internal sealed class DefaultConfigurationResourceSource(
	IOptions<ConfigurationResourceBridgeOptions> options) : IConfigurationResourceSource
{
	public IEnumerable<IdentityScope> GetIdentityScopes(string realmId)
		=> options.Value.StandardIdentityScopes.Concat(options.Value.GetIdentityScopes(realmId));

	public IEnumerable<ResourceServer> GetResourceServers(string realmId)
		=> options.Value.GetResourceServers(realmId);
}
