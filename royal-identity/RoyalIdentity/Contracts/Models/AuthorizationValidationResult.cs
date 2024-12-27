using RoyalIdentity.Models;
using RoyalIdentity.Users.Contexts;

namespace RoyalIdentity.Contracts.Models;

public readonly struct AuthorizationValidationResult
{
    /// <summary>
    /// Authorisation context.
    /// </summary>
    public AuthorizationContext? Context { get; init; }

    /// <summary>
    /// Details for generating error responses.
    /// </summary>
    public ErrorDetails? Error { get; init; }
}