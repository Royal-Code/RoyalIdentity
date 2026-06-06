namespace RoyalIdentity.Events;

/// <summary>
/// Event raised when a request references a realm that cannot be found.
/// </summary>
/// <param name="realm">The requested realm path that could not be resolved.</param>
public class RealmNotFoundEvent(string realm)
    : Event(EventCategories.Authentication, "Realm Not Found", EventTypes.Failure)
{
    /// <summary>
    /// Gets the requested realm path that could not be resolved.
    /// </summary>
    public string Realm { get; } = realm;
}
