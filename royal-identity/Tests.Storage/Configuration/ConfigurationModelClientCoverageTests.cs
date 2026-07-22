using System.Reflection;
using RoyalIdentity.Models;

namespace Tests.Storage.Configuration;

/// <summary>
/// Coverage guard (plan Fase 2): every public instance property of <see cref="Client"/> must have an explicit
/// persistence/materialization decision documented here — a column, a string-value kind, a satellite table, or
/// the realm binding. Adding a <see cref="Client"/> property without a decision fails this test, forcing the
/// author to also update <c>ClientMaterializer</c> and the model rather than silently dropping the value.
/// </summary>
public class ConfigurationModelClientCoverageTests
{
	private static readonly Dictionary<string, string> PersistenceDecisions = new()
	{
		[nameof(Client.Id)] = "identity: clients.client_id (composite PK with realm)",
		[nameof(Client.Realm)] = "identity: realm binding via clients.realm_id (FK), not a column",
		[nameof(Client.Name)] = "column: name",
		[nameof(Client.Description)] = "column: description",
		[nameof(Client.ClientUri)] = "column: client_uri",
		[nameof(Client.LogoUri)] = "column: logo_uri",
		[nameof(Client.Enabled)] = "column: enabled",
		[nameof(Client.ProtocolType)] = "column: protocol_type",
		[nameof(Client.RequirePkce)] = "column: require_pkce",
		[nameof(Client.AllowPlainTextPkce)] = "column: allow_plain_text_pkce",
		[nameof(Client.ClientType)] = "column: client_type (enum as int)",
		[nameof(Client.AllowOfflineAccess)] = "column: allow_offline_access",
		[nameof(Client.AllowAllResourceServers)] = "column: allow_all_resource_servers",
		[nameof(Client.IncludeJwtId)] = "column: include_jwt_id",
		[nameof(Client.AlwaysSendClientClaims)] = "column: always_send_client_claims",
		[nameof(Client.AlwaysIncludeUserClaimsInIdToken)] = "column: always_include_user_claims_in_id_token",
		[nameof(Client.ClientClaimsPrefix)] = "column: client_claims_prefix",
		[nameof(Client.EnableLocalLogin)] = "column: enable_local_login",
		[nameof(Client.UserSsoLifetime)] = "column: user_sso_lifetime",
		[nameof(Client.AccessTokenLifetime)] = "column: access_token_lifetime",
		[nameof(Client.IdentityTokenLifetime)] = "column: identity_token_lifetime",
		[nameof(Client.AuthorizationCodeLifetime)] = "column: authorization_code_lifetime",
		[nameof(Client.AbsoluteRefreshTokenLifetime)] = "column: absolute_refresh_token_lifetime",
		[nameof(Client.SlidingRefreshTokenLifetime)] = "column: sliding_refresh_token_lifetime",
		[nameof(Client.ConsentLifetime)] = "column: consent_lifetime",
		[nameof(Client.RequireConsent)] = "column: require_consent",
		[nameof(Client.AllowRememberConsent)] = "column: allow_remember_consent",
		[nameof(Client.RequireClientSecret)] = "column: require_client_secret",
		[nameof(Client.RefreshTokenExpiration)] = "column: refresh_token_expiration (enum as int)",
		[nameof(Client.RefreshTokenPostConsumedTimeTolerance)] = "column: refresh_token_post_consumed_time_tolerance_ticks",
		[nameof(Client.UpdateAccessTokenClaimsOnRefresh)] = "column: update_access_token_claims_on_refresh",
		[nameof(Client.AllowLogoutWithoutUserConfirmation)] = "column: allow_logout_without_user_confirmation",
		[nameof(Client.FrontChannelLogoutSessionRequired)] = "column: front_channel_logout_session_required",
		[nameof(Client.BackChannelLogoutSessionRequired)] = "column: back_channel_logout_session_required",
		[nameof(Client.AllowedIdentityScopes)] = "string values: kind allowed_identity_scope",
		[nameof(Client.AllowedResourceServers)] = "string values: kind allowed_resource_server",
		[nameof(Client.AllowedScopes)] = "string values: kind allowed_scope",
		[nameof(Client.AllowedResponseTypes)] = "string values: kind allowed_response_type",
		[nameof(Client.AllowedGrantTypes)] = "string values: kind allowed_grant_type",
		[nameof(Client.AllowedIdentityTokenSigningAlgorithms)] = "string values: kind allowed_identity_token_signing_algorithm",
		[nameof(Client.AllowedAccessTokenSigningAlgorithms)] = "string values: kind allowed_access_token_signing_algorithm",
		[nameof(Client.IdentityProviderRestrictions)] = "string values: kind identity_provider_restriction",
		[nameof(Client.RedirectUris)] = "string values: kind redirect_uri",
		[nameof(Client.PostLogoutRedirectUris)] = "string values: kind post_logout_redirect_uri",
		[nameof(Client.AllowedCorsOrigins)] = "string values: kind allowed_cors_origin (case-insensitive comparison key)",
		[nameof(Client.FrontChannelLogoutUri)] = "string values: kind front_channel_logout_uri",
		[nameof(Client.BackChannelLogoutUri)] = "string values: kind back_channel_logout_uri",
		[nameof(Client.Claims)] = "table: client_claims",
		[nameof(Client.ClientSecrets)] = "table: client_secrets",
	};

	[Fact]
	public void EveryPublicClientProperty_HasAPersistenceDecision()
	{
		var actual = typeof(Client)
			.GetProperties(BindingFlags.Public | BindingFlags.Instance)
			.Select(p => p.Name)
			.ToHashSet();

		Assert.Equal(PersistenceDecisions.Keys.ToHashSet(), actual);
	}
}
