using Microsoft.Extensions.Configuration;
using RoyalIdentity.UserAccounts.Options;

namespace RoyalIdentity.UserAccounts.Integration;

/// <summary>
/// Resolves <see cref="UserAccountsRealmOptions"/> from configuration, applying global defaults first and then
/// the override whose key is the stable realm identifier.
/// </summary>
public sealed class ConfigurationUserAccountsRealmOptionsResolver : IUserAccountsRealmOptionsResolver
{
    /// <summary>The child section containing the global defaults.</summary>
    public const string DefaultsSectionName = "Defaults";

    /// <summary>The child section containing overrides keyed by realm identifier.</summary>
    public const string RealmsSectionName = "Realms";

    private readonly IConfigurationSection section;

    /// <summary>
    /// Creates the resolver over a section shaped as
    /// <c>{ Defaults: { ... }, Realms: { realmId: { ... } } }</c>.
    /// </summary>
    public ConfigurationUserAccountsRealmOptionsResolver(IConfigurationSection section)
    {
        ArgumentNullException.ThrowIfNull(section);
        this.section = section;
    }

    /// <inheritdoc />
    public UserAccountsRealmOptions Resolve(string realmId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(realmId);

        var options = BindDefaults();
        BindOverride(options, section.GetSection(RealmsSectionName).GetSection(realmId));
        options.EnsureValid();
        return options;
    }

    internal IReadOnlyList<string> ValidateConfiguration()
    {
        List<string> errors = [];

        var defaults = BindDefaults();
        AddValidationErrors(errors, $"{section.Path}:{DefaultsSectionName}", defaults);

        foreach (var realmSection in section.GetSection(RealmsSectionName).GetChildren())
        {
            var options = new UserAccountsRealmOptions(defaults);
            BindOverride(options, realmSection);
            AddValidationErrors(errors, realmSection.Path, options);
        }

        return errors;
    }

    private UserAccountsRealmOptions BindDefaults()
    {
        var options = new UserAccountsRealmOptions();
        section.GetSection(DefaultsSectionName).Bind(options);
        return options;
    }

    private static void BindOverride(UserAccountsRealmOptions options, IConfigurationSection overrideSection)
    {
        if (!overrideSection.Exists())
            return;

        // IConfigurationBinder appends to existing collections. Overrides are replacement values for the two
        // configurable collections, while scalar/nested object properties inherit the already-bound defaults.
        if (overrideSection.GetSection(nameof(UserAccountsRealmOptions.FixedFieldClaimProjections)).Exists())
            options.FixedFieldClaimProjections = [];

        var disallowedWords = overrideSection
            .GetSection(nameof(UserAccountsRealmOptions.PasswordOptions))
            .GetSection(nameof(PasswordOptions.DisallowedWordsInPassword));

        if (disallowedWords.Exists())
            options.PasswordOptions.DisallowedWordsInPassword = [];

        overrideSection.Bind(options);
    }

    private static void AddValidationErrors(
        ICollection<string> errors,
        string path,
        UserAccountsRealmOptions options)
    {
        foreach (var error in options.Validate())
            errors.Add($"{path}: {error}");
    }
}
