namespace RoyalIdentity.Data.Configuration;

/// <summary>
/// Options for building the Configuration model outside any concrete <c>DbContext</c> (plan DF3): the same
/// values drive the default contexts and third-party combined contexts.
/// </summary>
public sealed class ConfigurationModelOptions
{
	/// <summary>
	/// Relational schema for the Configuration tables. <c>null</c> means no schema (SQLite); the PostgreSQL
	/// mapping extension defaults it to <c>configuration</c> (plan DF18).
	/// </summary>
	public string? Schema { get; set; }
}
