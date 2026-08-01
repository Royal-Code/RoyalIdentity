namespace RoyalIdentity.Contexts;

/// <summary>
/// The single answer every refusal of a presented authorization code gives.
/// </summary>
/// <remarks>
/// <para>
/// A code that was never issued, one already consumed, one bound to another client or redirect URI, one whose
/// <c>code_verifier</c> does not match, and one whose stored <c>code_challenge_method</c> the server cannot
/// process are all refused with <c>invalid_grant</c> and this exact text. Telling them apart would answer
/// questions the caller has no right to ask: which codes exist, who they belong to, and whether a guessed
/// verifier was the only thing missing (DF13/DF18).
/// </para>
/// <para>
/// Expiration is the deliberate exception, decided by <c>plan-data-operational-storage</c> DF11: it is told
/// once, on the exchange that consumes the code, and every later attempt gets this generic refusal because the
/// code is simply gone.
/// </para>
/// <para>
/// It is a shared constant rather than a repeated literal so the equivalence cannot drift: two call sites with
/// the same intent and different wording would recreate the oracle without any test noticing.
/// </para>
/// </remarks>
internal static class AuthorizationCodeRefusal
{
    public const string Description = "Authorization code is invalid";
}
