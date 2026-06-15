using RoyalIdentity.Options;

namespace RoyalIdentity.Storage.InMemory;

/// <summary>
/// Single home for the account lockout policy used by the in-memory (fake/reference) authenticator
/// (ADR-014 / plan Fase 4: "lockout num lugar so"). It reads <see cref="PasswordOptions"/> and mutates the
/// failure counters on <see cref="MemoryUserAccount"/>. In production this logic belongs to the
/// RoyalIdentity.UsersAccounts module. Realm is bound at construction (via <see cref="AccountOptions"/>).
/// </summary>
public sealed class LockoutPolicy(AccountOptions accountOptions, TimeProvider clock)
{
    /// <summary>Whether the account is currently locked out by failed-attempt count and duration.</summary>
    public bool IsLockedOut(MemoryUserAccount details)
    {
        var options = accountOptions.PasswordOptions;
        if (options.MaxFailedAccessAttempts is 0)
            return false;

        var isLockout = details.LoginAttemptsWithPasswordErrors >= options.MaxFailedAccessAttempts;

        if (isLockout &&
            options.AccountLockoutDurationMinutes is not 0 &&
            details.LastPasswordError is not null)
        {
            var lockoutDuration = clock.GetUtcNow().Subtract(details.LastPasswordError.Value).TotalMinutes;
            isLockout = lockoutDuration <= options.AccountLockoutDurationMinutes;
        }

        return isLockout;
    }

    /// <summary>Registers a failed password attempt (increments the counter and stamps the time).</summary>
    public void RegisterFailure(MemoryUserAccount details)
    {
        details.LoginAttemptsWithPasswordErrors++;
        details.LastPasswordError = clock.GetUtcNow();
    }

    /// <summary>Resets the failure state after a successful authentication.</summary>
    public void RegisterSuccess(MemoryUserAccount details)
    {
        if (details.LoginAttemptsWithPasswordErrors is 0)
            return;

        details.LoginAttemptsWithPasswordErrors = 0;
        details.LastPasswordError = null;
    }
}
