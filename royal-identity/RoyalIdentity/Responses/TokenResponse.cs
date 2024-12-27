using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RoyalIdentity.Endpoints.Abstractions;
using RoyalIdentity.Models.Tokens;
using RoyalIdentity.Responses.HttpResults;
using System.Diagnostics.CodeAnalysis;

namespace RoyalIdentity.Responses;

public class TokenResponse : IResponseHandler
{
    public TokenResponse(
        AccessToken accessToken, 
        RefreshToken? refreshToken,
        IdentityToken? identityToken,
        string scope)
    {
        AccessToken = accessToken;
        RefreshToken = refreshToken;
        IdentityToken = identityToken;
        Scope = scope;
    }

    /// <summary>
    /// Gets or sets the access token.
    /// </summary>
    public AccessToken AccessToken { get; }

    /// <summary>
    /// Gets or sets de refresh token.
    /// </summary>
    public RefreshToken? RefreshToken { get; set; }

    /// <summary>
    /// Gets or sets the identity token.
    /// </summary>
    public IdentityToken? IdentityToken { get; set; }

    /// <summary>
    /// Gets or sets the scope.
    /// </summary>
    /// <value>
    /// The scope.
    /// </value>
    public string Scope { get; set; }

    /// <summary>
    /// Gets or sets the custom entries.
    /// </summary>
    /// <value>
    /// The custom entries.
    /// </value>
    public Dictionary<string, object>? Custom { get; private set; }

    /// <summary>
    /// Adds customised values to be returned in the http response.
    /// </summary>
    /// <param name="key">The key, a json property name.</param>
    /// <param name="value">The custom value.</param>
    public void AddCustomEntry(string key, object value)
    {
        Custom ??= [];
        Custom.Add(key, value);
    }

    public ValueTask<IResult> CreateResponseAsync(CancellationToken ct)
    {
        var values = new TokenEndpointParameters()
        {
            AccessToken = AccessToken.Token,
            TokenType = AccessToken.TokenType,
            ExpiresIn = AccessToken.Lifetime,
            RefreshToken = RefreshToken?.Token,
            IdentityToken = IdentityToken?.Token,
            Scope = Scope,
            Custom = Custom
        };

        var result = new TokenResult(values);

        return new(result);
    }

    public bool HasProblem([NotNullWhen(true)] out ProblemDetails? problem)
    {
        problem = null;
        return false;
    }
}
