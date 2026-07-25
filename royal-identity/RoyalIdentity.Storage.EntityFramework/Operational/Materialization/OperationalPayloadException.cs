namespace RoyalIdentity.Storage.EntityFramework.Operational.Materialization;

/// <summary>
/// Raised when an operational payload cannot be serialized or materialized (plan DF9). Every case fails
/// closed: an unknown version, malformed JSON or a structurally invalid payload never produces a partially
/// materialized model. Messages never include payloads, handles, claims or subjects (plan DF28).
/// </summary>
public sealed class OperationalPayloadException : Exception
{
    private OperationalPayloadException(string message) : base(message)
    {
    }

    private OperationalPayloadException(string message, Exception innerException) : base(message, innerException)
    {
    }

    /// <summary>The persisted payload version is not the one this build writes and reads.</summary>
    public static OperationalPayloadException UnsupportedVersion(string payloadName, int version, int currentVersion)
        => new($"The persisted {payloadName} payload has version {version}; this build supports version {currentVersion}.");

    /// <summary>The payload is not valid JSON.</summary>
    public static OperationalPayloadException InvalidJson(string payloadName, Exception innerException)
        => new($"The persisted {payloadName} payload is not valid JSON.", innerException);

    /// <summary>The payload deserialized to nothing.</summary>
    public static OperationalPayloadException EmptyPayload(string payloadName)
        => new($"The persisted {payloadName} payload is empty.");

    /// <summary>The payload is well-formed JSON but cannot produce a complete model.</summary>
    public static OperationalPayloadException IncompletePayload(string payloadName, string detail)
        => new($"The persisted {payloadName} payload is incomplete: {detail}.");

    /// <summary>
    /// The relational identity of the record is self-contradictory, so no coherent model can be produced from
    /// it — for example an expiration that precedes the creation instant.
    /// </summary>
    public static OperationalPayloadException IncoherentRecord(string payloadName, string detail)
        => new($"The persisted {payloadName} record is incoherent: {detail}.");
}
