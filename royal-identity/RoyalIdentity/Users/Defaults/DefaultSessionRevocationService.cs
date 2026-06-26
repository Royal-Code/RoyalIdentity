using RoyalIdentity.Contracts;
using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Users.Contracts;

namespace RoyalIdentity.Users.Defaults;

/// <summary>
/// Core-owned <see cref="ISessionRevocationService"/> (Q13): executes active revocation of a subject's sessions and
/// refresh tokens over the pure stores, for the ambient realm. Idempotent and meant to run post-commit. The
/// integration translates the module's per-trigger policy (<c>SessionInvalidation</c>) into the core
/// <see cref="SessionRevocation"/> scope before calling here, so the core never references the module enum.
/// </summary>
public sealed class DefaultSessionRevocationService(
    IStorage storage,
    ICurrentRealmAccessor realmAccessor) : ISessionRevocationService
{
    public async Task RevokeAsync(
        string subjectId,
        SessionRevocation revocation,
        string? currentSessionId,
        CancellationToken ct = default)
    {
        if (revocation is SessionRevocation.None)
            return;

        var realm = realmAccessor.GetCurrentRealm();

        var sessionScope = revocation & SessionRevocation.AllSessions;
        if (sessionScope is not SessionRevocation.None)
        {
            var sessionStore = storage.GetUserSessionStore(realm);

            if (sessionScope.HasFlag(SessionRevocation.OtherSessions))
            {
                // Preserve the current session only when the scope does not also target it.
                var except = sessionScope.HasFlag(SessionRevocation.CurrentSession) ? null : currentSessionId;
                await sessionStore.EndSessionsForSubjectAsync(subjectId, except, ct);
            }
            else if (currentSessionId is not null)
            {
                // Current session only.
                var currentSession = await sessionStore.FindByIdAsync(currentSessionId, ct);
                if (currentSession?.SubjectId == subjectId)
                {
                    await sessionStore.EndAsync(currentSessionId, ct);
                }
            }
        }

        if (revocation.HasFlag(SessionRevocation.RefreshTokens))
        {
            await storage.GetRefreshTokenStore(realm).RemoveBySubjectAsync(subjectId, ct);
        }
    }
}
