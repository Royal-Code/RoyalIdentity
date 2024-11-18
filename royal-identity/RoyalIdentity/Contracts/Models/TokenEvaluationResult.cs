using RoyalIdentity.Models;
using System.Diagnostics.CodeAnalysis;

namespace RoyalIdentity.Contracts.Models;

/// <summary>
/// <para>
///     Models the validation result of access tokens and id tokens.
/// </para>
/// <para>
///     It could be called TokenValidationResult, but that has a name conflict with a Microsoft class.
/// </para>
/// </summary>

public class TokenEvaluationResult
{
    public TokenEvaluationResult(ValidationError error)
    {
        IsValid = false;
        HasError = true;
        Error = error;
    }

    public TokenEvaluationResult(EvaluatedToken token)
    {
        IsValid = true;
        HasError = false;
        Token = token;
    }

    [MemberNotNullWhen(true, nameof(Token))]
    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsValid { get; }

    [MemberNotNullWhen(false, nameof(Token))]
    [MemberNotNullWhen(true, nameof(Error))]
    public bool HasError { get; }

    public ValidationError? Error { get; }

    public EvaluatedToken? Token { get; }
}
