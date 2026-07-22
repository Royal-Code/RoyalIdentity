using System.Security.Claims;
using RoyalIdentity.Data.Configuration.Entities;
using RoyalIdentity.Models;

namespace RoyalIdentity.Storage.EntityFramework.Configuration.Materialization;

/// <summary>
/// Maps the core <see cref="Client"/> to its relational entity set and back (plan DF5/DF25). Every public
/// scalar of <see cref="Client"/> has a column; every single-string collection maps to a
/// <see cref="ClientStringValueEntity"/> kind; claims and secrets map to their own rows. Materialization
/// rebuilds independent graphs and restores the collection comparers the adapter owns (e.g. case-insensitive
/// CORS origins), never inheriting them from a provider collation. A property-coverage test fails when a new
/// <see cref="Client"/> property lands without a decision here.
/// </summary>
public sealed class ClientMaterializer
{
	private static readonly HashSet<string> KnownStringValueKinds = new(StringComparer.Ordinal)
	{
		ClientStringValueKinds.AllowedIdentityScope,
		ClientStringValueKinds.AllowedResourceServer,
		ClientStringValueKinds.AllowedScope,
		ClientStringValueKinds.AllowedResponseType,
		ClientStringValueKinds.AllowedGrantType,
		ClientStringValueKinds.AllowedIdentityTokenSigningAlgorithm,
		ClientStringValueKinds.AllowedAccessTokenSigningAlgorithm,
		ClientStringValueKinds.IdentityProviderRestriction,
		ClientStringValueKinds.RedirectUri,
		ClientStringValueKinds.PostLogoutRedirectUri,
		ClientStringValueKinds.AllowedCorsOrigin,
		ClientStringValueKinds.FrontChannelLogoutUri,
		ClientStringValueKinds.BackChannelLogoutUri,
	};

	/// <summary>The full relational projection of one <see cref="Client"/>.</summary>
	public readonly record struct ClientEntitySet(
		ClientEntity Root,
		IReadOnlyList<ClientStringValueEntity> StringValues,
		IReadOnlyList<ClientClaimEntity> Claims,
		IReadOnlyList<ClientSecretEntity> Secrets);

