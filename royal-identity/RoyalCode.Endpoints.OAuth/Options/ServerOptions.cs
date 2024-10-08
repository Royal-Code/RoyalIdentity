﻿namespace RoyalIdentity.Options;

/// <summary>
/// All options for RoyalIdentity.
/// </summary>
public class ServerOptions
{
    /// <summary>
    /// Gets or sets the authentication options.
    /// </summary>
    /// <value>
    /// The authentication options.
    /// </value>
    public AuthenticationOptions Authentication { get; set; } = new();

    /// <summary>
    /// Gets or sets the max input length restrictions.
    /// </summary>
    /// <value>
    /// The length restrictions.
    /// </value>
    public InputLengthRestrictions InputLengthRestrictions { get; set; } = new();

    /// <summary>
    /// Gets or sets the logging options
    /// </summary>
    public LoggingOptions Logging { get; set; } = new();
}

