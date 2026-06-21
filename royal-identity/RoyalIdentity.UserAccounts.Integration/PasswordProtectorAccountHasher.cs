using RoyalIdentity.UserAccounts.Features.Accounts.Domain;
using RoyalIdentity.Users.Contracts;

namespace RoyalIdentity.UserAccounts.Integration;

/// <summary>
/// Bridges the module's synchronous hashing seam (<see cref="IUserAccountPasswordHasher"/>) to the IdP's
/// <see cref="IPasswordProtector"/>, so accounts created and authenticated through the module use exactly the
/// password hashing the host configured for the IdP (parity, and respects a host override).
/// <para>
/// The module domain verifies passwords synchronously (<c>UserAccount.AuthenticateLocal</c> calls
/// <see cref="IUserAccountPasswordHasher.Verify"/> inline), so this adapter blocks on the protector's
/// <see cref="System.Threading.Tasks.ValueTask"/>. The default <c>DefaultPasswordProtector</c> completes
/// synchronously (CPU-bound hash wrapped in a completed task); under ASP.NET Core there is no synchronization
/// context, so blocking on a genuinely async protector cannot deadlock — it parks a thread-pool thread.
/// </para>
/// </summary>
public sealed class PasswordProtectorAccountHasher(IPasswordProtector passwordProtector) : IUserAccountPasswordHasher
{
    /// <inheritdoc />
    public string Hash(string password)
        => passwordProtector.HashPasswordAsync(password).AsTask().GetAwaiter().GetResult();

    /// <inheritdoc />
    public bool Verify(string password, string passwordHash)
        => passwordProtector.VerifyPasswordAsync(password, passwordHash).AsTask().GetAwaiter().GetResult();
}
