namespace RoyalIdentity.Users;

/// <summary>
/// A client the subject signed into during a session. Equality is by <see cref="ClientId"/> only, so a
/// session's client set deduplicates by client (pontos1 §1) regardless of the seen-at timestamps —
/// e.g. a <see cref="HashSet{T}"/> of these keeps one entry per client id.
/// </summary>
/// <param name="ClientId">The client identifier.</param>
/// <param name="FirstSeenAt">When the client was first recorded on the session.</param>
/// <param name="LastSeenAt">When the client was most recently recorded on the session.</param>
public sealed record UserSessionClient(string ClientId, DateTime FirstSeenAt, DateTime LastSeenAt)
{
    /// <summary>Equality is by <see cref="ClientId"/> only (dedup per client).</summary>
    public bool Equals(UserSessionClient? other) => other is not null && ClientId == other.ClientId;

    /// <inheritdoc />
    public override int GetHashCode() => ClientId.GetHashCode();
}
