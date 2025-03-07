﻿using RoyalIdentity.Extensions;

namespace RoyalIdentity.Options;

/// <summary>
/// Options for aspects of the user interface.
/// </summary>
public class UserInteractionOptions
{
    /// <summary>
    /// Gets or sets the login URL. If a local URL, the value must start with a leading slash.
    /// </summary>
    /// <value>
    /// The login URL.
    /// </value>
    public string LoginPath { get; set; } = Constants.UIConstants.DefaultRoutePaths.Login.EnsureLeadingSlash();

    /// <summary>
    /// Gets or sets the login return URL parameter.
    /// </summary>
    /// <value>
    /// The login return URL parameter.
    /// </value>
    public string ReturnUrlParameter { get; set; } = Constants.UIConstants.DefaultRoutePathParams.Login;

    /// <summary>
    /// Gets or sets the logout URL. If a local URL, the value must start with a leading slash.
    /// </summary>
    /// <value>
    /// The logout URL.
    /// </value>
    public string LogoutPath { get; set; } = Constants.UIConstants.DefaultRoutePaths.Logout.EnsureLeadingSlash();

    /// <summary>
    /// Gets or sets the access denied URL. If a local URL, the value must start with a leading slash.
    /// </summary>
    /// <value>
    /// The access denied URL.
    /// </value>
    public string AccessDeniedPath { get; set; } = Constants.UIConstants.DefaultRoutePaths.AccessDenied.EnsureLeadingSlash();

    /// <summary>
    /// Gets or sets the logging out URL. If a local URL, the value must start with a leading slash.
    /// </summary>
    /// <value>
    /// The logout URL.
    /// </value>
    public string LoggingOutPath { get; set; } = Constants.UIConstants.DefaultRoutePaths.LoggingOut.EnsureLeadingSlash();

    /// <summary>
    /// Gets or sets the logged out URL. If a local URL, the value must start with a leading slash.
    /// </summary>
    /// <value>
    /// The logout URL.
    /// </value>
    public string LoggedOutPath { get; set; } = Constants.UIConstants.DefaultRoutePaths.LoggedOut.EnsureLeadingSlash();

    /// <summary>
    /// Gets or sets the logout identifier parameter.
    /// </summary>
    /// <value>
    /// The logout identifier parameter.
    /// </value>
    public string LogoutIdParameter { get; set; } = Constants.UIConstants.DefaultRoutePathParams.Logout;

    /// <summary>
    /// Gets or sets the consent URL. If a local URL, the value must start with a leading slash.
    /// </summary>
    /// <value>
    /// The consent URL.
    /// </value>
    public string ConsentPath { get; set; } = Constants.UIConstants.DefaultRoutePaths.Consent.EnsureLeadingSlash();

    /// <summary>
    /// Gets or sets the consent return URL parameter.
    /// </summary>
    /// <value>
    /// The consent return URL parameter.
    /// </value>
    public string ConsentReturnUrlParameter { get; set; } = Constants.UIConstants.DefaultRoutePathParams.Consent;

    /// <summary>
    /// Gets or sets the select domain URL. If a local URL, the value must start with a leading slash.
    /// </summary>
    public string SelectDomainPath { get; set; } = Constants.UIConstants.DefaultRoutePaths.SelectDomain.EnsureLeadingSlash();

    /// <summary>
    /// Gets or sets the error URL. If a local URL, the value must start with a leading slash.
    /// </summary>
    /// <value>
    /// The error URL.
    /// </value>
    public string ErrorPath { get; set; } = Constants.UIConstants.DefaultRoutePaths.Error.EnsureLeadingSlash();

    /// <summary>
    /// Gets or sets the error identifier parameter.
    /// </summary>
    /// <value>
    /// The error identifier parameter.
    /// </value>
    public string ErrorIdParameter { get; set; } = Constants.UIConstants.DefaultRoutePathParams.Error;

    /// <summary>
    /// Gets or sets the custom redirect return URL parameter.
    /// </summary>
    /// <value>
    /// The custom redirect return URL parameter.
    /// </value>
    public string CustomRedirectReturnUrlParameter { get; set; } = Constants.UIConstants.DefaultRoutePathParams.Custom;

    /// <summary>
    /// Gets or sets the device verification URL.  If a local URL, the value must start with a leading slash.
    /// </summary>
    /// <value>
    /// The device verification URL.
    /// </value>
    public string DeviceVerificationUrl { get; set; } = Constants.UIConstants.DefaultRoutePaths.DeviceVerification;

    /// <summary>
    /// Gets or sets the device verification user code parameter.
    /// </summary>
    /// <value>
    /// The device verification user code parameter.
    /// </value>
    public string DeviceVerificationUserCodeParameter { get; set; } = Constants.UIConstants.DefaultRoutePathParams.UserCode;
}
