namespace RoyalIdentity.Data.Configuration.Entities;

/// <summary>
/// Canonical discriminators for <see cref="ClientStringValueEntity.Kind"/> (plan DF5). Each of the core
/// <c>Client</c>'s single-string collections persists under one stable kind; the adapter's materializer is the
/// only consumer that maps a kind back to its collection (and its comparer). Kept in the pure data project so
/// the persisted vocabulary lives with the model, never derived from a provider collation.
/// </summary>
public static class ClientStringValueKinds
{
	public const string AllowedIdentityScope = "allowed_identity_scope";

	public const string AllowedResourceServer = "allowed_resource_server";

	public const string AllowedScope = "allowed_scope";

	public const string AllowedResponseType = "allowed_response_type";

	public const string AllowedGrantType = "allowed_grant_type";

	public const string AllowedIdentityTokenSigningAlgorithm = "allowed_identity_token_signing_algorithm";

	public const string AllowedAccessTokenSigningAlgorithm = "allowed_access_token_signing_algorithm";

	public const string IdentityProviderRestriction = "identity_provider_restriction";

	public const string RedirectUri = "redirect_uri";

	public const string PostLogoutRedirectUri = "post_logout_redirect_uri";

	public const string AllowedCorsOrigin = "allowed_cors_origin";

	public const string FrontChannelLogoutUri = "front_channel_logout_uri";

	public const string BackChannelLogoutUri = "back_channel_logout_uri";
}
