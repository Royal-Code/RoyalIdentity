namespace RoyalIdentity.Razor.Localization;

/// <summary>
/// Keys of the shared validation catalogue (plan-localization DF18).
/// </summary>
/// <remarks>
/// DataAnnotations attributes must hold a compile-time constant, and <c>ErrorMessageResourceType</c> would
/// require a generated designer class — which DF3 rules out. So the attributes carry these keys and
/// <c>LocalizedValidationSummary</c> resolves them in the request's culture at render time.
/// </remarks>
public static class ValidationMessages
{
    public const string Required = "Validation_Required";
    public const string FieldUsername = "Field_Username";
    public const string FieldPassword = "Field_Password";
    public const string FieldReturnUrl = "Field_ReturnUrl";
    public const string FieldDomain = "Field_Domain";
}