	public ClientEntitySet ToEntitySet(Client client)
	{
		ArgumentNullException.ThrowIfNull(client);
		ArgumentNullException.ThrowIfNull(client.Realm);

		var realmId = client.Realm.Id;
		var clientId = client.Id;

		var root = new ClientEntity
		{
			RealmId = realmId,
			ClientId = clientId,
			Name = client.Name,
			Description = client.Description,
			ClientUri = client.ClientUri,
			LogoUri = client.LogoUri,
			Enabled = client.Enabled,
			ProtocolType = client.ProtocolType,
			RequirePkce = client.RequirePkce,
			AllowPlainTextPkce = client.AllowPlainTextPkce,
			ClientType = (int)client.ClientType,
			AllowOfflineAccess = client.AllowOfflineAccess,
			AllowAllResourceServers = client.AllowAllResourceServers,
			IncludeJwtId = client.IncludeJwtId,
			AlwaysSendClientClaims = client.AlwaysSendClientClaims,
			AlwaysIncludeUserClaimsInIdToken = client.AlwaysIncludeUserClaimsInIdToken,
			ClientClaimsPrefix = client.ClientClaimsPrefix,
			EnableLocalLogin = client.EnableLocalLogin,
			UserSsoLifetime = client.UserSsoLifetime,
			AccessTokenLifetime = client.AccessTokenLifetime,
			IdentityTokenLifetime = client.IdentityTokenLifetime,
			AuthorizationCodeLifetime = client.AuthorizationCodeLifetime,
			AbsoluteRefreshTokenLifetime = client.AbsoluteRefreshTokenLifetime,
			SlidingRefreshTokenLifetime = client.SlidingRefreshTokenLifetime,
			ConsentLifetime = client.ConsentLifetime,
			RequireConsent = client.RequireConsent,
			AllowRememberConsent = client.AllowRememberConsent,
			RequireClientSecret = client.RequireClientSecret,
			RefreshTokenExpiration = (int)client.RefreshTokenExpiration,
			RefreshTokenPostConsumedTimeToleranceTicks = client.RefreshTokenPostConsumedTimeTolerance.Ticks,
			UpdateAccessTokenClaimsOnRefresh = client.UpdateAccessTokenClaimsOnRefresh,
			AllowLogoutWithoutUserConfirmation = client.AllowLogoutWithoutUserConfirmation,
			FrontChannelLogoutSessionRequired = client.FrontChannelLogoutSessionRequired,
			BackChannelLogoutSessionRequired = client.BackChannelLogoutSessionRequired,
		};

		var stringValues = new List<ClientStringValueEntity>();
		AddStringValues(stringValues, realmId, clientId, ClientStringValueKinds.AllowedIdentityScope, client.AllowedIdentityScopes);
		AddStringValues(stringValues, realmId, clientId, ClientStringValueKinds.AllowedResourceServer, client.AllowedResourceServers);
		AddStringValues(stringValues, realmId, clientId, ClientStringValueKinds.AllowedScope, client.AllowedScopes);
		AddStringValues(stringValues, realmId, clientId, ClientStringValueKinds.AllowedResponseType, client.AllowedResponseTypes);
		AddStringValues(stringValues, realmId, clientId, ClientStringValueKinds.AllowedGrantType, client.AllowedGrantTypes);
		AddStringValues(stringValues, realmId, clientId, ClientStringValueKinds.AllowedIdentityTokenSigningAlgorithm, client.AllowedIdentityTokenSigningAlgorithms);
		AddStringValues(stringValues, realmId, clientId, ClientStringValueKinds.AllowedAccessTokenSigningAlgorithm, client.AllowedAccessTokenSigningAlgorithms);
		AddStringValues(stringValues, realmId, clientId, ClientStringValueKinds.IdentityProviderRestriction, client.IdentityProviderRestrictions);
		AddStringValues(stringValues, realmId, clientId, ClientStringValueKinds.RedirectUri, client.RedirectUris);
		AddStringValues(stringValues, realmId, clientId, ClientStringValueKinds.PostLogoutRedirectUri, client.PostLogoutRedirectUris);
		AddStringValues(stringValues, realmId, clientId, ClientStringValueKinds.AllowedCorsOrigin, client.AllowedCorsOrigins);
		AddStringValues(stringValues, realmId, clientId, ClientStringValueKinds.FrontChannelLogoutUri, client.FrontChannelLogoutUri);
		AddStringValues(stringValues, realmId, clientId, ClientStringValueKinds.BackChannelLogoutUri, client.BackChannelLogoutUri);

		var claims = client.Claims
			.Select((claim, index) => new ClientClaimEntity
			{
				RealmId = realmId,
				ClientId = clientId,
				Ordinal = index,
				Type = claim.Type,
				Value = claim.Value,
				ValueType = claim.ValueType,
				Issuer = claim.Issuer,
				OriginalIssuer = claim.OriginalIssuer,
			})
			.ToList();

		var secrets = client.ClientSecrets
			.Select((secret, index) => new ClientSecretEntity
			{
				RealmId = realmId,
				ClientId = clientId,
				Ordinal = index,
				Type = secret.Type,
				Value = secret.Value,
				Description = secret.Description,
				ExpirationUtc = secret.Expiration,
			})
			.ToList();

		return new ClientEntitySet(root, stringValues, claims, secrets);
	}

