namespace RoyalIdentity.Options;

/// <summary>
/// Realm-scoped refresh-token policy (plan-data-operational-storage DF32). It replaces the removed
/// per-client <c>UpdateAccessTokenClaimsOnRefresh</c> flag: the origin of the claims of a renewed token is a
/// realm decision, with no client override and no competing precedences.
/// </summary>
public class RefreshTokenOptions
{
    /// <summary>Creates a new instance with the closed defaults.</summary>
    public RefreshTokenOptions()
    {
    }

    /// <summary>Creates an independent copy of another instance.</summary>
    public RefreshTokenOptions(RefreshTokenOptions other)
    {
        ArgumentNullException.ThrowIfNull(other);

        ClaimsMode = other.ClaimsMode;
    }

    /// <summary>
    /// Gets or sets where the claims of a renewed token come from. The mode in force when a refresh token is
    /// issued is captured on the token itself, so changing this value later never reinterprets tokens that
    /// already exist.
    /// </summary>
    public RefreshTokenClaimsMode ClaimsMode { get; set; } = RefreshTokenClaimsMode.Current;

    /// <summary>
    /// Validates internal consistency of the refresh-token options.
    /// </summary>
    /// <returns>A list of configuration errors. Empty means valid.</returns>
    public IReadOnlyList<string> Validate()
    {
        List<string> errors = [];

        if (!Enum.IsDefined(ClaimsMode))
        {
            errors.Add("RefreshTokens.ClaimsMode is not a valid mode.");
        }

        return errors;
    }
}
