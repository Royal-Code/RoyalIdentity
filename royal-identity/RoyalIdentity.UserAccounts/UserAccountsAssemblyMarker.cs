namespace RoyalIdentity.UserAccounts;

/// <summary>
/// Assembly anchor for the pure <c>RoyalIdentity.UserAccounts</c> module (ADR-015 §2.1).
/// The real domain (<c>Domain/</c>) and use cases (<c>Features/</c>) land in later phases of
/// <c>plan-users-accounts-module-v2</c>; both live in this assembly, which references neither the IdP
/// core nor ASP.NET — a boundary enforced by the architecture tests in <c>Tests.Architecture</c>.
/// </summary>
public sealed class UserAccountsAssemblyMarker;
