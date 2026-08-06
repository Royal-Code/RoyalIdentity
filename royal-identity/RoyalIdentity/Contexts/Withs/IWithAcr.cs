namespace RoyalIdentity.Contexts.Withs;

public interface IWithAcr : IEndpointContextBase
{
    /// <summary>
    /// Gets the authentication context reference class preferences in the order supplied by the client.
    /// </summary>
    /// <value>
    /// The ordered, distinct authentication context reference class preferences.
    /// </value>
    public IReadOnlyList<string> AcrValues { get; }
}
