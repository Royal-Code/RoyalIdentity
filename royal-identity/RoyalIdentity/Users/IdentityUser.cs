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
