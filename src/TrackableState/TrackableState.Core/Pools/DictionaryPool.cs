using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace Klopoff.TrackableState.Core.Pools
{
    public static class DictionaryPool<TKey, TValue>
    {
        private static readonly ConcurrentBag<Dictionary<TKey, TValue>> Pool = new();
        private static readonly Action<Dictionary<TKey, TValue>> ReturnAction = Release;
        
        private static int _count;

        public static int MaxPoolSize { get; set; } = 64;
        public static int MaxRetainedCount { get; set; } = 1024;

        public static Dictionary<TKey, TValue> Get(IEqualityComparer<TKey> comparer = null)
        {
            if (Pool.TryTake(out Dictionary<TKey, TValue> dict))
            {
                Interlocked.Decrement(ref _count);
                
                if (dict.Count != 0)
                {
                    dict.Clear();
                }

                if (comparer != null && !ReferenceEquals(dict.Comparer, comparer))
                {
                    Release(dict);
                    return new Dictionary<TKey, TValue>(comparer);
                }
                
                return dict;
            }

            return comparer is null ? new Dictionary<TKey, TValue>() : new Dictionary<TKey, TValue>(comparer);
        }

        public static void Release(Dictionary<TKey, TValue> dict)
        {
            if (dict is null)
            {
                return;
            }

            int count = dict.Count;
            dict.Clear();

            if (count > MaxRetainedCount)
            {
                dict.TrimExcess();
            }

            if (Volatile.Read(ref _count) >= MaxPoolSize)
            {
                return;
            }

            Pool.Add(dict);
            Interlocked.Increment(ref _count);
        }

        public static Pooled<Dictionary<TKey, TValue>> GetPooled(out Dictionary<TKey, TValue> dict, IEqualityComparer<TKey> comparer = null)
        {
            dict = Get(comparer);
            return new Pooled<Dictionary<TKey, TValue>>(dict, ReturnAction);
        }
    }
}