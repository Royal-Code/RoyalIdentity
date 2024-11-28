namespace RoyalIdentity.Users
{
    /// <summary>
    /// Identifica uma sessão de usuário.
    /// </summary>
    public class IdentitySession
    {
        /// <summary>
        /// The unique identifier for the session.
        /// This is used to identify the session in the database and in authentication processes.
        /// It is the JWT Session ID (sid) claim value.
        /// </summary>
        public required string Id { get; init; }

        /// <summary>
        /// The username of the user that is currently authenticated.
        /// </summary>
        public required string Username { get; init; }

        /// <summary>
        /// The clients (client_id) that the user has sign in into during their session.
        /// </summary>
        public IList<string> Clients { get; init; } = [];

        /// <summary>
        /// Indicates if the session is active or not.
        /// </summary>
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Date and time when the session was created.
        /// </summary>
        public required DateTime StartedAt { get; init; }
    }
}