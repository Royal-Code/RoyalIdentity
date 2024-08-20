using System.Diagnostics.CodeAnalysis;

namespace RoyalIdentity.Endpoints.Abstractions;

/// <summary>
/// Stores the items of a context object.
/// </summary>
public sealed class ContextItems
{
    private readonly Dictionary<Type, object> items;

    /// <summary>
    /// Create a new context for items.
    /// </summary>
    public ContextItems() : this([]) { }

    /// <summary>
    /// Create a new context for items.
    /// </summary>
    /// <param name="items">The current itens.</param>
    public ContextItems(Dictionary<Type, object> items)
    {
        this.items = items;
    }

    /// <summary>
    /// Try to get an item for a specific type.
    /// </summary>
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="value">The object of the type, if exists.</param>
    /// <returns>
    ///     True if the item exists for the specified type, false otherwise.
    /// </returns>
    public bool TryGet<T>([NotNullWhen(true)] out T? value)
        where T: class
    {
        if (items.TryGetValue(typeof(T), out var obj))
        {
            value = (T)obj;
            return true;
        }

        value = null;
        return false;
    }

    /// <summary>
    /// Get the item for a specific type.
    /// </summary>
    /// <typeparam name="T">The item type.</typeparam>
    /// <returns>The object of the type, if exists.</returns>
    public T? Get<T>()
        where T : class
    {
        return (T)items[typeof(T)];
    }

    /// <summary>
    /// Set the item object for a specific type.
    /// </summary>
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="value">The object value.</param>
    /// <exception cref="ArgumentNullException">
    ///     If <paramref name="value"/> is null.
    /// </exception>
    public void Set<T>(T value)
        where T : class
    {
        if (value is null)
            throw new ArgumentNullException(nameof(value));

        items[typeof(T)] = value;
    }
}