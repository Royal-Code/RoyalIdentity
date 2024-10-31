namespace RoyalIdentity.Utils.Caching;

public interface ICached<in TKey, TValue>
    where TKey : notnull
{
    public ValueTask<TValue> GetAsync(TKey key, CancellationToken ct);
}