namespace RoyalIdentity.Options;

/// <summary>
/// Options for configuring logging behavior
/// </summary>
public class LoggingOptions
{
    /// <summary>
    /// 
    /// </summary>
    public ICollection<string> SensitiveValuesFilter { get; set; } =
        [
            OidcConstants.TokenRequest.ClientSecret,
            OidcConstants.TokenRequest.Password,
            OidcConstants.TokenRequest.ClientAssertion,
            OidcConstants.TokenRequest.RefreshToken,
            OidcConstants.TokenRequest.DeviceCode,
            OidcConstants.AuthorizeRequest.IdTokenHint
        ];

    /// <summary>
    /// Determines whether endpoint error logs should be sent to a log service for alternative and additional handling.
    /// </summary>
    public bool UseLogService { get; internal set; }
}
