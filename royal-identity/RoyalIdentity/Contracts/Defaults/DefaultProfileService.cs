using RoyalIdentity.Contracts.Models;
using RoyalIdentity.Extensions;
using RoyalIdentity.Models;
using System.Security.Claims;

namespace RoyalIdentity.Contracts.Defaults;

public class DefaultProfileService : IProfileService
{
    public ValueTask GetProfileDataAsync(ProfileDataRequest request, CancellationToken ct)
    {
        request.IssuedClaims.AddRange(request.Subject.Claims);
        return ValueTask.CompletedTask;
    }

    public ValueTask<bool> IsActiveAsync(ClaimsPrincipal subject, Client client, string caller, CancellationToken ct)
    {
        return ValueTask.FromResult(true);
    }
}
