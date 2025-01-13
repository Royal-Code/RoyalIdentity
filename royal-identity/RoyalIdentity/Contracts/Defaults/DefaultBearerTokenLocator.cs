using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using RoyalIdentity.Contracts.Models;
using RoyalIdentity.Extensions;
using RoyalIdentity.Options;
using static RoyalIdentity.Contracts.Models.BearerTokenResult;

namespace RoyalIdentity.Contracts.Defaults;

public class DefaultBearerTokenLocator : IBearerTokenLocator
{
    private readonly ILogger logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="BearerTokenUsageValidator"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    public DefaultBearerTokenLocator(ILogger<DefaultBearerTokenLocator> logger)
    {
        this.logger = logger;
    }

    /// <summary>
    /// Validates the request.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <returns></returns>
    public async Task<BearerTokenResult> LocateAsync(HttpContext context)
    {
        var authorizationHeader = context.Request.Headers.Authorization.FirstOrDefault();
        if (authorizationHeader.IsPresent())
        {
            var result = LocatorAuthorizationHeader(authorizationHeader);
            if (result.TokenFound)
            {
                logger.LogDebug("Bearer token found in header");
                return result;
            }
        }
        
        if (context.Request.HasApplicationFormContentType())
        {
            var result = await LocatorPostBodyAsync(context);
            if (result.TokenFound)
            {
                logger.LogDebug("Bearer token found in body");
                return result;
            }
        }

        logger.LogDebug("Bearer token not found");
        return new BearerTokenResult();
    }

    /// <summary>
    /// Validates the authorization header.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <returns></returns>
    private BearerTokenResult LocatorAuthorizationHeader(string authorizationHeader)
    {
        var header = authorizationHeader.Trim();
        if (header.StartsWith(OidcConstants.AuthenticationSchemes.AuthorizationHeaderBearer) && header.Length > 7)
        {
            var value = header[7..];
                return new BearerTokenResult
                {
                    TokenFound = true,
                    Token = value,
                    UsageType = BearerTokenLocation.AuthorizationHeader
                };
        }
        else
        {
            logger.LogTrace("Unexpected header format: {Header}", header);
            return new BearerTokenResult();
        }
    }

    /// <summary>
    /// Validates the post body.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <returns></returns>
    private async Task<BearerTokenResult> LocatorPostBodyAsync(HttpContext context)
    {
        var token = (await context.Request.ReadFormAsync())["access_token"].FirstOrDefault();
        if (token.IsPresent())
        {
            return new BearerTokenResult
            {
                TokenFound = true,
                Token = token,
                UsageType = BearerTokenLocation.PostBody
            };
        }

        return new BearerTokenResult();
    }
}
