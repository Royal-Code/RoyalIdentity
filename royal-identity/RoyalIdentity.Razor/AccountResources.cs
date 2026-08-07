namespace RoyalIdentity.Razor;

/// <summary>
/// Marker type for the account UI catalogue (plan-localization DF3/DF4). Consumers take
/// <c>IStringLocalizer&lt;AccountResources&gt;</c>; nobody touches <c>ResourceManager</c> or a generated
/// designer class.
/// </summary>
/// <remarks>
/// The type lives in the assembly root namespace on purpose: with <c>ResourcesPath = "Resources"</c> the
/// framework resolves the base name to <c>RoyalIdentity.Razor.Resources.AccountResources</c>, which is exactly
/// where the <c>.resx</c> files are. Moving this type into a nested namespace would silently break resolution
/// — the localizer would start echoing key names instead of failing.
/// </remarks>
public sealed class AccountResources
{
    private AccountResources()
    {
    }
}
