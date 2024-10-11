using Microsoft.AspNetCore.Http;
using RoyalIdentity.Endpoints.Abstractions;
using RoyalIdentity.Extensions;

namespace RoyalIdentity.Responses;

public class InteractionResponse : IResponseHandler
{
    private readonly IContextBase context;

    public InteractionResponse(IContextBase context)
    {
        this.context = context;
    }

    /// <summary>
    /// Gets or sets a value indicating whether the user must login.
    /// </summary>
    /// <value>
    ///   <c>true</c> if this instance is login; otherwise, <c>false</c>.
    /// </value>
    public bool IsLogin { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the user must consent.
    /// </summary>
    /// <value>
    /// <c>true</c> if this instance is consent; otherwise, <c>false</c>.
    /// </value>
    public bool IsConsent { get; set; }

    /// <summary>
    /// Gets a value indicating whether the user must be redirected to a custom page.
    /// </summary>
    /// <value>
    /// <c>true</c> if this instance is redirect; otherwise, <c>false</c>.
    /// </value>
    public bool IsRedirect => RedirectUrl.IsPresent();

    /// <summary>
    /// Gets or sets the URL for the custom page.
    /// </summary>
    /// <value>
    /// The redirect URL.
    /// </value>
    public string? RedirectUrl { get; set; }

    public ValueTask<IResult> CreateResponseAsync(CancellationToken ct)
    {
        throw new NotImplementedException();
    }
}
