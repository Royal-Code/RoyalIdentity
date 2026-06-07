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
            Oidc.Authorize.Request.IdTokenHint
        ];

    /// <summary>
    /// Determines whether endpoint error logs should be sent to a log service for alternative and additional handling.
    /// </summary>
    public bool UseLogService { get; internal set; }
}
