using Microsoft.AspNetCore.Http;
using RoyalIdentity.Models;
using RoyalIdentity.Models.Scopes;
using System.Security.Claims;

namespace RoyalIdentity.Contracts.Models;

public class IdentityTokenRequest
{
    /// <summary>
    /// The HttpContext for the current request.
    /// This is used to get the issuer name for the access token.
    /// </summary>
    public required HttpContext HttpContext { get; init; }

    public required ClaimsPrincipal User { get; init; }

    public required Client Client { get; init; }

    public required RequestedResources Resources { get; init; }

    public string? Nonce { get; init; }

    public string? AccessTokenToHash { get; init; }

    public string? AuthorizationCodeToHash { get; init; }

    public string? StateHash { get; init; }

    /// <summary>
    /// <para>
    ///     When set, the identity claims come from here instead of from the claims/profile provider. It is the
    ///     seam <c>RefreshTokenClaimsMode.Snapshot</c> uses: the whole point of that mode is that a renewal
    ///     reproduces the claims the grant was issued with, so an identity token minted from current profile
    ///     data would contradict the access token returned in the same response (DF32).
    /// </para>
    /// <para>
    ///     Everything else about the identity token — <c>at_hash</c>, <c>sid</c>, audiences, signing — is
    ///     unchanged, so this narrows only where the subject's claims come from.
    /// </para>
    /// </summary>
    public IReadOnlyCollection<Claim>? SnapshotClaims { get; init; }
}
