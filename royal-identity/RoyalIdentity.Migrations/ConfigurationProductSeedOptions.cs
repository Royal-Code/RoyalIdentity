namespace RoyalIdentity.Migrations;

/// <summary>Environment-specific inputs required by the Configuration product seed.</summary>
public sealed class ConfigurationProductSeedOptions
{
	/// <summary>Explicit redirect URIs assigned to the public <c>server_admin</c> client.</summary>
	public IReadOnlyList<string> ServerAdminRedirectUris { get; init; } = [];

	internal void Validate()
	{
		if (ServerAdminRedirectUris.Count is 0)
		{
			throw new InvalidOperationException(
				"At least one server_admin redirect URI is required by the Configuration product seed.");
		}

		var unique = new HashSet<string>(StringComparer.Ordinal);
		foreach (var redirectUri in ServerAdminRedirectUris)
		{
			if (string.IsNullOrWhiteSpace(redirectUri)
				|| !Uri.TryCreate(redirectUri, UriKind.Absolute, out _))
			{
				throw new InvalidOperationException(
					"Every server_admin redirect URI must be a non-empty absolute URI.");
			}

			if (!unique.Add(redirectUri))
				throw new InvalidOperationException("Duplicate server_admin redirect URIs are not allowed.");
		}
	}
}
