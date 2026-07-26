namespace RoyalIdentity.Storage.EntityFramework.Migrations;

/// <summary>
/// What a migrations-history bootstrap found and did (plan DF23). The outcome is shared by every provider
/// because the decision is the same everywhere — only the way a table is located and moved differs.
/// </summary>
public enum MigrationsHistoryBootstrapOutcome
{
    /// <summary>Neither the legacy nor the Configuration history exists — a database that was never migrated.</summary>
    NoHistory,

    /// <summary>The legacy history existed and was moved, preserving every applied migration id.</summary>
    Relocated,

    /// <summary>Only the Configuration history exists — the move already happened, so this run is a no-op.</summary>
    AlreadyRelocated,
}
