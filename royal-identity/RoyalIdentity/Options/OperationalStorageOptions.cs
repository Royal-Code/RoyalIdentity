namespace RoyalIdentity.Options;

/// <summary>
/// Realm-scoped policy for the operational data store (plan-data-operational-storage DF30/DF31). These are
/// realm decisions only — there is no per-client override — and they affect new writes/issuances only:
/// records already persisted keep the profile and the mode they were written with.
/// </summary>
public class OperationalStorageOptions
{
    /// <summary>
    /// Identifier of the payload protection profile every realm falls back to when nothing else is selected.
    /// It is a name the composition must register — never an implicit algorithm or protector (DF30).
    /// </summary>
    public const string DefaultPayloadProtectionProfile = "default";

    /// <summary>Creates a new instance with the closed defaults.</summary>
    public OperationalStorageOptions()
    {
    }

    /// <summary>Creates an independent copy of another instance.</summary>
    public OperationalStorageOptions(OperationalStorageOptions other)
    {
        ArgumentNullException.ThrowIfNull(other);

        PayloadProtectionProfile = other.PayloadProtectionProfile;
        JwtAccessTokenPersistence = other.JwtAccessTokenPersistence;
    }

    /// <summary>
    /// <para>
    ///     Gets or sets the identifier of the payload protection profile used for this realm's new operational
    ///     writes. The realm stores an id only: keys, secrets and key-ring paths belong to the host that
    ///     registers the profile and never enter the configuration payload (DF30).
    /// </para>
    /// <para>
    ///     The envelope of each record keeps the profile that actually wrote it, so a rotation only needs the
    ///     previous profile to stay registered as a reader. An unregistered profile fails closed — it never
    ///     falls back to unprotected storage.
    /// </para>
    /// </summary>
    public string PayloadProtectionProfile { get; set; } = DefaultPayloadProtectionProfile;

    /// <summary>
    /// Gets or sets how much of a JWT access token this realm persists (DF31). Reference access tokens are
    /// always persisted regardless of this value.
    /// </summary>
    public JwtAccessTokenPersistenceMode JwtAccessTokenPersistence { get; set; } = JwtAccessTokenPersistenceMode.None;

    /// <summary>
    /// Validates internal consistency of the operational storage options.
    /// </summary>
    /// <returns>A list of configuration errors. Empty means valid.</returns>
    public IReadOnlyList<string> Validate()
    {
        List<string> errors = [];

        if (string.IsNullOrWhiteSpace(PayloadProtectionProfile))
        {
            errors.Add("OperationalStorage.PayloadProtectionProfile must name a protection profile registered by the composition.");
        }

        if (!Enum.IsDefined(JwtAccessTokenPersistence))
        {
            errors.Add("OperationalStorage.JwtAccessTokenPersistence is not a valid mode.");
        }

        return errors;
    }
}
