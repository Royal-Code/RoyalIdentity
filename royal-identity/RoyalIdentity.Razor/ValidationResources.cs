namespace RoyalIdentity.Razor;

/// <summary>
/// Marker type for the shared validation catalogue used by SSR DataAnnotations messages and field display
/// names (plan-localization DF18).
/// </summary>
/// <remarks>
/// Same base-name rule as <see cref="AccountResources"/>: root namespace plus
/// <c>ResourcesPath = "Resources"</c>.
/// </remarks>
public sealed class ValidationResources
{
    private ValidationResources()
    {
    }
}
