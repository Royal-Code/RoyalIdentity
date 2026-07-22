namespace RoyalIdentity.Configuration;

/// <summary>
/// Options for the periodic configuration snapshot refresher (plan DF7). The interval is mandatory and must be
/// positive in every composition that registers the refresher — it never receives a hidden default.
/// </summary>
public sealed class ConfigurationSnapshotRefreshOptions
{
	/// <summary>How often the snapshot is reloaded from its source after the initial bootstrap.</summary>
	public TimeSpan RefreshInterval { get; set; }

	/// <summary>Throws when the interval is not a positive duration (validated at host start).</summary>
	public void Validate()
	{
		if (RefreshInterval <= TimeSpan.Zero)
			throw new InvalidOperationException(
				$"{nameof(ConfigurationSnapshotRefreshOptions)}.{nameof(RefreshInterval)} must be a positive duration.");
	}
}
