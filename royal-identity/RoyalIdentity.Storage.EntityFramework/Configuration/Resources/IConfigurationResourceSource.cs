using RoyalIdentity.Models.Scopes;

namespace RoyalIdentity.Storage.EntityFramework.Configuration.Resources;

/// <summary>
/// Supplies the volatile resource/scopes configuration for one realm. The shape is intentionally not
/// persisted while the resource model is under redesign (plan DF15/DF22).
/// </summary>
public interface IConfigurationResourceSource
{
	IEnumerable<IdentityScope> GetIdentityScopes(string realmId);

	IEnumerable<ResourceServer> GetResourceServers(string realmId);
}
