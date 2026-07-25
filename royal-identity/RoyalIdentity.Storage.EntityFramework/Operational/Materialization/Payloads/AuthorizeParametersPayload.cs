using System.Collections.Specialized;

namespace RoyalIdentity.Storage.EntityFramework.Operational.Materialization.Payloads;

/// <summary>
/// The persisted authorize parameters. A <see cref="NameValueCollection"/> allows repeated keys, so each entry
/// keeps its own list of values instead of a single string, and the round-trip reproduces the collection
/// exactly.
/// </summary>
public sealed class AuthorizeParametersPayload
{
	public List<AuthorizeParameterPayload> Parameters { get; set; } = [];
}

/// <summary>One key of the authorize parameters, with every value it holds — including a null value.</summary>
public sealed class AuthorizeParameterPayload
{
	/// <summary>The key. A <see cref="NameValueCollection"/> allows a null key, so it is nullable here too.</summary>
	public string? Name { get; set; }

	public List<string?> Values { get; set; } = [];
}
