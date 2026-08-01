using System.Collections.Frozen;

namespace RoyalIdentity.Options;

/// <summary>
/// Options for configuring logging behavior
/// </summary>
public class LoggingOptions
{
    /// <summary>
    /// Creates a new instance of <see cref="LoggingOptions"/>.
    /// </summary>
    public LoggingOptions()
    {
    }

    /// <summary>
    /// Creates a new instance of <see cref="LoggingOptions"/> copying values from another instance.
    /// </summary>
    /// <param name="other">The options to copy.</param>
    public LoggingOptions(LoggingOptions other)
    {
        SensitiveValuesFilter = [.. other.SensitiveValuesFilter];
        UseLogService = other.UseLogService;
    }

    /// <summary>
    /// 
    /// </summary>
    public ICollection<string> SensitiveValuesFilter { get; set; } = [];

    /// <summary>
    /// Parameter names redacted from logs whatever the configuration says.
    /// </summary>
    /// <remarks>
    /// <para>
    /// These carry a credential or a single-use artifact, so redacting them is not a preference. They used to
    /// be the default value of <see cref="SensitiveValuesFilter"/>, which made the protection removable — and,
    /// worse, made it impossible to extend: <c>RealmOptions</c> is serialized whole into the Configuration
    /// payload, so a realm persisted before a name was added keeps the older list and silently loses the new
    /// protection. That is exactly how <c>code</c> and <c>code_verifier</c> would have gone on being logged in
    /// clear after being added to the default.
    /// </para>
    /// <para>
    /// <see cref="SensitiveValuesFilter"/> stays as the operator's own additions — names this product cannot
    /// know, such as parameters of a custom extension grant. It only ever adds.
    /// </para>
    /// </remarks>
    public static readonly FrozenSet<string> AlwaysRedacted = new[]
    {
        Oidc.Token.Request.ClientSecret,
        Oidc.Token.Request.Password,
        Oidc.Token.Request.ClientAssertion,
        Oidc.Token.Request.RefreshToken,
        Oidc.Token.Request.DeviceCode,
        Oidc.Authorize.Request.IdTokenHint,
        Oidc.Token.Request.Code,
        Oidc.Token.Request.CodeVerifier
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Every parameter name redacted for this realm: the mandatory floor plus whatever configuration added.
    /// </summary>
    public IEnumerable<string> RedactedParameterNames => AlwaysRedacted.Concat(SensitiveValuesFilter);

    /// <summary>
    /// Determines whether endpoint error logs should be sent to a log service for alternative and additional handling.
    /// </summary>
    public bool UseLogService { get; internal set; }
}
