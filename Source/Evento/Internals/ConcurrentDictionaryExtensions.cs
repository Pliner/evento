using System.Collections.Concurrent;

namespace Evento.Internals;

internal static class ConcurrentDictionaryExtensions
{
    public static void ClearAndDispose<TKey, TValue>(this ConcurrentDictionary<TKey, TValue> source) where TValue : IDisposable where TKey : notnull
    {
        do
        {
            foreach (var key in source.Select(x => x.Key))
                if (source.TryRemove(key, out var value))
                    value.Dispose();
        } while (!source.IsEmpty);
    }
}