using RoyalIdentity.Models.Tokens;
using RoyalIdentity.Storage.EntityFramework.Operational.Materialization.Payloads;

namespace RoyalIdentity.Storage.EntityFramework.Operational.Materialization;

/// <summary>
/// Serializes the non-queryable graph of an <see cref="AuthorizationCode"/> to a versioned payload and back
/// (plan DF9): the subject principal, the resolved resources and the code's own properties.
/// <para>
/// Client, redirect URI, session and the timestamps come back from <see cref="AuthorizationCodeIdentity"/>,
/// never from the payload. That is a correctness requirement, not a preference: the conditional consumption of
/// DF11 matches the client and the redirect URI in the database, so the object handed to the pipeline must
/// carry exactly the values that condition evaluated.
/// </para>
/// </summary>
public sealed class AuthorizationCodePayloadSerializer
{
    /// <summary>Current payload schema version.</summary>
    public const int CurrentVersion = 1;

    private readonly OperationalPayloadCodec<AuthorizationCodePayload> codec =
        new(nameof(AuthorizationCode), CurrentVersion);

    public (int Version, string Json) Serialize(AuthorizationCode code)
    {
        ArgumentNullException.ThrowIfNull(code);

        var payload = new AuthorizationCodePayload
        {
            SessionState = code.SessionState,
            Nonce = code.Nonce,
            StateHash = code.StateHash,
            CodeChallenge = code.CodeChallenge,
            CodeChallengeMethod = code.CodeChallengeMethod,
            Properties = code.Properties is null ? null : new Dictionary<string, string>(code.Properties),
            Subject = ClaimsPrincipalPayload.From(code.Subject),
            Scopes = RequestedResourcesPayload.From(code.Scopes),
        };

        return (CurrentVersion, codec.Serialize(payload));
    }

    /// <param name="version">The persisted payload version.</param>
    /// <param name="json">The persisted payload.</param>
    /// <param name="identity">The authoritative relational identity of the row.</param>
    public AuthorizationCode Deserialize(int version, string json, AuthorizationCodeIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(identity);

        var payload = codec.Deserialize(version, json);

        return new AuthorizationCode(
            identity.Code,
            identity.ClientId,
            payload.Subject.ToClaimsPrincipal(nameof(AuthorizationCode)),
            payload.SessionState,
            identity.CreatedAtUtc,
            identity.Lifetime,
            payload.Scopes.ToRequestedResources(nameof(AuthorizationCode)),
            identity.RedirectUri)
        {
            RealmId = identity.RealmId,
            SessionId = identity.SessionId,
            Nonce = payload.Nonce,
            StateHash = payload.StateHash,
            CodeChallenge = payload.CodeChallenge,
            CodeChallengeMethod = payload.CodeChallengeMethod,
            Properties = payload.Properties is null ? null : new Dictionary<string, string>(payload.Properties),
        };
    }
}
