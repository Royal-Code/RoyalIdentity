namespace RoyalIdentity.UserAccounts.Integration;

/// <summary>
/// Assembly anchor for the integration adapter (ADR-015 §2.1) — the only project that references both the
/// IdP core and the pure module. The two anchors below intentionally touch a core type and a module type so the
/// assembly truly references both (verified by Tests.Architecture).
/// </summary>
public sealed class UserAccountsIntegrationMarker
{
    /// <summary>Anchor to a core (IdP) type — forces the reference to the <c>RoyalIdentity</c> assembly.</summary>
    public static readonly System.Type CoreAnchor = typeof(RoyalIdentity.Users.Contracts.IUserDirectory);

    /// <summary>Anchor to a module type — forces the reference to the <c>RoyalIdentity.UserAccounts</c> assembly.</summary>
    public static readonly System.Type ModuleAnchor = typeof(RoyalIdentity.UserAccounts.UserAccountsAssemblyMarker);
}
