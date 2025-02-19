using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using RoyalIdentity.Contexts;
using RoyalIdentity.Contracts.Models;
using RoyalIdentity.Endpoints.Abstractions;
using RoyalIdentity.Models;
using RoyalIdentity.Users.Contexts;
using static RoyalIdentity.Options.OidcConstants;

namespace RoyalIdentity.Contracts.Defaults;

public class DefaultAuthorizeRequestValidator : IAuthorizeRequestValidator
{
    private readonly IHttpContextAccessor httpContextAccessor;
    private readonly IPipelineDispatcher dispatcher;
    private readonly ILogger logger;

    public DefaultAuthorizeRequestValidator(
        IHttpContextAccessor httpContextAccessor,
        IPipelineDispatcher dispatcher,
        ILogger<DefaultAuthorizeRequestValidator> logger)
    {
        this.httpContextAccessor = httpContextAccessor;
        this.dispatcher = dispatcher;
        this.logger = logger;
    }

    public async Task<AuthorizationValidationResult> ValidateAsync(
        AuthorizationValidationRequest request, 
        CancellationToken ct)
    {
        var httpContext = httpContextAccessor.HttpContext;
        if (httpContext is null)
        {
            logger.LogInformation("Authorization validation request is not being executed in an HTTP context");

            throw new InvalidOperationException("AuthorizeRequestValidator requires execution under an HTTP context.");
        }

        var context = new AuthorizeValidateContext(httpContext, request.Parameters);
        context.Load(logger);

        await dispatcher.SendAsync(context, ct);

        if (context.Response is null)
            throw new InvalidOperationException("No response generated for the pipeline");

        if (context.Response.HasProblem(out var problems))
            return new AuthorizationValidationResult()
            {
                Error = new ErrorDetails()
                {
                    Error = problems.Title ?? AuthorizeErrors.InvalidRequest,
                    ErrorDescription = problems.Detail,
                    ErrorUri = problems.Instance
                }
            };

        return new AuthorizationValidationResult()
        {
            Context = new AuthorizationContext(context)
        };
    }
}
