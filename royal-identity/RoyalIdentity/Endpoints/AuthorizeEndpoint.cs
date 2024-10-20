using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RoyalIdentity.Contexts;
using RoyalIdentity.Endpoints.Abstractions;
using RoyalIdentity.Endpoints.Defaults;
using RoyalIdentity.Extensions;
using RoyalIdentity.Options;
using System.Collections.Specialized;

namespace RoyalIdentity.Endpoints;

/// <summary>
/// Manipulates the 'authorize' endpoint specified by 'oauth', generating the context according to the input.
/// </summary>
public class AuthorizeEndpoint : IEndpointHandler
{
    private readonly ServerOptions options;
    public readonly ILogger logger;

    public AuthorizeEndpoint(IOptions<ServerOptions> options, ILogger<AuthorizeEndpoint> logger)
    {
        this.options = options.Value;
        this.logger = logger;
    }

    public ValueTask<EndpointCreationResult> TryCreateContextAsync(HttpContext httpContext)
    {
        logger.LogDebug("Start authorize enpoint");

        NameValueCollection values;

        if (HttpMethods.IsGet(httpContext.Request.Method))
        {
            values = httpContext.Request.Query.AsNameValueCollection();
        }
        else if (HttpMethods.IsPost(httpContext.Request.Method))
        {
            if (!httpContext.Request.HasApplicationFormContentType())
            {
                // return a problem details of a UnsupportedMediaType infoming the ContentType is invalid
                var problemDetails = new ProblemDetails
                {
                    Type = "about:blank",
                    Status = StatusCodes.Status415UnsupportedMediaType,
                    Title = "Invalid ContentType",
                    Detail = "The content type must be: application/x-www-form-urlencoded"
                };

                return ValueTask.FromResult(
                    new EndpointCreationResult(
                        httpContext,
                        ResponseHandler.Problem(problemDetails)));
            }

            values = httpContext.Request.Form.AsNameValueCollection();
        }
        else
        {
            // return a problem details of a UnsupportedMediaType infoming the http method is not allowed
            var problemDetails = new ProblemDetails
            {
                Type = "about:blank",
                Status = StatusCodes.Status405MethodNotAllowed,
                Title = "Method Not Allowed",
                Detail = "HTTP method is not allowed"
            };

            return ValueTask.FromResult(
                new EndpointCreationResult(
                    httpContext,
                    ResponseHandler.Problem(problemDetails)));
        }

        var items = ContextItems.From(options);
        var context = new AuthorizeContext(httpContext, values, items);

        Load(context);

        return ValueTask.FromResult(new EndpointCreationResult(context));
    }

    private void Load(AuthorizeContext context)
    {
        var raw = context.Raw;

        var scope = raw.Get(OidcConstants.AuthorizeRequest.Scope);
        context.RequestedScopes.AddRange(scope.FromSpaceSeparatedString());

        var responseType = raw.Get(OidcConstants.AuthorizeRequest.ResponseType);
        context.ResponseTypes.AddRange(responseType.FromSpaceSeparatedString());
        context.ClientId = raw.Get(OidcConstants.AuthorizeRequest.ClientId);
        context.RedirectUri = raw.Get(OidcConstants.AuthorizeRequest.RedirectUri);
        context.State = raw.Get(OidcConstants.AuthorizeRequest.State);
        context.ResponseMode = raw.Get(OidcConstants.AuthorizeRequest.ResponseMode);
        context.Nonce = raw.Get(OidcConstants.AuthorizeRequest.Nonce);

        var display = raw.Get(OidcConstants.AuthorizeRequest.Display);
        if (display.IsPresent())
        {
            if (Constants.SupportedDisplayModes.Contains(display))
            {
                context.DisplayMode = display;
            }
            else
            {
                logger.LogDebug("Unsupported display mode - ignored: {Display}", display);
            }
        }

        var prompt = raw.Get(OidcConstants.AuthorizeRequest.Prompt);
        if (prompt.IsPresent())
        {
            var prompts = prompt.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (prompts.All(Constants.SupportedPromptModes.Contains))
            {
                context.PromptModes = prompts.ToHashSet();
            }
            else
            {
                logger.LogDebug("Unsupported prompt mode - ignored: {Promp}", prompt);
            }
        }

        var maxAge = raw.Get(OidcConstants.AuthorizeRequest.MaxAge);
        if (maxAge.IsPresent())
        {
            if (int.TryParse(maxAge, out var seconds) && seconds >= 0)
            {
                context.MaxAge = seconds;
            }
            else
            {
                logger.LogDebug("Invalid max_age - ignored: {MaxAge}", maxAge);
            }
        }

        context.UiLocales = raw.Get(OidcConstants.AuthorizeRequest.UiLocales);
        context.IdTokenHint = raw.Get(OidcConstants.AuthorizeRequest.IdTokenHint);
        context.LoginHint = raw.Get(OidcConstants.AuthorizeRequest.LoginHint);

        var acrValues = raw.Get(OidcConstants.AuthorizeRequest.AcrValues);
        context.AuthenticationContextReferenceClasses.AddRange(acrValues.FromSpaceSeparatedString());

        context.CodeChallenge = raw.Get(OidcConstants.AuthorizeRequest.CodeChallenge);
        context.CodeChallengeMethod = raw.Get(OidcConstants.AuthorizeRequest.CodeChallengeMethod);
    }
}