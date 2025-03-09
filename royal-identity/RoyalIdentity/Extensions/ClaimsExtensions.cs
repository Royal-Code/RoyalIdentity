using RoyalIdentity.Options;
using RoyalIdentity.Utils;
using System.Security.Claims;
using System.Text.Json;

namespace RoyalIdentity.Extensions;

internal static class ClaimsExtensions
{
    public static Dictionary<string, object> ToClaimsDictionary(this IEnumerable<Claim> claims)
    {
        var d = new Dictionary<string, object>();

        if (claims == null)
        {
            return d;
        }

        var distinctClaims = claims.Distinct(new ClaimComparer());

        foreach (var claim in distinctClaims)
        {
            if (!d.TryGetValue(claim.Type, out object? value))
            {
                d.Add(claim.Type, GetValue(claim));
            }
            else
            {
                if (value is List<object> list)
                {
                    list.Add(GetValue(claim));
                }
                else
                {
                    d.Remove(claim.Type);
                    d.Add(claim.Type, new List<object> { value, GetValue(claim) });
                }
            }
        }

        return d;
    }

    public static ClaimsIdentity CreateIdentity(this IEnumerable<Claim> claims)
    {
        var identity = new ClaimsIdentity(
            claims.Distinct(new ClaimComparer()),
            Server.AuthenticationScheme,
            JwtClaimTypes.Subject,
            JwtClaimTypes.Role);

        return identity;
    }

    private static object GetValue(Claim claim)
    {
        if ((claim.ValueType == ClaimValueTypes.Integer ||
            claim.ValueType == ClaimValueTypes.Integer32) && int.TryParse(claim.Value, out int intValue))
        {
            return intValue;
        }

        if (claim.ValueType == ClaimValueTypes.Integer64 && long.TryParse(claim.Value, out long longValue))
        {
            return longValue;
        }

        if (claim.ValueType == ClaimValueTypes.Boolean && bool.TryParse(claim.Value, out bool boolValue))
        {
            return boolValue;
        }

        if (claim.ValueType == ServerConstants.ClaimValueTypes.Json)
        {
            try
            {
                return JsonSerializer.Deserialize<JsonElement>(claim.Value);
            }
            catch { /* not required */ }
        }

        return claim.Value;
    }
}
