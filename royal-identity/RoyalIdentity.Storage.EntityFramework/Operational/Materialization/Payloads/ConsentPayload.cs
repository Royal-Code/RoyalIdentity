using RoyalIdentity.Models;

namespace RoyalIdentity.Storage.EntityFramework.Operational.Materialization.Payloads;

/// <summary>A persisted <see cref="ConsentedScope"/>. The scope name keeps its original casing (plan DF14).</summary>
public sealed class ConsentedScopePayload
{
    public required string Scope { get; set; }

    public string? Description { get; set; }

    public DateTimeOffset CreationTime { get; set; }

    public bool JustOnce { get; set; }

    public static ConsentedScopePayload From(ConsentedScope scope) => new()
    {
        Scope = scope.Scope,
        Description = scope.Description,
        CreationTime = scope.CreationTime,
        JustOnce = scope.JustOnce,
    };

    public ConsentedScope ToConsentedScope() => new()
    {
        Scope = Scope,
        Description = Description,
        CreationTime = CreationTime,
        JustOnce = JustOnce,
    };
}

/// <summary>
/// The persisted graph of a <see cref="Consent"/>. Realm, subject, client and the timestamps are the
/// relational identity; the consented scopes live here.
/// </summary>
public sealed class ConsentPayload
{
    /// <summary>
    /// <c>null</c> distinguishes a consent whose scope collection was never set from one with an empty
    /// collection, so the round-trip reproduces the model exactly.
    /// </summary>
    public List<ConsentedScopePayload>? Scopes { get; set; }
}
