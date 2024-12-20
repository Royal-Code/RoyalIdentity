namespace RoyalIdentity.Users.Contracts;

public interface IUserSessionStore
{
    /// <summary>
    /// Adds a client to the list of clients the user has sign in into during their session.
    /// </summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="clientId">The client identifier.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public Task AddClientIdAsync(string sessionId, string clientId, CancellationToken ct = default);

    /// <summary>
    /// Initializes a new session for the user.
    /// </summary>
    /// <param name="user">The user.</param>
    /// <param name="amr">The authentication method reference.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The session identifier.</returns>
    public Task<IdentitySession> StartSessionAsync(IdentityUser user, string amr, CancellationToken ct = default);

    /// <summary>
    /// Ends a user session and returns the <see cref="IdentitySession"/> instance if exists.
    /// </summary>
    /// <param name="sessionId">The session id.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    ///     The session if it exists, null otherwise.
    /// </returns>
    public Task<IdentitySession?> EndSessionAsync(string sessionId, CancellationToken ct = default);

    /// <summary>
    /// Try to get the current session for the current user.
    /// </summary>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>
    ///     The current session if it exists, null otherwise.
    /// </returns>
    ValueTask<IdentitySession?> GetCurrentSessionAsync(CancellationToken ct);

    /// <summary>
    /// Try to get the session for the user with the given session identifier.
    /// </summary>
    /// <param name="sessionId">The session identifier (sid).</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The session if it exists, null otherwise.</returns>
    ValueTask<IdentitySession?> GetUserSessionAsync(string sessionId, CancellationToken ct);

}