	public Client ToClient(
		ClientEntity root,
		IEnumerable<ClientStringValueEntity> stringValues,
		IEnumerable<ClientClaimEntity> claims,
		IEnumerable<ClientSecretEntity> secrets,
		Realm realm)
	{
		ArgumentNullException.ThrowIfNull(root);
		ArgumentNullException.ThrowIfNull(stringValues);
		ArgumentNullException.ThrowIfNull(claims);
		ArgumentNullException.ThrowIfNull(secrets);
		ArgumentNullException.ThrowIfNull(realm);

		var stringValueRows = stringValues.ToList();
		var claimRows = claims.ToList();
		var secretRows = secrets.ToList();
		ValidateProjection(root, stringValueRows, claimRows, secretRows, realm);

		var client = new Client
		{
			Id = root.ClientId,
			Name = root.Name,
			Description = root.Description,
			ClientUri = root.ClientUri,
			LogoUri = root.LogoUri,
			Enabled = root.Enabled,
			Realm = realm,
			ProtocolType = root.ProtocolType,
			RequirePkce = root.RequirePkce,
			AllowPlainTextPkce = root.AllowPlainTextPkce,
			ClientType = (ClientType)root.ClientType,
			AllowOfflineAccess = root.AllowOfflineAccess,
			AllowAllResourceServers = root.AllowAllResourceServers,
			IncludeJwtId = root.IncludeJwtId,
			AlwaysSendClientClaims = root.AlwaysSendClientClaims,
			AlwaysIncludeUserClaimsInIdToken = root.AlwaysIncludeUserClaimsInIdToken,
			ClientClaimsPrefix = root.ClientClaimsPrefix,
			EnableLocalLogin = root.EnableLocalLogin,
			UserSsoLifetime = root.UserSsoLifetime,
			AccessTokenLifetime = root.AccessTokenLifetime,
			IdentityTokenLifetime = root.IdentityTokenLifetime,
			AuthorizationCodeLifetime = root.AuthorizationCodeLifetime,
			AbsoluteRefreshTokenLifetime = root.AbsoluteRefreshTokenLifetime,
			SlidingRefreshTokenLifetime = root.SlidingRefreshTokenLifetime,
			ConsentLifetime = root.ConsentLifetime,
			RequireConsent = root.RequireConsent,
			AllowRememberConsent = root.AllowRememberConsent,
			RequireClientSecret = root.RequireClientSecret,
			RefreshTokenExpiration = (TokenExpiration)root.RefreshTokenExpiration,
			RefreshTokenPostConsumedTimeTolerance = TimeSpan.FromTicks(root.RefreshTokenPostConsumedTimeToleranceTicks),
			UpdateAccessTokenClaimsOnRefresh = root.UpdateAccessTokenClaimsOnRefresh,
			AllowLogoutWithoutUserConfirmation = root.AllowLogoutWithoutUserConfirmation,
			FrontChannelLogoutSessionRequired = root.FrontChannelLogoutSessionRequired,
			BackChannelLogoutSessionRequired = root.BackChannelLogoutSessionRequired,
		};

		var valuesByKind = stringValueRows
			.GroupBy(v => v.Kind)
			.ToDictionary(g => g.Key, g => g.Select(v => v.Value).ToList());

		Fill(client.AllowedIdentityScopes, valuesByKind, ClientStringValueKinds.AllowedIdentityScope);
		Fill(client.AllowedResourceServers, valuesByKind, ClientStringValueKinds.AllowedResourceServer);
		Fill(client.AllowedScopes, valuesByKind, ClientStringValueKinds.AllowedScope);
		Fill(client.AllowedResponseTypes, valuesByKind, ClientStringValueKinds.AllowedResponseType);
		Fill(client.AllowedGrantTypes, valuesByKind, ClientStringValueKinds.AllowedGrantType);
		Fill(client.AllowedIdentityTokenSigningAlgorithms, valuesByKind, ClientStringValueKinds.AllowedIdentityTokenSigningAlgorithm);
		Fill(client.AllowedAccessTokenSigningAlgorithms, valuesByKind, ClientStringValueKinds.AllowedAccessTokenSigningAlgorithm);
		Fill(client.IdentityProviderRestrictions, valuesByKind, ClientStringValueKinds.IdentityProviderRestriction);
		Fill(client.RedirectUris, valuesByKind, ClientStringValueKinds.RedirectUri);
		Fill(client.PostLogoutRedirectUris, valuesByKind, ClientStringValueKinds.PostLogoutRedirectUri);
		// AllowedCorsOrigins keeps its case-insensitive comparer, restored by the adapter (plan DF5).
		Fill(client.AllowedCorsOrigins, valuesByKind, ClientStringValueKinds.AllowedCorsOrigin);
		Fill(client.FrontChannelLogoutUri, valuesByKind, ClientStringValueKinds.FrontChannelLogoutUri);
		Fill(client.BackChannelLogoutUri, valuesByKind, ClientStringValueKinds.BackChannelLogoutUri);

		client.Claims.Clear();
		foreach (var claim in claimRows.OrderBy(c => c.Ordinal))
			client.Claims.Add(new Claim(claim.Type, claim.Value, claim.ValueType, claim.Issuer, claim.OriginalIssuer));

		client.ClientSecrets.Clear();
		foreach (var secret in secretRows.OrderBy(s => s.Ordinal))
			client.ClientSecrets.Add(new ClientSecret(secret.Value, secret.Description, secret.ExpirationUtc) { Type = secret.Type });

		return client;
	}

