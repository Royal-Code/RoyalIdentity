using System.Diagnostics.CodeAnalysis;

namespace RoyalIdentity.Users;

/// <summary>
/// Represents the result of a credential validation.
/// </summary>
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