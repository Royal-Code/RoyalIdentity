namespace RoyalIdentity.Contexts.Withs;

public interface IWithCodeChallenge : IWithClient
{
    /// <summary>
    /// Gets or sets the type of the response.
    /// </summary>
    /// <value>
    /// The type of the response.
    /// </value>
    public HashSet<string> ResponseTypes { get; }

    /// <summary>
    /// Gets or sets the code challenge
    /// </summary>
    /// <value>
    /// The code challenge
    /// </value>
    public string? CodeChallenge { get; }

    /// <summary>
    /// Gets or sets the code challenge method
    /// </summary>
    /// <value>
    /// The code challenge method
    /// </value>
    public string? CodeChallengeMethod { get; }
}
