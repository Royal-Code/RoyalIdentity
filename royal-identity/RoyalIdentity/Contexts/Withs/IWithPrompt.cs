using System.Security.Claims;

namespace RoyalIdentity.Contexts.Withs;

public interface IWithPrompt : IWithClient, IWithAcr
{
    /// <summary>
    /// Gets or sets the subject.
    /// </summary>
    /// <value>
    /// The subject.
    /// </value>
    public ClaimsPrincipal? Subject { get; }

    /// <summary>
    /// Gets or sets the collection of prompt modes.
    /// </summary>
    /// <value>
    /// The collection of prompt modes.
    /// </value>
    public HashSet<string> PromptModes { get; }

    /// <summary>
    /// Gets or sets the maximum age.
    /// </summary>
    /// <value>
    /// The maximum age.
    /// </value>
    public int? MaxAge { get; set; }
}
