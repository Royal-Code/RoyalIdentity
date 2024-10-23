using RoyalIdentity.Extensions;
using RoyalIdentity.Options;

namespace RoyalIdentity.Contexts.Withs;

public interface IWithAcr : IEndpointContextBase
{
    /// <summary>
    /// Gets or sets the authentication context reference classes.
    /// </summary>
    /// <value>
    /// The authentication context reference classes.
    /// </value>
    public HashSet<string> AcrValues { get; }

    public string? GetPrefixedAcrValue(string prefix)
    {
        var value = AcrValues.FirstOrDefault(x => x.StartsWith(prefix));

        if (value is not null)
            value = value.Substring(prefix.Length);


        return value;
    }

    public void RemovePrefixedAcrValue(string prefix)
    {
        foreach (var acr in AcrValues.Where(acr => acr.StartsWith(prefix, StringComparison.Ordinal)))
        {
            AcrValues.Remove(acr);
        }
        var acr_values = AcrValues.ToSpaceSeparatedString();
        if (acr_values.IsPresent())
        {
            Raw[OidcConstants.AuthorizeRequest.AcrValues] = acr_values;
        }
        else
        {
            Raw.Remove(OidcConstants.AuthorizeRequest.AcrValues);
        }
    }

    public string? GetIdP()
    {
        return GetPrefixedAcrValue(Constants.KnownAcrValues.HomeRealm);
    }

    public void RemoveIdP()
    {
        RemovePrefixedAcrValue(Constants.KnownAcrValues.HomeRealm);
    }
}
