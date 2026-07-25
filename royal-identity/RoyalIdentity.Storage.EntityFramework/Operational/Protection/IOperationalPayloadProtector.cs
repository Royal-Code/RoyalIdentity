namespace RoyalIdentity.Storage.EntityFramework.Operational.Protection;

/// <summary>
/// <para>
///     One registered operational payload protection profile (plan DF30). A realm selects a profile
///     <em>by id</em>: the configuration payload holds the id and nothing else — never a key, a secret or a
///     key-ring path.
/// </para>
/// <para>
///     Deliberately not <c>IKeyMaterialProtector</c>: signing-key material and operational payloads have
///     different purposes, different lifecycles and, here, an authenticated context
///     (<see cref="OperationalProtectionContext"/>). Only the cipher mechanics are shared.
/// </para>
/// </summary>
public interface IOperationalPayloadProtector
{
    /// <summary>
    /// Stable identifier persisted in the envelope of everything this profile writes, so a later rotation only
    /// needs the previous profile to stay registered as a reader.
    /// </summary>
    string ProfileId { get; }

    /// <summary>Protects a payload, binding it to <paramref name="context"/>.</summary>
    ValueTask<string> ProtectAsync(string payload, OperationalProtectionContext context, CancellationToken ct = default);

    /// <summary>
    /// Restores a payload this profile produced. Fails when the value is malformed, tampered with, or was
    /// produced under a different context.
    /// </summary>
    ValueTask<string> UnprotectAsync(string protectedPayload, OperationalProtectionContext context, CancellationToken ct = default);
}
