namespace RoyalIdentity.Endpoins.Abstractions;

public sealed class ContextItems
{
    private readonly IDictionary<Type, object> items;

    public ContextItems()
    {
        items = new Dictionary<Type, object>();
    }

    public ContextItems(IDictionary<Type, object> items)
    {
        this.items = items;
    }

    public T? Get<T>()
    {
        return (T)items[typeof(T)];
    }

    public void Set<T>(T value)
    {
        if (value is null)
            throw new ArgumentNullException(nameof(value));

        items[typeof(T)] = value;
    }
}