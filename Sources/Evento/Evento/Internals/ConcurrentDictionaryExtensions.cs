using System.Collections.Concurrent;

namespace Evento.Internals;


public static class ConcurrentDictionaryExtensions
{
    public static void ClearAndDispose<TKey, TValue>(
        this ConcurrentDictionary<TKey, TValue> source
    ) where TValue : IDisposable where TKey : notnull
    {
        source.ClearAndDispose(x => x.Dispose());
    }

    public static void ClearAndDispose<TKey, TValue>(
        this ConcurrentDictionary<TKey, TValue> source, Action<TValue> dispose
    ) where TKey : notnull
    {
        do
        {
            foreach (var key in source.Select(x => x.Key))
                if (source.TryRemove(key, out var value))
                    dispose(value);
        } while (!source.IsEmpty);
    }

    public static void Remove<TKey, TValue>(this ConcurrentDictionary<TKey, TValue> source, TKey key) where TKey : notnull
    {
        ((IDictionary<TKey, TValue>)source).Remove(key);
    }
}
