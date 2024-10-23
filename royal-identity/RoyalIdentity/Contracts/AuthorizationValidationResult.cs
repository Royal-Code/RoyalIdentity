using RoyalIdentity.Models;
using RoyalIdentity.Users.Contexts;

namespace RoyalIdentity.Contracts;

public readonly struct AuthorizationValidationResult
{
    /// <summary>
    /// Authorisation context.
    /// </summary>
    public AuthorizationContext? Context { get; init; }

    /// <summary>
    /// Details for generating error responses.
    /// </summary>
    public ValidationError? Error { get; init; }
}