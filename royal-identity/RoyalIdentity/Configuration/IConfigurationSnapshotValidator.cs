namespace RoyalIdentity.Configuration;

/// <summary>
/// Cross-cutting validation applied to freshly loaded configuration before it is published (plan-localization
/// DF8). Every registered validator runs after <see cref="IConfigurationSnapshotSource.LoadAsync"/> and before
/// <c>Publish</c>, so invalid configuration fails startup and an invalid refresh keeps the last-known-good
/// snapshot instead of replacing it.
/// </summary>
/// <remarks>
/// This is the seam a composition uses to assert things the core cannot know on its own — the UI locale
/// catalogue being the first case. Validators must not mutate the data.
/// </remarks>
public interface IConfigurationSnapshotValidator
{
    /// <summary>
    /// Returns the configuration errors found in <paramref name="data"/>. Empty means valid.
    /// </summary>
    ValueTask<IReadOnlyList<string>> ValidateAsync(ConfigurationSnapshotData data, CancellationToken ct);
}
