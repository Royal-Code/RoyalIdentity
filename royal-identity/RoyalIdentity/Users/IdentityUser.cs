using System.Security.Claims;

namespace RoyalIdentity.Users;

#pragma warning disable S1694 // use interface instead of abstract class - by design it is an abstract class

/// <summary>
/// Represents an identity user.
/// An identity user is a user that can be authenticated in the system.
/// </summary>
public abstract class IdentityUser
{
    /// <summary>
    /// The user name is the identifier of the user.
    /// It can be an email, a user personal name, a user id, sequence number, etc.
    /// In the OpenID Connect specification, it is called "sub" (subject).
    /// </summary>
    public abstract string UserName { get; }

    /// <summary>
    /// The display name is the name that will be displayed to the user.
    /// In the OpenID Connect specification, it is called "name".
    /// </summary>
    public abstract string DysplayName { get; }

    /// <summary>
    /// Determines whether the user is active.
    /// </summary>
    public abstract bool IsActive { get; }

    /// <summary>
    /// Processes the user's password and starts a new session.
    /// </summary>
    /// <param name="password">The password.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns></returns>
    public abstract ValueTask<ValidateCredentialsResult> AuthenticateAndStartSessionAsync(string password, CancellationToken ct = default);

    /// <summary>
    /// Verifies if the user is locked out.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if the user is locked out, otherwise false.</returns>
    public abstract ValueTask<bool> IsLockoutAsync(CancellationToken ct = default);

    /// <summary>
    /// Creates a new principal for the user.
    /// </summary>
    /// <param name="session">The session.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A new principal.</returns>
    public abstract ValueTask<ClaimsPrincipal> CreatePrincipalAsync(IdentitySession? session, CancellationToken ct = default);
}
