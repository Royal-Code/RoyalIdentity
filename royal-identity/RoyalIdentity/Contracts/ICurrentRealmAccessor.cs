using System.Diagnostics.CodeAnalysis;
using RoyalIdentity.Models;

namespace RoyalIdentity.Contracts;

/// <summary>
/// Supplies the current (ambient) realm to core orchestration services so they do not spread
/// <c>HttpContext.GetCurrentRealm()</c> (ADR-014 §2.5). It covers ONLY realm resolution — cookie I/O
/// (sign-in/sign-out) stays on <c>HttpContext</c>. The HTTP implementation reads the realm from the
/// current request; tests can use a fake.
/// </summary>
public interface ICurrentRealmAccessor
{
    /// <summary>Gets the current realm. Throws when no realm is available in the ambient context.</summary>
    Realm GetCurrentRealm();

    /// <summary>Tries to get the current realm without throwing.</summary>
    bool TryGetCurrentRealm([NotNullWhen(true)] out Realm? realm);
}
