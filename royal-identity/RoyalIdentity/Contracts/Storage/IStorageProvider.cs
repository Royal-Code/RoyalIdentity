namespace RoyalIdentity.Contracts.Storage;

/// <summary>
/// Provides access to a storage.
/// </summary>
public interface IStorageProvider
{
    /// <summary>
    /// Creates a new session.
    /// </summary>
    /// <returns>
    ///     An instance of <see cref="IStorageSession"/> that can be used to access the storage.
    /// </returns>
    IStorageSession CreateSession();
}