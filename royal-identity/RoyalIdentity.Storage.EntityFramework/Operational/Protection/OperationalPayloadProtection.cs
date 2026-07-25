using RoyalIdentity.Models;

namespace RoyalIdentity.Storage.EntityFramework.Operational.Protection;

/// <summary>
/// The seam the operational stores use to turn a serialized payload into the value persisted in
/// <c>protected_payload</c> and back (plan DF30). It resolves the effective profile from the realm snapshot
/// before the operation, wraps the result in a versioned <see cref="OperationalPayloadEnvelope"/> and, on
/// read, selects the reader from the envelope — not from the realm's current option.
/// </summary>
public sealed class OperationalPayloadProtection(OperationalPayloadProtectorResolver resolver)
{
	/// <summary>
	/// Protects <paramref name="payload"/> with the profile selected by <paramref name="realm"/> and returns
	/// the value to persist.
	/// </summary>
	public async ValueTask<string> ProtectAsync(
		Realm realm, string payload, OperationalProtectionContext context, CancellationToken ct = default)
	{
		ArgumentNullException.ThrowIfNull(realm);

		var protector = resolver.GetForWrite(realm.Options.OperationalStorage.PayloadProtectionProfile);
		var protectedPayload = await protector.ProtectAsync(payload, context, ct);

		return new OperationalPayloadEnvelope(protector.ProfileId, protectedPayload).ToPersistedValue();
	}

	/// <summary>
	/// Restores a persisted value with the profile its envelope records. An unknown envelope version, an
	/// unregistered reader, a tampered payload or a context mismatch all fail closed.
	/// </summary>
	public async ValueTask<string> UnprotectAsync(
		string persistedValue, OperationalProtectionContext context, CancellationToken ct = default)
	{
		var envelope = OperationalPayloadEnvelope.Parse(persistedValue);
		var protector = resolver.GetForRead(envelope.ProtectorId);

		return await protector.UnprotectAsync(envelope.Payload, context, ct);
	}
}
