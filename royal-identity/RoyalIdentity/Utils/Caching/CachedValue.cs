namespace RoyalIdentity.Utils.Caching;

internal class CachedValue<TValue>
{
    public CachedValue(TValue value, DateTimeOffset expiration)
    {
        Value = value;
        Expiration = expiration;
    }

    public TValue Value { get; private set; }

    public DateTimeOffset Expiration { get; private set; }

    public bool IsExpired => DateTimeOffset.UtcNow >= Expiration;

    public void Update(TValue value, DateTimeOffset expiration)
    {
        Value = value;
        Expiration = expiration;
    }
}