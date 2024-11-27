using Microsoft.Extensions.Logging;
using RoyalIdentity.Contracts.Models;
using RoyalIdentity.Options;
using RoyalIdentity.Utils;
using System.Security.Claims;
using static RoyalIdentity.Options.OidcConstants;

namespace RoyalIdentity.Contracts.Defaults;

/// <summary>
/// Default implamentation of <see cref="IBackChannelLogoutNotifier"/>.
/// </summary>
public class DefaultBackChannelLogoutNotifier : IBackChannelLogoutNotifier
{
    private readonly TimeProvider clock;
    private readonly JwtUtil util;
    private readonly IHttpClientFactory httpClientFactory;
    private readonly ILogger logger;

    protected int defaultLogoutTokenLifetime = 5 * 60;

    public DefaultBackChannelLogoutNotifier(
        TimeProvider clock,
        JwtUtil util, 
        IHttpClientFactory httpClientFactory, 
        ILogger<DefaultBackChannelLogoutNotifier> logger)
    {
        this.clock = clock;
        this.util = util;
        this.httpClientFactory = httpClientFactory;
        this.logger = logger;
    }

    /// <inheritdoc />
    public async Task SendAsync(LogoutBackChannelRequest request, CancellationToken ct)
    {
        var data = await CreateFormPostPayloadAsync(request);
        await PostAsync(request.Uri, data);
    }

    /// <summary>
    /// Creates the form-url-encoded payload (as a dictionary) to send to the client.
    /// </summary>
    /// <param name="request"></param>
    /// <returns></returns>
    protected async Task<Dictionary<string, string>> CreateFormPostPayloadAsync(LogoutBackChannelRequest request)
    {
        var token = await CreateTokenAsync(request);

        var data = new Dictionary<string, string>
            {
                { BackChannelLogoutRequest.LogoutToken, token }
            };
        return data;
    }

    /// <summary>
    /// Creates the JWT used for the back-channel logout notification.
    /// </summary>
    /// <param name="request"></param>
    /// <returns>The token.</returns>
    protected virtual async Task<string> CreateTokenAsync(LogoutBackChannelRequest request)
    {
        var claims = await CreateClaimsForTokenAsync(request);
        if (claims.Any(x => x.Type == JwtClaimTypes.Nonce))
        {
            throw new InvalidOperationException("nonce claim is not allowed in the back-channel signout token.");
        }

        return await util.IssueJwtAsync(request.ClientId, defaultLogoutTokenLifetime, claims);
    }

    /// <summary>
    /// Create the claims to be used in the back-channel logout token.
    /// </summary>
    /// <param name="request"></param>
    /// <returns>The claims to include in the token.</returns>
    protected Task<IEnumerable<Claim>> CreateClaimsForTokenAsync(LogoutBackChannelRequest request)
    {
        if (request.RequireSessionId && request.SessionId is null)
        {
            throw new InvalidOperationException("Client requires SessionId");
        }

        var json = "{\"" + OidcConstants.Events.BackChannelLogout + "\":{} }";

        var claims = new List<Claim>
            {
                new(JwtClaimTypes.Subject, request.Subject),
                new(JwtClaimTypes.Audience, request.ClientId),
                new(JwtClaimTypes.IssuedAt, clock.GetUtcNow().ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64),
                new(JwtClaimTypes.JwtId, CryptoRandom.CreateUniqueId(16, CryptoRandom.OutputFormat.Hex)),
                new(JwtClaimTypes.Events, json, ServerConstants.ClaimValueTypes.Json)
            };

        if (request.SessionId is not null)
        {
            claims.Add(new Claim(JwtClaimTypes.SessionId, request.SessionId));
        }

        return Task.FromResult(claims.AsEnumerable());
    }

    /// <summary>
    /// Performs the HTTP POST of the logout payload to the request.
    /// </summary>
    /// <param name="url"></param>
    /// <param name="payload"></param>
    /// <returns></returns>
    protected virtual async Task PostAsync(string url, Dictionary<string, string> payload)
    {
        try
        {
            var client = httpClientFactory.CreateClient(ServerConstants.HttpClients.BackChannelLogoutHttpClient);

            var response = await client.PostAsync(url, new FormUrlEncodedContent(payload));
            if (response.IsSuccessStatusCode)
            {
                logger.LogDebug("Response from back-channel logout endpoint: {Url} status code: {Status}", url, (int)response.StatusCode);
            }
            else
            {
                logger.LogWarning("Response from back-channel logout endpoint: {Url} status code: {Status}", url, (int)response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Exception invoking back-channel logout for url: {Url}", url);
        }
    }
}
