using RoyalIdentity.Models;
using RoyalIdentity.Models.Tokens;

namespace RoyalIdentity.Contracts.Storage;

/// <summary>
/// <para>
///     Single-use consumption of an authorization code as the token flow sees it. It exists so the flow never
///     has to know whether the backing implements <see cref="ISingleUseAuthorizationCodeStore"/>: when the
///     capability is present the consumption is the atomic primitive of MP-2; when it is not — only while the
///     in-memory fake is still the default backing — this seam takes the legacy get-then-remove path
///     explicitly (plan-data-operational-storage DF39).
/// </para>
/// <para>
///     The fallback and this detection disappear in Plano 4 together with the default backing swap. The fake
///     never gains the capability (ADR-018), and the EF composition fails validation if it is missing, so it
///     can never reach the fallback.
/// </para>
/// </summary>
public interface IAuthorizationCodeConsumer
{
    /// <summary>
    /// Consumes the code for the given realm, binding it to the expected client and redirect URI. Returns
    /// <c>null</c> — indistinguishably — for an absent code, a code already consumed, and a code whose
    /// binding does not match; a code that fails the binding is not consumed.
    /// </summary>
    Task<AuthorizationCode?> ConsumeAsync(
        Realm realm, string code, string clientId, string redirectUri, CancellationToken ct = default);
}
