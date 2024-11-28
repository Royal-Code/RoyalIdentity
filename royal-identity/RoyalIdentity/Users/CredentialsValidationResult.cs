using System.Diagnostics.CodeAnalysis;

namespace RoyalIdentity.Users;

public readonly struct CredentialsValidationResult
{
    public CredentialsValidationResult(IdentityUser user, IdentitySession session)
    {
        User = user;
        Session = session;
        Reason = null;
        ErrorMessage = null;
    }

    public CredentialsValidationResult(string reason, string errorMessage)
    {
        User = null;
        Reason = reason;
        ErrorMessage = errorMessage;
    }

    [MemberNotNullWhen(true, nameof(User), nameof(Session))]
    [MemberNotNullWhen(false, nameof(Reason), nameof(ErrorMessage))]
    public bool Success => User is not null;

    public string? Reason { get; }

    public string? ErrorMessage { get;}

    public IdentityUser? User { get; }

    public IdentitySession? Session { get; }

    public static class WellKnownReasons
    {
        public const string NotFound = "NotFound";
        public const string Inactive = "Inactive";
        public const string InvalidCredentials = "InvalidCredentials";
        public const string Blocked = "Blocked";
    }
}