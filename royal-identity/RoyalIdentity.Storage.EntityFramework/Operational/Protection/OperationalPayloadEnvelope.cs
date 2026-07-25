namespace RoyalIdentity.Storage.EntityFramework.Operational.Protection;

/// <summary>
/// Versioned envelope persisted in <c>protected_payload</c> (plan DF30). The profile that actually wrote the
/// record travels inside the envelope — not in a separate column and not inferred from the realm's current
/// option — so rotating a realm's profile only affects new writes and existing records stay readable by the
/// profile still registered for them. <see cref="ToString"/> deliberately omits the payload (plan DF28).
/// </summary>
public sealed class OperationalPayloadEnvelope
{
    public const int CurrentVersion = 1;

    private const char Separator = ':';

    public OperationalPayloadEnvelope(string protectorId, string payload, int version = CurrentVersion)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(protectorId);
        ArgumentException.ThrowIfNullOrEmpty(payload);

        if (protectorId.Contains(Separator, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"An operational protection profile id must not contain '{Separator}'.", nameof(protectorId));
        }

        ProtectorId = protectorId;
        Payload = payload;
        Version = version;
    }

    /// <summary>The profile that produced <see cref="Payload"/>.</summary>
    public string ProtectorId { get; }

    /// <summary>The envelope format version.</summary>
    public int Version { get; }

    /// <summary>The opaque protected payload.</summary>
    public string Payload { get; }

    /// <summary>The persisted representation: <c>v{version}:{profileId}:{payload}</c>.</summary>
    public string ToPersistedValue() => $"v{Version}{Separator}{ProtectorId}{Separator}{Payload}";

    /// <summary>
    /// Parses a persisted value. Fails closed — an unknown version or a malformed value never yields a partial
    /// envelope.
    /// </summary>
    public static OperationalPayloadEnvelope Parse(string persistedValue)
    {
        ArgumentException.ThrowIfNullOrEmpty(persistedValue);

        var parts = persistedValue.Split(Separator, 3);
        if (parts.Length is not 3
            || parts[0].Length < 2
            || parts[0][0] != 'v'
            || !int.TryParse(parts[0].AsSpan(1), out var version)
            || version != CurrentVersion
            || parts[1].Length is 0
            || parts[2].Length is 0)
        {
            throw OperationalPayloadProtectionException.InvalidEnvelope();
        }

        return new OperationalPayloadEnvelope(parts[1], parts[2], version);
    }

    public override string ToString()
        => $"OperationalPayloadEnvelope {{ ProtectorId = {ProtectorId}, Version = {Version}, Payload = [REDACTED] }}";
}
