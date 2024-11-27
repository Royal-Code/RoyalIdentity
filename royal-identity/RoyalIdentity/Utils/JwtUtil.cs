using Microsoft.AspNetCore.Http;
using RoyalIdentity.Contracts;
using RoyalIdentity.Extensions;
using RoyalIdentity.Models.Tokens;
using System.Security.Claims;

namespace RoyalIdentity.Utils;

public class JwtUtil
{
    private readonly IJwtFactory jwtFactory;
    private readonly TimeProvider clock;
    private readonly IHttpContextAccessor accessor;

    public JwtUtil(IJwtFactory jwtFactory, TimeProvider clock, IHttpContextAccessor accessor)
    {
        this.jwtFactory = jwtFactory;
        this.clock = clock;
        this.accessor = accessor;
    }

    /// <summary>
    /// Issues a JWT.
    /// </summary>
    /// <param name="lifetime">The lifetime.</param>
    /// <param name="claims">The claims.</param>
    /// <returns></returns>
    /// <exception cref="System.ArgumentNullException">claims</exception>
    public virtual async Task<string> IssueJwtAsync(string clientId, int lifetime, IEnumerable<Claim> claims)
    {
        var issuer = accessor.HttpContext?.GetServerIssuerUri();

        var token = new Jwt(clientId, issuer!, clock.GetUtcNow().UtcDateTime, lifetime, claims);

        await jwtFactory.CreateTokenAsync(token, default);

        return token.Token;
    }

    /// <summary>
    /// Issues a JWT.
    /// </summary>
    /// <param name="lifetime">The lifetime.</param>
    /// <param name="issuer">The issuer.</param>
    /// <param name="claims">The claims.</param>
    /// <returns></returns>
    /// <exception cref="System.ArgumentNullException">claims</exception>
    public virtual async Task<string> IssueJwtAsync(string clientId, int lifetime, string issuer, IEnumerable<Claim> claims)
    {
        var token = new Jwt(clientId, issuer, clock.GetUtcNow().UtcDateTime, lifetime, claims);

        await jwtFactory.CreateTokenAsync(token, default);

        return token.Token;
    }

    private sealed class Jwt : TokenBase
    {
        public Jwt(string clientId, string issuer, DateTime creationTime, int lifetime, IEnumerable<Claim> claims) 
            : base(clientId, issuer, creationTime, lifetime)
        {
            Claims.AddRange(claims);
        }
    }
}
