using RoyalIdentity.Contexts;

namespace RoyalIdentity.Contracts;

[Redesign("Ao implementar, melhorar os métodos.")]
public interface IExtensionGrant
{
    /// <summary>
    /// Validates the custom grant request.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <returns>
    /// A principal
    /// </returns>
    Task ValidateAsync(IEndpointContextBase context);

    /// <summary>
    /// Returns the grant type this validator can deal with
    /// </summary>
    /// <value>
    /// The type of the grant.
    /// </value>
    string GrantType { get; }
}