using RoyalIdentity.Data.Operational.Entities;

namespace RoyalIdentity.Storage.EntityFramework.Operational.Materialization;

/// <summary>
/// The record types of the Operational family, used to separate lookup-digest domains (plan DF38) and to
/// bind the authenticated context of a protected payload (plan DF30). The three artifact types come from
/// <see cref="ProtocolArtifactTypes"/> so the discriminator persisted in <c>protocol_artifacts</c> and the
/// domain separator can never drift apart; consents and authorize parameters have their own tables and their
/// own names here.
/// </summary>
public static class OperationalRecordTypes
{
	/// <inheritdoc cref="ProtocolArtifactTypes.AccessToken"/>
	public const string AccessToken = ProtocolArtifactTypes.AccessToken;

	/// <inheritdoc cref="ProtocolArtifactTypes.RefreshToken"/>
	public const string RefreshToken = ProtocolArtifactTypes.RefreshToken;

	/// <inheritdoc cref="ProtocolArtifactTypes.AuthorizationCode"/>
	public const string AuthorizationCode = ProtocolArtifactTypes.AuthorizationCode;

	/// <summary>Consents (table <c>consents</c>).</summary>
	public const string Consent = "consent";

	/// <summary>Authorize-parameters continuations (table <c>authorize_parameters</c>).</summary>
	public const string AuthorizeParameters = "authorize_parameters";
}
