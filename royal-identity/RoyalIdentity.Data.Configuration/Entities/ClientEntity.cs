namespace RoyalIdentity.Data.Configuration.Entities;

/// <summary>
/// Client root (table <c>clients</c>), realm-bound by composite key. Carries the full scalar inventory of the
/// core <c>Client</c> (plan DF5); every scalar has a column here and a materialization decision in the adapter,
/// guarded by a property-coverage test. Enums and <c>TimeSpan</c> are stored as primitives (this project never
/// references the core), and the adapter converts them. Collections live in the satellite tables
/// (<see cref="ClientStringValueEntity"/>, <see cref="ClientClaimEntity"/>, <see cref="ClientSecretEntity"/>).
/// </summary>
public class ClientEntity
{
	public required string RealmId { get; set; }

	public required string ClientId { get; set; }

	public required string Name { get; set; }

	public string? Description { get; set; }

	public string? ClientUri { get; set; }

	public string? LogoUri { get; set; }

	public bool Enabled { get; set; }

	public required string ProtocolType { get; set; }

	public bool RequirePkce { get; set; }

	public bool AllowPlainTextPkce { get; set; }

	/// <summary>Stores the core <c>ClientType</c> enum as its integer value.</summary>
	public int ClientType { get; set; }

	public bool AllowOfflineAccess { get; set; }

	public bool AllowAllResourceServers { get; set; }

	public bool IncludeJwtId { get; set; }

	public bool AlwaysSendClientClaims { get; set; }

	public bool AlwaysIncludeUserClaimsInIdToken { get; set; }

	public string? ClientClaimsPrefix { get; set; }

	public bool EnableLocalLogin { get; set; }

	public int? UserSsoLifetime { get; set; }

	public int AccessTokenLifetime { get; set; }

	public int IdentityTokenLifetime { get; set; }

	public int AuthorizationCodeLifetime { get; set; }

	public int AbsoluteRefreshTokenLifetime { get; set; }

	public int SlidingRefreshTokenLifetime { get; set; }

	public int? ConsentLifetime { get; set; }

	public bool RequireConsent { get; set; }

	public bool AllowRememberConsent { get; set; }

	public bool RequireClientSecret { get; set; }

	/// <summary>Stores the core <c>TokenExpiration</c> enum as its integer value.</summary>
	public int RefreshTokenExpiration { get; set; }

	/// <summary>Stores <c>RefreshTokenPostConsumedTimeTolerance</c> as <see cref="System.TimeSpan.Ticks"/>.</summary>
	public long RefreshTokenPostConsumedTimeToleranceTicks { get; set; }

	public bool UpdateAccessTokenClaimsOnRefresh { get; set; }

	public bool AllowLogoutWithoutUserConfirmation { get; set; }

	public bool FrontChannelLogoutSessionRequired { get; set; }

	public bool BackChannelLogoutSessionRequired { get; set; }
}
