using Microsoft.Extensions.Logging;

namespace RoyalIdentity.Storage.EntityFramework.Operational.Protection;

/// <summary>
/// Explicit development/operator opt-in that stores operational payloads without encryption. It is never a
/// fallback: it exists only when the composition registers it AND a realm selects its id, and it warns on
/// construction (plan DF30). Because it cannot authenticate anything, it cannot detect a payload replayed
/// from another realm, record or version — which is exactly why it must be an explicit choice. Combining it
/// with <c>JwtAccessTokenPersistence.Full</c> writes a reusable bearer in the clear and requires both opt-ins
/// deliberately.
/// </summary>
public sealed class PlainOperationalPayloadProtector : IOperationalPayloadProtector
{
	public PlainOperationalPayloadProtector(string profileId, ILogger<PlainOperationalPayloadProtector> logger)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(profileId);
		ArgumentNullException.ThrowIfNull(logger);

		ProfileId = profileId;

		logger.LogWarning(
			"Plain operational payload protection is active under the profile '{ProfileId}'. Persisted tokens, " +
			"codes, consents and authorize parameters are not encrypted at rest.",
			profileId);
	}

	public string ProfileId { get; }

	public ValueTask<string> ProtectAsync(
		string payload, OperationalProtectionContext context, CancellationToken ct = default)
	{
		ArgumentException.ThrowIfNullOrEmpty(payload);
		ArgumentNullException.ThrowIfNull(context);
		ct.ThrowIfCancellationRequested();

		return ValueTask.FromResult(payload);
	}

	public ValueTask<string> UnprotectAsync(
		string protectedPayload, OperationalProtectionContext context, CancellationToken ct = default)
	{
		ArgumentException.ThrowIfNullOrEmpty(protectedPayload);
		ArgumentNullException.ThrowIfNull(context);
		ct.ThrowIfCancellationRequested();

		return ValueTask.FromResult(protectedPayload);
	}
}
