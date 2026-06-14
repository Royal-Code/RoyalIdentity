namespace RoyalIdentity.Users;

/// <summary>
/// Primitive projection of a user property as a claim, crossing the seam between the core
/// (<c>IProfileService</c>) and the accounts module (<c>IUserPropertyProvider</c>). No rich IdP types
/// cross this boundary — only strings (ADR-014 §2.9).
/// </summary>
/// <param name="Type">The claim type.</param>
/// <param name="Value">The claim value.</param>
/// <param name="ValueType">Optional claim value type (e.g. a <see cref="System.Security.Claims.ClaimValueTypes"/> entry).</param>
public sealed record UserClaimDto(string Type, string Value, string? ValueType = null);
