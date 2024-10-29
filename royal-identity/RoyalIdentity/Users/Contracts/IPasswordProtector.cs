namespace RoyalIdentity.Users.Contracts;

public interface IPasswordProtector
{
    /// <summary>
    /// Hashes the password.
    /// </summary>
    /// <param name="password">The password.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The hashed password.</returns>
    public ValueTask<string> HashPasswordAsync(string password, CancellationToken ct = default);

    /// <summary>
    /// Verifies the password.
    /// </summary>
    /// <param name="password">The password.</param>
    /// <param name="hash">The hash.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>True if the password is correct, false otherwise.</returns>
    public ValueTask<bool> VerifyPasswordAsync(string password, string hash, CancellationToken ct = default);
}