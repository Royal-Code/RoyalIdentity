namespace RoyalIdentity.Data.Operational.Entities;

/// <summary>
/// Discriminator values of <see cref="ProtocolArtifactEntity.ArtifactType"/> (plan DF36). The value is part of
/// the primary key, of every query and of every cleanup predicate, so a typed store can never read or mutate a
/// row of another lifecycle. A new artifact with one principal key, a realm, an expiration and a compatible
/// lifecycle registers a discriminator here; needing queryable fields, multiple keys or own relations requires
/// an explicit relational evolution instead.
/// </summary>
public static class ProtocolArtifactTypes
{
    /// <summary>Reference access tokens (always persisted) and JWT metadata/full when the realm enables it.</summary>
    public const string AccessToken = "access_token";

    /// <summary>Refresh tokens.</summary>
    public const string RefreshToken = "refresh_token";

    /// <summary>Authorization codes.</summary>
    public const string AuthorizationCode = "authorization_code";

    /// <summary>Every discriminator known to this version of the model.</summary>
    public static IReadOnlyList<string> All { get; } = [AccessToken, RefreshToken, AuthorizationCode];
}
