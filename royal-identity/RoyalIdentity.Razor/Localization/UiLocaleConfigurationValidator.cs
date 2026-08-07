using RoyalIdentity.Configuration;
using RoyalIdentity.Contracts.Localization;

namespace RoyalIdentity.Razor.Localization;

/// <summary>
/// Refuses to publish a configuration snapshot whose realms offer locales this UI cannot render
/// (plan-localization DF8). The core cannot make this check on its own — only the composed UI knows what it
/// ships — so the assertion enters through <see cref="IConfigurationSnapshotValidator"/>.
/// </summary>
/// <remarks>
/// The consequence is deliberate: a realm configured for a locale without a catalogue fails startup, and a bad
/// refresh keeps the last-known-good snapshot, instead of silently serving English to users who asked for
/// something else.
/// </remarks>
public sealed class UiLocaleConfigurationValidator(IUiLocaleCatalog catalog) : IConfigurationSnapshotValidator
{
    /// <inheritdoc />
    public ValueTask<IReadOnlyList<string>> ValidateAsync(ConfigurationSnapshotData data, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(data);

        List<string> errors = [];

        // A host without a composed UI has nothing to promise and nothing to contradict; metadata stays absent
        // by DF14 rather than failing every realm here.
        if (catalog.NeutralLocale is null)
            return ValueTask.FromResult<IReadOnlyList<string>>(errors);

        foreach (var realm in data.Realms)
        {
            var internationalization = realm.Options.Internationalization;
            if (!internationalization.Enabled)
                continue;

            foreach (var locale in internationalization.SupportedLocales)
            {
                if (!catalog.Supports(locale))
                {
                    errors.Add(
                        $"Realm '{realm.Path}' offers the locale '{locale}', which the composed user " +
                        "interface has no catalogue for.");
                }
            }
        }

        return ValueTask.FromResult<IReadOnlyList<string>>(errors);
    }
}
