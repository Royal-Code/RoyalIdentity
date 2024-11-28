using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;

namespace RoyalIdentity.Users;

public abstract class IdentityUser
{
    public abstract string UserName { get; }

    public abstract string DysplayName { get; }

    public abstract bool IsActive { get; }

    public abstract ValueTask<ValidateCredentialsResult> ValidateCredentialsAsync(string password, CancellationToken ct = default);

    public abstract ValueTask<bool> IsBlockedAsync(CancellationToken ct = default);

    public abstract ValueTask<ClaimsPrincipal> CreatePrincipalAsync(IdentitySession? session, string? amr, CancellationToken ct = default);
}

public readonly struct ValidateCredentialsResult
{
    // implicit conversion from bool to ValidateCredentialsResult
    public static implicit operator ValidateCredentialsResult(bool isValid) => new(isValid, null);

    // implicit conversion from IdentitySession to ValidateCredentialsResult
    public static implicit operator ValidateCredentialsResult(IdentitySession session) => new(true, session);

    public ValidateCredentialsResult(bool isValid, IdentitySession? session)
    {
        IsValid = isValid;
        Session = session;

        // if is valid, session must not be null
        if (isValid && session is null)
        {
            throw new InvalidOperationException("Session must not be null when the result is valid");
        }
    }

    [MemberNotNullWhen(true, nameof(Session))]
    public bool IsValid { get; }

    public IdentitySession? Session { get; }
}