using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RoyalIdentity.Contexts;
using RoyalIdentity.Endpoints.Abstractions;
using RoyalIdentity.Extensions;
using RoyalIdentity.Options;
using RoyalIdentity.Responses.HttpResults;
using System.Diagnostics.CodeAnalysis;

namespace RoyalIdentity.Responses;

public class InteractionResponse : IResponseHandler
{
    private readonly IEndpointContextBase context;

    public InteractionResponse(IEndpointContextBase context)
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
    [MemberNotNullWhen(true, nameof(RedirectUrl))]
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
        IResult? result = null;
        if (IsLogin)
        {
            result = new LoginPageResult(context);
        }
        else if (IsConsent)
        {
            result = new ConsentPageResult(context);
        }
        else if (IsRedirect)
        {
            result = new CustomRedirectResult(context, RedirectUrl);
        }

        if (result is null)
        {
            var problemDetails = new ProblemDetails
            {
                Type = "about:blank",
                Status = StatusCodes.Status400BadRequest,
                Title = OidcConstants.AuthorizeErrors.InvalidRequest,
                Detail = ""
            };

            result = Results.Json(problemDetails, statusCode: problemDetails.Status ?? 400);
        }

        return ValueTask.FromResult(result);
    }
}