	private static void ValidateProjection(
		ClientEntity root,
		IReadOnlyCollection<ClientStringValueEntity> stringValues,
		IReadOnlyCollection<ClientClaimEntity> claims,
		IReadOnlyCollection<ClientSecretEntity> secrets,
		Realm realm)
	{
		if (!string.Equals(root.RealmId, realm.Id, StringComparison.Ordinal))
			throw ConfigurationMaterializationException.RealmMismatch();

		if (stringValues.Any(value => !BelongsToRoot(value.RealmId, value.ClientId, root)))
			throw ConfigurationMaterializationException.SatelliteMismatch("string-value");

		if (claims.Any(claim => !BelongsToRoot(claim.RealmId, claim.ClientId, root)))
			throw ConfigurationMaterializationException.SatelliteMismatch("claim");

		if (secrets.Any(secret => !BelongsToRoot(secret.RealmId, secret.ClientId, root)))
			throw ConfigurationMaterializationException.SatelliteMismatch("secret");

		if (stringValues.Any(value => !KnownStringValueKinds.Contains(value.Kind)))
			throw ConfigurationMaterializationException.UnknownStringValueKind();

		if (!Enum.IsDefined((ClientType)root.ClientType))
			throw ConfigurationMaterializationException.InvalidEnum(nameof(Client.ClientType));

		if (!Enum.IsDefined((TokenExpiration)root.RefreshTokenExpiration))
			throw ConfigurationMaterializationException.InvalidEnum(nameof(Client.RefreshTokenExpiration));
	}

	private static bool BelongsToRoot(string realmId, string clientId, ClientEntity root)
		=> string.Equals(realmId, root.RealmId, StringComparison.Ordinal)
			&& string.Equals(clientId, root.ClientId, StringComparison.Ordinal);

	private static void AddStringValues(
		List<ClientStringValueEntity> target, string realmId, string clientId, string kind, IEnumerable<string> values)
	{
		foreach (var value in values)
		{
			target.Add(new ClientStringValueEntity
			{
				RealmId = realmId,
				ClientId = clientId,
				Kind = kind,
				Value = value,
				// CORS origins compare case-insensitively; every other kind is Ordinal. The comparison key is
				// the per-kind uniqueness key, so a provider collation is never trusted to enforce it (plan DF5).
				ComparisonKey = kind == ClientStringValueKinds.AllowedCorsOrigin ? value.ToLowerInvariant() : value,
			});
		}
	}

	private static void Fill(HashSet<string> target, Dictionary<string, List<string>> valuesByKind, string kind)
	{
		target.Clear();
		if (valuesByKind.TryGetValue(kind, out var values))
		{
			foreach (var value in values)
				target.Add(value);
		}
	}
}
