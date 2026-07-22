using Microsoft.EntityFrameworkCore;
using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Data.Configuration.Entities;
using RoyalIdentity.Models;
using RoyalIdentity.Models.Scopes;
using static RoyalIdentity.Options.Constants;

namespace RoyalIdentity.Storage.EntityFramework.Configuration.Resources;

internal sealed class EntityFrameworkResourceStore(
	string realmId,
	IConfigurationDbContextAccessor accessor,
	IConfigurationResourceSource source) : IResourceStore
{
	public async Task<AllScopes> GetAllResourcesAsync(CancellationToken ct = default)
	{
		if (!await IsRealmLiveAsync(ct))
			return new AllScopes([], []);

		var catalog = BuildCatalog();
		return new AllScopes(catalog.ResourceServers.Values.ToList(), catalog.IdentityScopes.Values.ToList());
	}

	public async Task<AllScopes> GetAllEnabledResourcesAsync(CancellationToken ct = default)
	{
		if (!await IsRealmLiveAsync(ct))
			return new AllScopes([], []);

		var catalog = BuildCatalog();
		var servers = catalog.ResourceServers.Values
			.Where(server => server.Enabled)
			.Select(server =>
			{
				var copy = Clone(server);
				copy.Scopes = [.. copy.Scopes.Where(scope => scope.Enabled)];
				return copy;
			})
			.ToList();
		var identityScopes = catalog.IdentityScopes.Values
			.Where(scope => scope.Enabled)
			.Select(Clone)
			.ToList();

		return new AllScopes(servers, identityScopes);
	}

	public Task<RequestedResources> FindResourcesByScopeAsync(
		IEnumerable<string> scopeNames,
		bool onlyEnabled = false,
		CancellationToken ct = default)
		=> FindRequestedResourcesAsync(scopeNames, [], onlyEnabled, ct);

	public async Task<RequestedResources> FindRequestedResourcesAsync(
		IEnumerable<string> scopeNames,
		IEnumerable<string> resourceUris,
		bool onlyEnabled = false,
		CancellationToken ct = default)
	{
		ArgumentNullException.ThrowIfNull(scopeNames);
		ArgumentNullException.ThrowIfNull(resourceUris);

		var resources = new RequestedResources();
		foreach (var scopeName in scopeNames)
			resources.RequestedScopeNames.Add(scopeName);
		foreach (var resourceUri in resourceUris)
			resources.RequestedResourceUris.Add(resourceUri);

		if (!await IsRealmLiveAsync(ct))
		{
			foreach (var scopeName in resources.RequestedScopeNames)
				resources.MissingScopes.Add(scopeName);
			foreach (var resourceUri in resources.RequestedResourceUris)
				resources.InvalidTargets.Add(resourceUri);
			return resources;
		}

		var catalog = BuildCatalog();
		foreach (var name in resources.RequestedScopeNames)
		{
			if (name == Server.StandardScopes.OfflineAccess)
			{
				resources.OfflineAccess = true;
				continue;
			}

			if (catalog.IdentityScopes.TryGetValue(name, out var identityScope)
				&& (!onlyEnabled || identityScope.Enabled))
			{
				if (IsEnabled(identityScope, resources))
					resources.IdentityScopes.Add(identityScope);
				continue;
			}

			if (catalog.ScopeIndex.TryGetValue(name, out var scopeMatch)
				&& (!onlyEnabled || (scopeMatch.Server.Enabled && scopeMatch.Scope.Enabled)))
			{
				if (IsEnabled(scopeMatch.Server, resources) && IsEnabled(scopeMatch.Scope, resources))
				{
					resources.Scopes.Add(scopeMatch.Scope);
					AddResourceServer(resources, scopeMatch.Server);
				}
				continue;
			}

			resources.MissingScopes.Add(name);
		}

		foreach (var uri in resources.RequestedResourceUris)
		{
			if (!IsValidResourceUri(uri)
				|| !catalog.ResourceIndex.TryGetValue(uri, out var resourceMatch)
				|| !resourceMatch.Server.Enabled)
			{
				resources.InvalidTargets.Add(uri);
				continue;
			}

			resources.ProtectedResources.Add(resourceMatch.Resource);
			AddResourceServer(resources, resourceMatch.Server);
		}

		return resources;
	}

	private Task<bool> IsRealmLiveAsync(CancellationToken ct)
		=> accessor.DbContext.Set<RealmEntity>()
			.AsNoTracking()
			.AnyAsync(realm => realm.Id == realmId && realm.DeletedAtUtc == null, ct);

	private Catalog BuildCatalog()
	{
		var identityScopes = source.GetIdentityScopes(realmId)
			.Select(Clone)
			.ToDictionary(scope => scope.Name, StringComparer.Ordinal);
		var resourceServers = source.GetResourceServers(realmId)
			.Select(Clone)
			.ToDictionary(server => server.Name, StringComparer.Ordinal);
		var scopeIndex = new Dictionary<string, (ResourceServer Server, Scope Scope)>(StringComparer.Ordinal);
		var resourceIndex = new Dictionary<string, (ResourceServer Server, ProtectedResource Resource)>(StringComparer.Ordinal);

		foreach (var server in resourceServers.Values)
		{
			foreach (var scope in server.Scopes)
			{
				if (!scopeIndex.TryAdd(scope.Name, (server, scope)))
					throw new InvalidOperationException($"Duplicate scope name '{scope.Name}' in realm '{realmId}'.");
			}

			foreach (var resource in server.ProtectedResources)
			{
				if (!IsValidResourceUri(resource.ResourceUri))
					throw new InvalidOperationException($"Invalid protected resource URI '{resource.ResourceUri}' in realm '{realmId}'.");

				if (!resourceIndex.TryAdd(resource.ResourceUri, (server, resource)))
					throw new InvalidOperationException($"Duplicate protected resource URI '{resource.ResourceUri}' in realm '{realmId}'.");
			}
		}

		return new Catalog(identityScopes, resourceServers, scopeIndex, resourceIndex);
	}

	private static IdentityScope Clone(IdentityScope source)
		=> new(source.Visibility, source.Name, source.DisplayName, source.Description, source.UserClaims)
		{
			Description = source.Description,
			Enabled = source.Enabled,
			ShowInDiscoveryDocument = source.ShowInDiscoveryDocument,
			Required = source.Required,
			Emphasize = source.Emphasize,
		};

	private static Scope Clone(Scope source)
		=> new(source.Visibility, source.Name, source.DisplayName, source.Description)
		{
			Enabled = source.Enabled,
			ShowInDiscoveryDocument = source.ShowInDiscoveryDocument,
			Required = source.Required,
			Emphasize = source.Emphasize,
		};

	private static ResourceServer Clone(ResourceServer source)
		=> new(source.Visibility, source.Name, source.DisplayName, source.Description)
		{
			Enabled = source.Enabled,
			ShowInDiscoveryDocument = source.ShowInDiscoveryDocument,
			Audience = source.Audience,
			AllowScopeRequests = source.AllowScopeRequests,
			Scopes = source.Scopes.Select(Clone).ToList(),
			ProtectedResources = source.ProtectedResources.Select(resource => new ProtectedResource(resource.ResourceUri)
			{
				ShowInDiscoveryDocument = resource.ShowInDiscoveryDocument,
				DisplayName = resource.DisplayName,
				DocumentationUri = resource.DocumentationUri,
				PolicyUri = resource.PolicyUri,
				TosUri = resource.TosUri,
			}).ToList(),
			Secrets = source.Secrets.Select(secret => new ClientSecret(secret.Value, secret.Description, secret.Expiration)
			{
				Type = secret.Type,
			}).ToList(),
			AllowedAccessTokenSigningAlgorithms = new HashSet<string>(
				source.AllowedAccessTokenSigningAlgorithms,
				StringComparer.Ordinal),
		};

	private static bool IsValidResourceUri(string uri)
	{
		if (!Uri.TryCreate(uri, UriKind.Absolute, out var parsed) || !string.IsNullOrEmpty(parsed.Fragment))
			return false;

		return parsed.Scheme == Uri.UriSchemeHttps
			|| (parsed.Scheme == Uri.UriSchemeHttp && parsed.IsLoopback);
	}

	private static void AddResourceServer(RequestedResources resources, ResourceServer server)
	{
		if (!resources.ResourceServers.Contains(server))
			resources.ResourceServers.Add(server);
	}

	private static bool IsEnabled(ScopeBase scope, RequestedResources resources)
	{
		if (scope.Enabled)
			return true;

		resources.MissingScopes.Add(scope.Name);
		return false;
	}

	private sealed record Catalog(
		Dictionary<string, IdentityScope> IdentityScopes,
		Dictionary<string, ResourceServer> ResourceServers,
		Dictionary<string, (ResourceServer Server, Scope Scope)> ScopeIndex,
		Dictionary<string, (ResourceServer Server, ProtectedResource Resource)> ResourceIndex);
}
