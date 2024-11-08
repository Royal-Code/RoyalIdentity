namespace RoyalIdentity.Contracts;

public interface IExtensionsGrantsProvider
{
    /// <summary>
    /// Gets the available grant types.
    /// </summary>
    IReadOnlyList<string> GetAvailableGrantTypes();
}