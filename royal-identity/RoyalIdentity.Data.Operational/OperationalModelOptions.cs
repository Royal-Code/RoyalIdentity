namespace RoyalIdentity.Data.Operational;

/// <summary>
/// Options for building the Operational model outside any concrete <c>DbContext</c> (plan DF1/DF2): the same
/// values drive the default contexts and third-party contexts combining Configuration and Operational.
/// </summary>
public sealed class OperationalModelOptions
{
    /// <summary>
    /// Relational schema for the Operational tables. <c>null</c> means no schema (SQLite); the PostgreSQL
    /// mapping extension defaults it to <c>operation</c> (plan DF4).
    /// </summary>
    public string? Schema { get; set; }
}
