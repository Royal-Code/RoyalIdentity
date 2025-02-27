namespace RoyalIdentity.Contracts.Storage;

/// <summary>
/// Manage a <see cref="IStorage"/> session.
/// </summary>
public interface IStorageSession : IDisposable
{
    /// <summary>
    /// Gets the storage.
    /// </summary>
    /// <returns>
    ///     An instance of <see cref="IStorage"/> that can be used to access the storage.
    /// </returns>
    IStorage GetStorage();
}