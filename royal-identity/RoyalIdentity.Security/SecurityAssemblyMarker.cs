namespace RoyalIdentity.Security;

/// <summary>
/// Assembly anchor for the shared technical library <c>RoyalIdentity.Security</c> (ADR-016).
/// It will hold the generic, reusable security primitives — crypto random, Base64Url, hashing,
/// fixed-time comparison, password hashing and key material — added from Phase 2 onward of
/// <c>plan-royalidentity-security</c>. This is a leaf library: it references neither the IdP core
/// (<c>RoyalIdentity</c>), any domain module, nor ASP.NET — boundaries enforced by the architecture
/// tests in <c>Tests.Architecture</c>.
/// </summary>
public sealed class SecurityAssemblyMarker;
