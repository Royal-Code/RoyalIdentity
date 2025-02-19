namespace RoyalIdentity.Models;

/// <summary>
/// Represents the permissions (in terms of scopes) granted to a client by a subject
/// </summary>
public class Consent
{
    /// <summary>
    /// Gets or sets the subject identifier.
    /// </summary>
    /// <value>
    /// The subject identifier.
    /// </value>
    public required string SubjectId { get; set; }

    /// <summary>
    /// Gets or sets the client identifier.
    /// </summary>
    /// <value>
    /// The client identifier.
    /// </value>
    public required string ClientId { get; set; }

    /// <summary>
    /// Gets or sets the scopes.
    /// </summary>
    /// <value>
    /// The scopes.
    /// </value>
    public IList<ConsentedScope>? Scopes { get; set; }

    /// <summary>
    /// Gets or sets the creation time.
    /// </summary>
    /// <value>
    /// The creation time.
    /// </value>
    public required DateTime CreationTime { get; set; }

    /// <summary>
    /// Gets or sets the expiration.
    /// </summary>
    /// <value>
    /// The expiration.
    /// </value>
    public DateTime? Expiration { get; set; }

    public void AddScopes(IEnumerable<ConsentedScope> scopes)
    {
        Scopes ??= [];
        foreach (var scope in scopes)
        {
            var old = Scopes.FirstOrDefault(s => s.Scope == scope.Scope);
            if (old is not null)
                Scopes.Remove(old);

            Scopes.Add(scope);
        }
    }

    public IReadOnlyList<string> GetValidScopes()
    {
        return Scopes?.Select(s => s.Scope).ToList() ?? [];
    }

    /// <summary>
    /// It informs you that consent has been required during an authorization.
    /// Consents that have only been granted once can therefore be removed.
    /// </summary>
    /// <returns>
    /// If any consent was removed because it was 'just once', then it returns true. 
    /// If no consent was removed, it returns false.
    /// </returns>
    public bool RemoveTemporaryConsents()
    {
        if (Scopes is null)
            return false;

        var justOnceScopes = Scopes.Where(s => s.JustOnce).ToList();
        if (justOnceScopes.Count is 0)
            return false;

        justOnceScopes.ForEach(s => Scopes.Remove(s));
        return true;
    }
}

/// <summary>
/// The scope that the user has consented to.
/// </summary>
public class ConsentedScope
{
    /// <summary>
    /// Gets or sets the consented scope.
    /// </summary>
    /// <value>
    /// Consented scope.
    /// </value>
    public required string Scope { get; set; }

    /// <summary>
    /// Gets or sets the description.
    /// </summary>
    /// <value>
    /// The user description.
    /// </value>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the creation time.
    /// </summary>
    /// <value>
    /// The creation time.
    /// </value>
    public DateTimeOffset CreationTime { get; set; }

    /// <summary>
    /// Gets or sets just once.
    /// </summary>
    /// <value>
    /// The user consented only once.
    /// </value>
    public bool JustOnce { get; set; }
}