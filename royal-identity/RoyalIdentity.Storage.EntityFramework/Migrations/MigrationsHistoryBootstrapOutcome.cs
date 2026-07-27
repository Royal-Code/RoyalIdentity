namespace RoyalIdentity.Storage.EntityFramework.Migrations;

/// <summary>
/// What a migrations-history bootstrap found and did (plan DF23). The outcome is shared by every provider
/// because the decision is the same everywhere — only the way a table is located and moved differs.
/// </summary>
public enum MigrationsHistoryBootstrapOutcome
{
    /// <summary>Neither the Configuration history nor any legacy history exists.</summary>
    NoHistory,

    /// <summary>The legacy history existed and was moved, preserving every applied migration id.</summary>
    Relocated,

    /// <summary>
    /// The Configuration history already exists, so no Configuration relocation is required. A legacy history
    /// owned by another family may coexist and is left untouched.
    /// </summary>
    AlreadyRelocated,

    /// <summary>
    /// A legacy history exists, but none of its migration ids belong to Configuration. It is left untouched for
    /// its owning family.
    /// </summary>
    ForeignHistory,
}
