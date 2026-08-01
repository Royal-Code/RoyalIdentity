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
    public ICollection<string> SensitiveValuesFilter { get; set; } =
        [
            Oidc.Token.Request.ClientSecret,
            Oidc.Token.Request.Password,
            Oidc.Token.Request.ClientAssertion,
            Oidc.Token.Request.RefreshToken,
            Oidc.Token.Request.DeviceCode,
            Oidc.Authorize.Request.IdTokenHint,

            // Single-use credentials that were reaching the log in clear on every refused exchange — which is
            // most of the paths that log at all. A log is read by more people, for longer, than a response is.
            Oidc.Token.Request.Code,
            Oidc.Token.Request.CodeVerifier
        ];

    /// <summary>
    /// Determines whether endpoint error logs should be sent to a log service for alternative and additional handling.
    /// </summary>
    public bool UseLogService { get; internal set; }
}
