namespace RoyalIdentity.Options;

/// <summary>
/// How much of a JWT access token the realm persists in the Operational store
/// (plan-data-operational-storage DF31). It never affects reference access tokens, which are always
/// persisted, and it never turns persistence into revocation: stateless validation does not consult the store.
/// </summary>
public enum JwtAccessTokenPersistenceMode
{
    /// <summary>Nothing is written: a JWT access token produces no artifact at all. The default.</summary>
    None = 0,

    /// <summary>
    /// Queryable metadata only (subject, client, session, type, timestamps). The compact JWT is not written.
    /// </summary>
    Metadata = 1,

    /// <summary>
    /// The complete graph plus the compact JWT, inside the payload protected by the realm's profile
    /// (see <see cref="OperationalStorageOptions.PayloadProtectionProfile"/>).
    /// </summary>
    Full = 2
}
